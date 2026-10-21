using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SB2Hotas;

// Triple screen: the game's borderless window is stretched over every monitor, so SpaceBourne 2 draws one wide
// picture without NVIDIA Surround or AMD Eyefinity. The mod's screen.lua widens the field of view to match (Hor+)
// and can keep the HUD on the centre monitor; screen.txt tells it which slice of the window that monitor is.
// Nothing is injected and no game file changes: this only moves and resizes a window, like dragging it would.
internal sealed class Screens
{
    public readonly record struct Display(Rectangle Bounds, bool Primary);

    IntPtr window;                      // the game window, and its style and place before we stretched it
    int oldStyle;
    Rectangle oldRect;
    bool stretched, gaveUp;
    readonly Queue<long> retries = new();
    string written = "";

    // Called about once a second. Returns the text for the Triple screen tab.
    public string Tick(bool span, bool fov, bool hud, string process)
    {
        var displays = Displays();
        var area = displays.Count == 0 ? Rectangle.Empty : displays.Select(d => d.Bounds).Aggregate(Rectangle.Union);
        var game = FindGameWindow(process);
        if (game != window) { window = game; stretched = gaveUp = false; retries.Clear(); }
        bool multi = displays.Count > 1;
        if (!span || !multi) PutBack();
        bool spread = span && multi && game != IntPtr.Zero && !gaveUp && Stretch(game, area);

        // The centre monitor's slice of the window. Width 0 lets the mod use a centred 16:9 slice instead,
        // which suits a single ultrawide or an NVIDIA Surround / AMD Eyefinity screen.
        var centre = Centre(displays);
        double left = 0, width = 0;
        if (spread) { left = (double)(centre.X - area.X) / area.Width; width = (double)centre.Width / area.Width; }
        WriteOptions(fov, hud, left, width);

        var text = new StringBuilder("Monitors, left to right: ");
        text.Append(string.Join("  ·  ", displays.Select(d => $"{d.Bounds.Width}×{d.Bounds.Height}" + (d.Primary ? " (main)" : ""))));
        if (multi) text.Append($"  →  together {area.Width}×{area.Height}.");
        text.Append("\r\n");
        if (multi && displays.Any(d => d.Bounds.Top != area.Top || d.Bounds.Height != area.Height))
            text.Append("Your monitors don't all have the same height (or aren't lined up by their top edge), so part of the picture "
                + "falls outside the smaller ones. Give them the same height first: see \"Triple screen\" in the README "
                + "(NVIDIA DSR/DLDSR or a custom resolution), then line them up at the top in Windows display settings.\r\n");
        text.Append(!multi ? "Only one monitor found, so there is nothing to stretch. The FOV and HUD options still help an ultrawide or Surround screen."
            : !span ? "Stretching is off."
            : game == IntPtr.Zero ? "Waiting for SpaceBourne 2…"
            : gaveUp ? "SpaceBourne 2 keeps putting its window back. In the game, set Display Mode to Windowed Fullscreen (or Windowed), "
                + "then untick and tick the first box again."
            : spread ? $"SpaceBourne 2 is stretched over all {displays.Count} monitors ({area.Width}×{area.Height})."
            : "SpaceBourne 2 is minimised.");
        return text.ToString();
    }

    bool Stretch(IntPtr h, Rectangle area) => PerMonitor(() =>
    {
        if (IsIconic(h)) return false;                       // minimised: leave it alone
        if (IsZoomed(h)) ShowWindow(h, SW_RESTORE);
        int style = GetWindowLong(h, GWL_STYLE);
        GetWindowRect(h, out var r);
        var now = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        int want = (style & ~Frame) | WS_POPUP;
        if (style == want && now == area) return true;
        if (!stretched) { oldStyle = style; oldRect = now; stretched = true; }
        else
        {
            // The game put its window back (it may once, while starting). A few retries, then stop fighting it.
            long t = Environment.TickCount64;
            retries.Enqueue(t);
            while (retries.Peek() < t - 30000) retries.Dequeue();
            if (retries.Count > 3) { gaveUp = true; return false; }
        }
        if (style != want) SetWindowLong(h, GWL_STYLE, want);
        SetWindowPos(h, IntPtr.Zero, area.X, area.Y, area.Width, area.Height, Move);
        return true;
    });

    void PutBack()
    {
        if (stretched && IsWindow(window)) PerMonitor(() =>
        {
            SetWindowLong(window, GWL_STYLE, oldStyle);
            SetWindowPos(window, IntPtr.Zero, oldRect.X, oldRect.Y, oldRect.Width, oldRect.Height, Move);
            return true;
        });
        stretched = gaveUp = false;
        retries.Clear();
    }

    void WriteOptions(bool fov, bool hud, double left, double width)
    {
        string N(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        var text = $"{(fov ? 1 : 0)} {(hud ? 1 : 0)} {N(left)} {N(width)}";
        if (text == written) return;
        var path = Data.PathOf("screen.txt");
        try { File.WriteAllText(path + ".tmp", text); File.Move(path + ".tmp", path, true); written = text; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }   // tried again next second
    }

    // Real pixels on every monitor, whatever the Windows scale settings.
    public static List<Display> Displays() => PerMonitor(() =>
    {
        var list = new List<Display>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, _, _, _) =>
        {
            var info = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(m, ref info))
                list.Add(new Display(Rectangle.FromLTRB(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom), (info.Flags & 1) != 0));
            return true;
        }, IntPtr.Zero);
        list.Sort((a, b) => a.Bounds.X.CompareTo(b.Bounds.X));
        return list;
    });

    // With an odd number of monitors in a row, the middle one; otherwise the main one.
    static Rectangle Centre(List<Display> displays) =>
        displays.Count == 0 ? Rectangle.Empty
        : displays.Count % 2 == 1 ? displays[displays.Count / 2].Bounds
        : displays.FirstOrDefault(d => d.Primary, displays[0]).Bounds;

    static IntPtr FindGameWindow(string process)
    {
        var ids = new HashSet<uint>();
        foreach (var p in Process.GetProcessesByName(process)) using (p) ids.Add((uint)p.Id);
        if (ids.Count == 0) return IntPtr.Zero;
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;
        var name = new StringBuilder(64);
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out uint id);
            if (!ids.Contains(id) || !IsWindowVisible(h)) return true;
            name.Clear();
            GetClassName(h, name, name.Capacity);
            if (name.ToString() != "UnrealWindow") return true;   // skips the splash screen and message boxes
            GetWindowRect(h, out var r);
            long a = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
            if (a > bestArea) { best = h; bestArea = a; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    static T PerMonitor<T>(Func<T> action)
    {
        IntPtr old = IntPtr.Zero;
        try { old = SetThreadDpiAwarenessContext(new IntPtr(-4)); }   // PER_MONITOR_AWARE_V2
        catch (EntryPointNotFoundException) { }                        // older Windows: coordinates are already real
        try { return action(); }
        finally { if (old != IntPtr.Zero) SetThreadDpiAwarenessContext(old); }
    }

    const int GWL_STYLE = -16, SW_RESTORE = 9;
    const int WS_POPUP = unchecked((int)0x80000000);
    const int Frame = 0x00C00000 | 0x00040000 | 0x00080000 | 0x00020000 | 0x00010000;   // caption, sizing border, menu, min/max
    const uint Move = 0x0004 | 0x0010 | 0x0200 | 0x0020;                                  // no z-order, no activate, no owner, frame changed

    delegate bool WindowProc(IntPtr h, IntPtr data);
    delegate bool MonitorProc(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int Size; public RECT Monitor, Work; public int Flags; }
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowProc proc, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorProc proc, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder name, int size);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint id);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong(IntPtr h, int index, int value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}

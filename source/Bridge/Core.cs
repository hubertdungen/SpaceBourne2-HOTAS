using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;

namespace SB2Hotas;

// The mod's config.lua builds the same folder (%LOCALAPPDATA%\SB2HOTAS), so neither side needs machine-specific paths.
public static class Data
{
    public static readonly string Dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SB2HOTAS");
    public static string PathOf(string name)=>Path.Combine(Dir,name);
}

public sealed class AxisBinding
{
    public string Device { get; set; } = "";
    public int Axis { get; set; }
    public double Min { get; set; } = -32768;
    public double Center { get; set; }
    public double Max { get; set; } = 32767;
    public double Deadzone { get; set; } = .06;
    public double Curve { get; set; } = 1.4;
    public bool Invert { get; set; }
    public bool Unipolar { get; set; }
    public bool Confirmed { get; set; }

    public static AxisBinding For(string movement)=>movement is "Throttle" or Settings.Reverse ? new() { Unipolar = true, Curve = 1 } : new();

    public double Normalize(double raw)
    {
        if (!double.IsFinite(raw) || Max - Min < 100 || (!Unipolar && (Center <= Min || Center >= Max))) return 0;
        var v = Unipolar ? Math.Clamp((raw-Min)/(Max-Min),0,1)
            : Math.Clamp((raw-Center)/(raw >= Center ? Max-Center : Center-Min),-1,1);
        if (Invert) v = Unipolar ? 1-v : -v;
        var dz = Math.Clamp(Deadzone,0,.4);
        v = Math.Sign(v)*Math.Max(0,(Math.Abs(v)-dz)/(1-dz));
        return Math.Sign(v)*Math.Pow(Math.Abs(v),Math.Clamp(Curve,.5,3));
    }
}

public sealed class Settings
{
    // Order matters: it is the axis order of the protocol and of the mask bits.
    public static readonly string[] Movements = ["Pitch","Roll","Yaw","Throttle","Strafe","Lift"];
    // Cockpit view axes: the bridge folds them into the head pose, so they never reach the protocol as flight axes.
    public static readonly string[] LookMovements = ["Look Yaw","Look Pitch"];
    // A second lever for reverse (e.g. one throttle lever forward, the other backward); it subtracts from Throttle.
    public const string Reverse = "Reverse thrust";
    public static IEnumerable<string> AllMovements => Movements.Append(Reverse).Concat(LookMovements);
    public int UdpPort { get; set; } = 4242;
    public Dictionary<string,AxisBinding> Axes { get; set; } = AllMovements.ToDictionary(m=>m,AxisBinding.For);
    public bool HeadEnabled { get; set; } = true;
    public List<ButtonBinding> Buttons { get; set; } = GameActions();
    public double TurnRate { get; set; } = 55;
    // Triple screen tab: Screens.cs stretches the game window; the mod's screen.lua does the FOV and the HUD.
    // The game folder picked in Install mod (GOG or another non-Steam copy), for the Play button. "" = Steam.
    public string GameFolder { get; set; } = "";
    // All off by default; ticking SpanScreens in the bridge ticks the other two as well.
    public bool SpanScreens { get; set; }
    public bool WideFov { get; set; }
    public bool CentreHud { get; set; }
    // SpaceBourne 2's ship actions with their default keys, so players only have to detect the button.
    public static List<ButtonBinding> GameActions() =>
    [
        new() { Name="Fire", Output="Left mouse" }, new() { Name="Aim", Output="Right mouse" },
        new() { Name="Lock target", Output="Middle mouse" }, new() { Name="Cycle targets", Output="<" },
        new() { Name="Missile", Output="Space" }, new() { Name="Flares", Output="X" },
        new() { Name="Weapon group 1", Output="1" }, new() { Name="Weapon group 2", Output="2" },
        new() { Name="Boost", Output="Shift" }, new() { Name="Speed lock", Output="Caps Lock" },
        new() { Name="Warp drive", Output="B" }, new() { Name="Landing gear", Output="N" },
        new() { Name="Engine on/off", Output="Alt" }, new() { Name="Free look", Output="Z" },
        new() { Name="Switch camera", Output="C" }, new() { Name="Ship modes", Output="Tab" },
        new() { Name="Perk 1", Output="3" }, new() { Name="Perk 2", Output="4" }, new() { Name="Perk 3", Output="5" },
        new() { Name="Usable 1", Output="6" }, new() { Name="Usable 2", Output="7" }, new() { Name="Usable 3", Output="8" },
        new() { Name="Power to weapons", Output="Up" }, new() { Name="Power to shields", Output="Right" },
        new() { Name="Power to engines", Output="Left" }, new() { Name="Reset power", Output="Down" },
    ];
    // Templates may carry // comments explaining their guesses.
    static readonly JsonSerializerOptions ReadOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    public static Settings Load(string path)
    {
        var s = File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), ReadOptions) ?? new() : new();
        foreach (var m in AllMovements) if (!s.Axes.ContainsKey(m)) s.Axes[m] = AxisBinding.For(m);
        // Unknown names (typos in a template) would be invisible in the grid yet still block flight.
        foreach (var k in s.Axes.Keys.Except(AllMovements).ToList()) s.Axes.Remove(k);
        // Profiles from the first builds only had eight empty "Button N" rows: give them the game's actions.
        if (s.Buttons.Count == 8 && s.Buttons.All(b => b.Button < 0 && b.Output == "None" && b.Name.StartsWith("Button ") && int.TryParse(b.Name[7..], out _)))
            s.Buttons = GameActions();
        foreach (var b in s.Buttons)
        {
            if (!ButtonOutput.Options.Contains(b.Output)) b.Output = "None";
            if (!ButtonBinding.Modes.Contains(b.Mode)) b.Mode = "Hold";
        }
        s.TurnRate = Math.Clamp(s.TurnRate,10,120);
        return s;
    }
    // Exported profiles carry "Buttons"; device templates carry only the axes they assign.
    public static bool IsFullProfile(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("Buttons", out _);
    }
    public void Save(string path)
    {
        var tmp=path+".tmp";
        File.WriteAllText(tmp,JsonSerializer.Serialize(this,new JsonSerializerOptions { WriteIndented=true }));
        File.Move(tmp,path,true);
    }
}

public sealed class ButtonBinding
{
    public string Name { get; set; } = "Button";
    public string Device { get; set; } = "";
    public int Button { get; set; } = -1;
    public string Output { get; set; } = "None";
    // Hold: key down while the control is held. Tap: one key press when it turns on (rotary selectors,
    // latched switches). Tap on/off: one press when it turns on and another when it turns off (toggle switches).
    public static readonly string[] Modes = ["Hold","Tap","Tap on/off"];
    public string Mode { get; set; } = "Hold";
}

public static class HeadPacket
{
    // OpenTrack UDP: six native little-endian doubles: X,Y,Z (cm), yaw,pitch,roll (degrees).
    public static double[]? Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 48) return null;
        var v = new double[6];
        for (int i=0;i<6;i++)
        {
            v[i]=BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes[(i*8)..]));
            if (!double.IsFinite(v[i]) || Math.Abs(v[i]) > (i<3 ? 200 : 360)) return null;
        }
        return v;
    }
}

// Protocol 2; the mod's protocol.lua documents the fields. Mask bit i = the mod drives Settings.Movements[i];
// bit 6 = the throttle value comes from the keyboard fallback (so the game's speed lock still applies).
public sealed record FlightFrame(long Sequence, bool Flight, bool Head, double[] Axes, double[] Pose, double TurnRate, int Mask)
{
    public string Serialize() => string.Join(" ", new double[]
    {
        2, Sequence, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000.0,
        Flight ? 1:0, Head ? 1:0,
        Axes[0],Axes[1],Axes[2],Axes[3],Axes[4],Axes[5],
        Pose[0],Pose[1],Pose[2],Pose[3],Pose[4],Pose[5],TurnRate,Mask
    }.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
}

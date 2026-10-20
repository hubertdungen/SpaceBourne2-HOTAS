using System.Runtime.InteropServices;

namespace SB2Hotas;

internal static class Sdl
{
    const string Dll="SDL2.dll";
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_Init(uint flags);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name,[MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_NumJoysticks();
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr SDL_JoystickOpen(int index);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_JoystickClose(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr SDL_JoystickName(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_JoystickInstanceID(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern ushort SDL_JoystickGetVendor(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern ushort SDL_JoystickGetProduct(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr SDL_JoystickGetSerial(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_JoystickNumAxes(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_JoystickNumButtons(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_JoystickNumHats(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern byte SDL_JoystickGetButton(IntPtr joystick,int button);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern byte SDL_JoystickGetHat(IntPtr joystick,int hat);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_JoystickGetAttached(IntPtr joystick);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern short SDL_JoystickGetAxis(IntPtr joystick,int axis);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_JoystickUpdate();
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_PumpEvents();
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_Quit();
    public static string Str(IntPtr p)=>Marshal.PtrToStringUTF8(p)??"";
}

internal sealed class Device : IDisposable
{
    static readonly string[] HatDirections={"Up","Right","Down","Left"};   // SDL_HAT_* bits 0..3
    readonly string baseKey;
    readonly int realButtons;
    public IntPtr Handle { get; }
    public string Name { get; }
    public string Key { get; private set; }
    public string Label { get; private set; }
    public int Count { get; }
    // Inputs bindable like buttons: real buttons, then four directions per hat, then each axis pushed past
    // AxisPress either way, so rotaries, sliders, toe brakes and mini-sticks can press game keys too.
    public int Buttons { get; }
    public int RealInputs { get; }   // buttons + hat directions, the ones the monitor lists as "pressed"
    const short AxisPress=12000;
    public bool Connected=>Sdl.SDL_JoystickGetAttached(Handle)!=0;
    public Device(int index)
    {
        Handle=Sdl.SDL_JoystickOpen(index);
        if(Handle==IntPtr.Zero) throw new IOException("Could not open a game controller.");
        Name=Sdl.Str(Sdl.SDL_JoystickName(Handle));
        Key=baseKey=$"{Sdl.SDL_JoystickGetVendor(Handle):X4}:{Sdl.SDL_JoystickGetProduct(Handle):X4}:{Name}:{Sdl.Str(Sdl.SDL_JoystickGetSerial(Handle))}";
        Label=Name;
        Count=Sdl.SDL_JoystickNumAxes(Handle);
        realButtons=Sdl.SDL_JoystickNumButtons(Handle);
        RealInputs=realButtons+4*Sdl.SDL_JoystickNumHats(Handle);
        Buttons=RealInputs+2*Count;
    }
    // Two identical controllers would share a key; the second one becomes "#2", and so on.
    public void Number(int n) { Key=$"{baseKey}#{n}"; Label=$"{Name} #{n}"; }
    public short Raw(int axis)=>Sdl.SDL_JoystickGetAxis(Handle,axis);
    public bool Pressed(int i)
    {
        if(i<realButtons) return Sdl.SDL_JoystickGetButton(Handle,i)!=0;
        if(i<RealInputs) return (Sdl.SDL_JoystickGetHat(Handle,(i-realButtons)/4) & (1<<((i-realButtons)%4)))!=0;
        if(i>=Buttons) return false;
        var raw=Raw((i-RealInputs)/2);
        return (i-RealInputs)%2==0 ? raw>AxisPress : raw< -AxisPress;
    }
    public string InputName(int i)=>i<realButtons ? $"Button {i+1}"
        : i<RealInputs ? $"Hat {(i-realButtons)/4+1} {HatDirections[(i-realButtons)%4]}"
        : $"Axis {(i-RealInputs)/2} {((i-RealInputs)%2==0 ? "+" : "-")}";
    public override string ToString()=>Label;
    public void Dispose()=>Sdl.SDL_JoystickClose(Handle);
}

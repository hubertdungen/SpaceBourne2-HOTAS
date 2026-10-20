using System.Runtime.InteropServices;

namespace SB2Hotas;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Two bridges would fight over state.txt and the OpenTrack UDP port.
        using var single=new Mutex(true,"SB2HOTAS-Bridge",out bool first);
        if(!first) { MessageBox.Show("SB2 HOTAS is already running.","SB2 HOTAS",MessageBoxButtons.OK,MessageBoxIcon.Information); return; }
        // SDL2.dll comes from the game: use the copy next to the bridge, or, before "Install mod" has made one,
        // the game's own, so a fresh download works straight away.
        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly,(name,_,_)=>
        {
            if(name!="SDL2.dll") return IntPtr.Zero;
            foreach(var dir in new[]{ AppContext.BaseDirectory, GameInstall.FindGame() is string game ? GameInstall.Win64(game) : null })
                if(dir!=null && NativeLibrary.TryLoad(Path.Combine(dir,"SDL2.dll"),out var handle)) return handle;
            return IntPtr.Zero;
        });
        ApplicationConfiguration.Initialize();
        try { using var form=new BridgeForm(); Application.Run(form); }
        catch(Exception e) { MessageBox.Show(e.Message,"SB2 HOTAS — error",MessageBoxButtons.OK,MessageBoxIcon.Error); }
    }
}

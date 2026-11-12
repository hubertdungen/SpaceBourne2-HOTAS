using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SB2Hotas;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Started by an update: the old bridge is still closing, so wait for it before taking its place.
        string? updatedFrom=null;
        if(args.Length>=3 && args[0]=="--after-update")
        {
            updatedFrom=args[2];
            try { if(int.TryParse(args[1],out var pid)) Process.GetProcessById(pid).WaitForExit(30000); }
            catch(ArgumentException) { }   // already gone
        }
        // Two bridges would fight over state.txt and the OpenTrack UDP port.
        using var single=new Mutex(true,"SB2HOTAS-Bridge",out bool first);
        if(!first) { MessageBox.Show("SB2 HOTAS is already running.","SB2 HOTAS",MessageBoxButtons.OK,MessageBoxIcon.Information); return; }
        Updater.CleanUp();
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
        try { using var form=new BridgeForm(updatedFrom); Application.Run(form); }
        catch(Exception e) { MessageBox.Show(e.Message,"SB2 HOTAS — error",MessageBoxButtons.OK,MessageBoxIcon.Error); }
    }
}

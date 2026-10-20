using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SB2Hotas;

// Finds SpaceBourne 2 and installs or removes UE4SS + the mod, the same way Install.ps1 does, so players only
// need the bridge. The game executable is never modified: UE4SS loads through dwmapi.dll next to it.
internal static class GameInstall
{
    const string ExePath=@"SB2\Binaries\Win64\SB2-Win64-Shipping.exe";
    const string Marker="SB2HOTAS-installed-UE4SS.txt";   // present only if we installed UE4SS
    static readonly Regex Entry=new(@"^\s*SB2HOTAS\s*:");
    // Release layout: <root>\bridge\SB2-HOTAS.exe next to <root>\ue4ss and <root>\mod\SB2HOTAS\scripts.
    public static readonly string PackageRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));

    public static bool IsGame(string folder)=>File.Exists(Path.Combine(folder,ExePath));
    public static string Win64(string game)=>Path.GetDirectoryName(Path.Combine(game,ExePath))!;
    // UE4SS 3.0.0 (bundled) keeps UE4SS.dll and Mods next to the exe; later 3.x releases move them into "ue4ss\".
    static string? ExistingMods(string win64)=>
        File.Exists(Path.Combine(win64,"ue4ss","UE4SS.dll")) ? Path.Combine(win64,"ue4ss","Mods")
        : File.Exists(Path.Combine(win64,"UE4SS.dll")) ? Path.Combine(win64,"Mods") : null;

    // Steam's own install plus every library listed in its libraryfolders.vdf.
    public static string? FindGame()
    {
        try
        {
            if(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null) is not string steam) return null;
            var libs=new List<string> { steam.Replace('/','\\') };
            var vdf=Path.Combine(libs[0],"steamapps","libraryfolders.vdf");
            if(File.Exists(vdf))
                foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace(@"\\",@"\"));
            return libs.Select(l=>Path.Combine(l,"steamapps","common","SpaceBourne 2")).FirstOrDefault(IsGame);
        }
        catch(Exception) { return null; }
    }

    public static void Install(string game,string packageRoot,string bridgeDir)
    {
        var win64=Win64(game);
        var ue4ss=Path.Combine(packageRoot,"ue4ss");
        var scripts=Path.Combine(packageRoot,"mod","SB2HOTAS","scripts");
        if(!Directory.Exists(scripts)) throw new IOException($"Mod files not found in {scripts}. Keep the bridge inside the extracted SB2 HOTAS folder.");
        string mods;
        try
        {
            // Reuse an existing UE4SS (the player may have other mods); otherwise install ours with its console off.
            if(ExistingMods(win64) is string existing) mods=existing;
            else
            {
                if(File.Exists(Path.Combine(win64,"dwmapi.dll")))
                    throw new IOException("The game folder already has a dwmapi.dll from another mod loader. Remove it, or install UE4SS yourself and run Install mod again.");
                if(!File.Exists(Path.Combine(ue4ss,"UE4SS.dll"))) throw new IOException($"UE4SS not found in {ue4ss}.");
                foreach(var f in new[]{"dwmapi.dll","UE4SS.dll"}) File.Copy(Path.Combine(ue4ss,f),Path.Combine(win64,f),true);
                File.WriteAllLines(Path.Combine(win64,"UE4SS-settings.ini"),File.ReadAllLines(Path.Combine(ue4ss,"UE4SS-settings.ini"))
                    .Select(l=>Regex.IsMatch(l,@"^GuiConsoleEnabled\s*=") ? "GuiConsoleEnabled = 0" : l));
                File.WriteAllText(Path.Combine(win64,Marker),"UE4SS was installed by SB2 HOTAS; its Uninstall removes it.");
                mods=Path.Combine(win64,"Mods");
            }
            var mod=Path.Combine(mods,"SB2HOTAS","scripts");
            Directory.CreateDirectory(mod);
            foreach(var f in Directory.GetFiles(scripts,"*.lua")) File.Copy(f,Path.Combine(mod,Path.GetFileName(f)),true);
            // Enable the mod (re-enabling a "SB2HOTAS : 0"), keeping UE4SS's built-in Keybinds entry last as it requires.
            var modsTxt=Path.Combine(mods,"mods.txt");
            var lines=File.Exists(modsTxt) ? File.ReadAllLines(modsTxt).ToList() : [];
            var at=lines.FindIndex(Entry.IsMatch);
            if(at>=0) lines[at]="SB2HOTAS : 1";
            else
            {
                var keybinds=lines.FindIndex(l=>Regex.IsMatch(l,@"^\s*Keybinds\s*:"));
                lines.Insert(keybinds>=0 ? keybinds : lines.Count,"SB2HOTAS : 1");
            }
            File.WriteAllLines(modsTxt,lines);
        }
        catch(UnauthorizedAccessException) { throw new IOException("Windows refused to write to the game folder. Run the bridge as administrator once to install."); }
        // The bridge reads controllers through the SDL2.dll that ships with the game.
        var sdl=Path.Combine(bridgeDir,"SDL2.dll"); var gameSdl=Path.Combine(win64,"SDL2.dll");
        try { if(!File.Exists(sdl) && File.Exists(gameSdl)) File.Copy(gameSdl,sdl); }
        catch(UnauthorizedAccessException) { throw new IOException($"The mod is installed, but Windows refused to copy SDL2.dll into {bridgeDir}. Move the SB2 HOTAS folder somewhere you can write to (e.g. Documents)."); }
    }

    public static string Uninstall(string game)
    {
        var win64=Win64(game);
        try
        {
            foreach(var mods in new[]{Path.Combine(win64,"Mods"),Path.Combine(win64,"ue4ss","Mods")})
            {
                if(Directory.Exists(Path.Combine(mods,"SB2HOTAS"))) Directory.Delete(Path.Combine(mods,"SB2HOTAS"),true);
                var modsTxt=Path.Combine(mods,"mods.txt");
                if(File.Exists(modsTxt)) File.WriteAllLines(modsTxt,File.ReadAllLines(modsTxt).Where(l=>!Entry.IsMatch(l)));
            }
            if(!File.Exists(Path.Combine(win64,Marker))) return $"Mod removed from {game}. UE4SS was already there before, so it stays.";
            var ourMods=Path.Combine(win64,"Mods");
            if(Directory.Exists(ourMods) && Directory.GetDirectories(ourMods).Length>0) return $"Mod removed from {game}. Other UE4SS mods are installed, so UE4SS stays.";
            foreach(var f in new[]{"dwmapi.dll","UE4SS.dll","UE4SS-settings.ini","UE4SS.log",Marker})
                File.Delete(Path.Combine(win64,f));
            if(Directory.Exists(ourMods)) Directory.Delete(ourMods,true);
            return $"Mod and UE4SS removed from {game}. The game is back to how it was.";
        }
        catch(UnauthorizedAccessException) { throw new IOException("Windows refused to write to the game folder. Run the bridge as administrator once to uninstall."); }
    }
}

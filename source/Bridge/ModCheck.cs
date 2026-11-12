using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SB2Hotas;

// What the game folder has of the in-game mod, for the version line at the bottom of the bridge's window.
internal enum ModStatus { NoGame, Missing, Off, Older, Unknown, Current, Newer }

internal sealed record ModInfo(ModStatus Status,string Text,string? Game);

internal static class ModCheck
{
    // Since 0.3.6 main.lua says its version (the realism installer reads the same line). Older releases are
    // recognised by their Lua files; releases whose in-game files are identical share one entry.
    static readonly Regex VersionLine=new(@"VERSION\s*=\s*""([^""]+)""");
    static readonly Dictionary<string,(string Label,string Version)> Known=new()
    {
        ["e4b3baf3ac82a4e9cbf18e84bb32f938cab22fbadb46eb17c3dbffcc13dd59a2"]=("0.1.0","0.1.0"),
        ["726ca0b7ef1ecc46beadf4f9f8dacfaa6c76e2e83e5b79fcb662190b9147e595"]=("0.2.0","0.2.0"),
        ["28d81369233978fef39dabacaa3b1012167a90a32989208b77ee37a0b5e7fd30"]=("0.2.1","0.2.1"),
        ["ee4de0a8fefdd4bb25b9e27efb3c2cbf60662217aba51976537943f8bdd2f856"]=("0.2.2–0.3.0","0.3.0"),
        ["ec5726f94f62fdbcbf86ba58e67690520e9fa46236fb73f6f338a823d69249c0"]=("0.3.1","0.3.1"),
        ["3ce60540374f4d91e188d1a6a4f13ce1eab73c026c753d9c213084dc2f99c960"]=("0.3.2–0.3.4","0.3.4"),
        ["4285f4195646d4b8510fe97d86686dab1bb6f4fed95013068ed4bfa72b28681a"]=("0.3.5","0.3.5"),
    };
    static readonly Regex Entry=new(@"^\s*SB2HOTAS\s*:\s*(\d)",RegexOptions.Multiline);

    public static ModInfo Check(string? game,Version bridge)
    {
        if(game==null || !GameInstall.IsGame(game)) return new(ModStatus.NoGame,"SpaceBourne 2 not found",null);
        var mods=GameInstall.ExistingMods(GameInstall.Win64(game));
        var scripts=mods==null ? null : Path.Combine(mods,"SB2HOTAS","scripts");
        if(scripts==null || !File.Exists(Path.Combine(scripts,"main.lua"))) return new(ModStatus.Missing,"not installed",game);
        var (label,version)=Identify(scripts);
        if(!Enabled(mods!)) return new(version!=null && version.CompareTo(bridge)>0 ? ModStatus.Newer : ModStatus.Off,$"{label}, switched off in mods.txt",game);
        if(version==null) return new(ModStatus.Unknown,label,game);
        int c=version.CompareTo(bridge);
        return c<0 ? new(ModStatus.Older,$"{label} (older than this bridge)",game)
            : c>0 ? new(ModStatus.Newer,$"{label} (newer than this bridge: update the bridge)",game)
            : new(ModStatus.Current,$"{label} ✓",game);
    }
    // UE4SS 3.0 loads a mod when mods.txt says 1, or when mods.txt doesn't list it and it has enabled.txt.
    static bool Enabled(string mods)
    {
        var modsTxt=Path.Combine(mods,"mods.txt");
        var m=File.Exists(modsTxt) ? Entry.Match(File.ReadAllText(modsTxt)) : Match.Empty;
        return m.Success ? m.Groups[1].Value=="1" : File.Exists(Path.Combine(mods,"SB2HOTAS","enabled.txt"));
    }
    static (string Label,Version? Version) Identify(string scripts)
    {
        var said=VersionLine.Match(File.ReadAllText(Path.Combine(scripts,"main.lua")));
        if(said.Success) return (said.Groups[1].Value,Version.TryParse(said.Groups[1].Value,out var v) ? v : null);
        return Known.TryGetValue(Fingerprint(scripts),out var k) ? (k.Label,Version.Parse(k.Version)) : ("an unknown version",null);
    }
    // SHA-256 of every Lua file (by name, line endings as LF), so a copy with Windows line endings matches too.
    public static string Fingerprint(string scripts)
    {
        var text=new StringBuilder();
        foreach(var f in Directory.GetFiles(scripts,"*.lua").OrderBy(Path.GetFileName,StringComparer.Ordinal))
            text.Append(Path.GetFileName(f)).Append('\n').Append(File.ReadAllText(f,Encoding.UTF8).Replace("\r\n","\n")).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }
}

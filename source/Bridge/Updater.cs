using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace SB2Hotas;

// The bridge updates itself from the GitHub releases. The latest version comes from github.com's own redirect of
// /releases/latest (the API allows only 60 requests an hour). The release zip is checked (its bridge must be that
// version) before anything changes, every replaced file is backed up first, and a failure puts them all back.
// Controller setups live in %LOCALAPPDATA%\SB2HOTAS and are never touched.
internal static class Updater
{
    const string Home="https://github.com/hubertdungen/SpaceBourne2-HOTAS";
    const string ExeName="SB2-HOTAS.exe";
    // Test aid: SB2HOTAS_UPDATE_BASE points the updater at a local stand-in for GitHub. Only http on this PC is accepted.
    static string Base
    {
        get
        {
            var test=Environment.GetEnvironmentVariable("SB2HOTAS_UPDATE_BASE");
            return test!=null && Uri.TryCreate(test,UriKind.Absolute,out var u) && u.Scheme==Uri.UriSchemeHttp && u.IsLoopback ? test.TrimEnd('/') : Home;
        }
    }
    public static string ReleasesPage=>Base+"/releases/latest";
    public static readonly string BackupDir=Data.PathOf("backups");
    public static Version Current=>Version.TryParse(Application.ProductVersion.Split('+')[0],out var v) ? v : new Version(0,0,0);

    // The package this bridge runs from: <root>\bridge\SB2-HOTAS.exe with <root>\mod\SB2HOTAS next to it.
    // Null for a developer build or a lone exe, which can't update itself.
    public static string? PackageRoot()
    {
        var exe=Environment.ProcessPath;
        if(exe==null || !string.Equals(Path.GetFileName(exe),ExeName,StringComparison.OrdinalIgnoreCase)) return null;
        var bridge=Path.GetDirectoryName(exe)!;
        if(!string.Equals(Path.GetFileName(bridge),"bridge",StringComparison.OrdinalIgnoreCase)) return null;
        var root=Path.GetDirectoryName(bridge)!;
        return Directory.Exists(Path.Combine(root,"mod","SB2HOTAS")) ? root : null;
    }
    // The SpaceBourne 2 Realism installer keeps its own copy and updates it with "Update all", so that copy is left to it.
    public static bool InstallerCopy(string root)
    {
        var mine=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SB2Realism")+Path.DirectorySeparatorChar;
        return (Path.GetFullPath(root)+Path.DirectorySeparatorChar).StartsWith(mine,StringComparison.OrdinalIgnoreCase);
    }
    static HttpClient Client(bool redirects,TimeSpan timeout)
    {
        var http=new HttpClient(new HttpClientHandler { AllowAutoRedirect=redirects }) { Timeout=timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SB2-HOTAS/"+Application.ProductVersion);
        return http;
    }
    static string Text(Version v)=>$"{v.Major}.{v.Minor}.{Math.Max(0,v.Build)}";

    // The latest release's version, or null when the answer isn't a release. One retry: GitHub sometimes answers
    // 502 or 504 for a moment.
    public static async Task<Version?> Latest()
    {
        using var http=Client(false,TimeSpan.FromSeconds(15));
        for(int attempt=0;;attempt++)
        {
            using var request=new HttpRequestMessage(HttpMethod.Head,Base+"/releases/latest");
            using var response=await http.SendAsync(request);
            int code=(int)response.StatusCode;
            if((code==502 || code==504) && attempt==0) { await Task.Delay(3000); continue; }
            var m=Regex.Match(response.Headers.Location?.ToString()??"",@"/releases/tag/v?(\d+\.\d+\.\d+)$");
            return m.Success ? Version.Parse(m.Groups[1].Value) : null;
        }
    }
    // Downloads the release zip into a new temporary folder and unpacks it. Returns that folder (delete it when done)
    // and the package inside, once its bridge has been checked to be that version.
    public static async Task<(string Temp,string Package)> Download(Version version,IProgress<int> progress,CancellationToken cancel)
    {
        var v=Text(version);
        var temp=Path.Combine(Path.GetTempPath(),"SB2HOTAS-update-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var zip=Path.Combine(temp,"update.zip");
            using(var http=Client(true,TimeSpan.FromMinutes(10)))
            using(var response=await http.GetAsync($"{Base}/releases/download/v{v}/SB2-HOTAS-{v}.zip",HttpCompletionOption.ResponseHeadersRead,cancel))
            {
                response.EnsureSuccessStatusCode();
                long total=response.Content.Headers.ContentLength??0, done=0;
                await using var source=await response.Content.ReadAsStreamAsync(cancel);
                await using var file=File.Create(zip);
                var buffer=new byte[1<<16];
                int read, shown=-1;
                while((read=await source.ReadAsync(buffer,cancel))>0)
                {
                    await file.WriteAsync(buffer.AsMemory(0,read),cancel);
                    done+=read;
                    int percent=total>0 ? (int)(done*100/total) : 0;
                    if(percent!=shown) { shown=percent; progress.Report(percent); }
                }
            }
            var unpacked=Path.Combine(temp,"x");
            await Task.Run(()=>ZipFile.ExtractToDirectory(zip,unpacked),cancel);
            var exe=Directory.GetFiles(unpacked,ExeName,SearchOption.AllDirectories)
                .FirstOrDefault(p=>string.Equals(Path.GetFileName(Path.GetDirectoryName(p)),"bridge",StringComparison.OrdinalIgnoreCase))
                ?? throw new IOException($@"The download has no bridge\{ExeName}. Nothing was changed.");
            var package=Path.GetDirectoryName(Path.GetDirectoryName(exe))!;
            var inside=FileVersionInfo.GetVersionInfo(exe).ProductVersion?.Split('+')[0];
            if(inside==null || !Version.TryParse(inside,out var found) || Text(found)!=v)
                throw new IOException($"The download is not SB2 HOTAS {v} (its bridge says {inside??"nothing"}). Nothing was changed.");
            if(!File.Exists(Path.Combine(package,"mod","SB2HOTAS","scripts","main.lua")))
                throw new IOException("The download has no in-game mod. Nothing was changed.");
            return (temp,package);
        }
        catch { TryDelete(temp); throw; }
    }
    // Puts the new package over this one and returns the backup folder. The running exe can't be overwritten but can
    // be renamed, so it moves aside to SB2-HOTAS.exe.old (deleted at the next start) and the new one takes its name.
    public static string Apply(string root,string package,string oldVersion)
    {
        var files=Directory.GetFiles(package,"*",SearchOption.AllDirectories).Select(f=>Path.GetRelativePath(package,f)).ToList();
        var backup=Path.Combine(BackupDir,$"{oldVersion}-{DateTime.Now:yyyyMMdd-HHmmss}");
        var exe=Path.Combine(root,"bridge",ExeName);
        var aside=exe+".old";
        var replaced=new List<string>();
        var created=new List<string>();
        bool moved=false;
        try
        {
            foreach(var rel in files)   // the whole backup first, before anything changes
            {
                var dest=Path.Combine(root,rel);
                if(!File.Exists(dest)) continue;
                var copy=Path.Combine(backup,rel);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(dest,copy,true);
            }
            if(File.Exists(aside)) File.Delete(aside);
            File.Move(exe,aside);
            moved=true;
            foreach(var rel in files)
            {
                var dest=Path.Combine(root,rel);
                bool existed=File.Exists(dest);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(Path.Combine(package,rel),dest,true);
                (existed ? replaced : created).Add(rel);
            }
        }
        catch
        {
            foreach(var rel in created) { try { File.Delete(Path.Combine(root,rel)); } catch { } }
            foreach(var rel in replaced) { try { File.Copy(Path.Combine(backup,rel),Path.Combine(root,rel),true); } catch { } }
            if(moved) { try { if(File.Exists(exe)) File.Delete(exe); File.Move(aside,exe); } catch { } }
            if(!moved) TryDelete(backup);   // nothing had changed yet
            throw;
        }
        // Keep the three newest backups.
        foreach(var old in new DirectoryInfo(BackupDir).GetDirectories().OrderByDescending(d=>d.CreationTimeUtc).Skip(3)) TryDelete(old.FullName);
        return backup;
    }
    // Starts the new bridge; it waits for this one to close (they would fight over the same files and port).
    public static void Restart(string root,string oldVersion)
    {
        var start=new ProcessStartInfo(Path.Combine(root,"bridge",ExeName)) { UseShellExecute=false, WorkingDirectory=Path.Combine(root,"bridge") };
        start.ArgumentList.Add("--after-update"); start.ArgumentList.Add(Environment.ProcessId.ToString()); start.ArgumentList.Add(oldVersion);
        Process.Start(start);
    }
    // At start, once no other bridge runs: the exe an update moved aside.
    public static void CleanUp()
    {
        var root=PackageRoot();
        if(root!=null) { try { File.Delete(Path.Combine(root,"bridge",ExeName+".old")); } catch { } }
    }
    public static void TryDelete(string dir) { try { if(Directory.Exists(dir)) Directory.Delete(dir,true); } catch { } }
}

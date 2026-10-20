# Builds the release package release\SB2-HOTAS-<version>\ and zips it to release\SB2-HOTAS-<version>.zip.
# The version comes from <Version> in source\Bridge\Bridge.csproj. Developer scripts and work\ stay out.
param([string]$UE4SS = "$PSScriptRoot\..\..\work\tools\ue4ss")   # folder with dwmapi.dll, UE4SS.dll, UE4SS-settings.ini
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csproj = Join-Path $root 'source\Bridge\Bridge.csproj'
$version = (Select-Xml -Path $csproj -XPath '//Version').Node.InnerText
if (-not $version) { throw "No <Version> in $csproj." }
$out = Join-Path $root "release\SB2-HOTAS-$version"
$zip = "$out.zip"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "1/3 Publishing the bridge $version..."
# Self-contained single file: no .NET install needed, on Windows or inside the game's Proton prefix on Linux.
dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$out\bridge" --nologo -v q
if ($LASTEXITCODE) { throw "Bridge build failed." }
if (-not (Test-Path "$out\bridge\templates\*.json")) { throw "No bridge\templates\*.json in the publish output (they must not be bundled into the exe)." }
# SDL2.dll is never shipped: Install.ps1 copies the game's own copy. The .pdb is for developers only.
Remove-Item "$out\bridge\SDL2.dll", "$out\bridge\*.pdb" -ErrorAction SilentlyContinue

Write-Host "2/3 Copying the mod, UE4SS and documents..."
$scripts = New-Item -ItemType Directory -Force "$out\mod\SB2HOTAS\scripts"
Copy-Item "$root\mod\SB2HOTAS\scripts\*.lua" $scripts
$ue4ssOut = New-Item -ItemType Directory -Force "$out\ue4ss"
foreach ($f in 'dwmapi.dll', 'UE4SS.dll', 'UE4SS-settings.ini') { Copy-Item (Join-Path $UE4SS $f) $ue4ssOut }
Copy-Item "$root\Install.ps1", "$root\LICENSE", "$root\THIRD-PARTY-NOTICES.txt" $out
# Linux/Proton scripts: always LF, or bash fails on the stray carriage returns.
foreach ($f in 'install.sh', 'run-bridge.sh') {
    $text = [IO.File]::ReadAllText((Join-Path $root $f)) -replace "`r`n", "`n"
    [IO.File]::WriteAllText((Join-Path $out $f), $text, (New-Object Text.UTF8Encoding $false))
}
# Players get the README without the Developers sections (those scripts are not in the package).
$readme = (Get-Content "$root\README.md" -Raw -Encoding UTF8) -replace '(?s)\r?\n## Developers.*$', ''
[IO.File]::WriteAllText("$out\README.md", $readme.TrimEnd() + "`r`n")

Write-Host "3/3 Zipping..."
# Windows' own tar writes standard zip paths (/); ZipFile in Windows PowerShell writes backslashes.
# It is fed an mtree list rather than the folder so the Unix modes are explicit: on Windows tar
# would store the .sh scripts as non-executable, and "./install.sh" would fail after unzipping on Linux.
function MtreeName($p) {   # mtree escapes spaces and other special bytes as \ooo
    -join ([Text.Encoding]::UTF8.GetBytes($p) | ForEach-Object {
        if ($_ -gt 32 -and $_ -lt 127 -and $_ -ne 35 -and $_ -ne 92) { [char]$_ } else { '\' + [Convert]::ToString($_, 8).PadLeft(3, '0') } })
}
$leaf = Split-Path $out -Leaf
$mtree = @('#mtree') + (@(Get-Item $out) + @(Get-ChildItem $out -Recurse) | ForEach-Object {
    $name = MtreeName ($leaf + $_.FullName.Substring($out.Length).Replace('\', '/'))
    $time = [DateTimeOffset]::new($_.LastWriteTimeUtc).ToUnixTimeSeconds()
    if ($_.PSIsContainer) { "$name type=dir mode=0755 time=$time.0" }
    else {
        $mode = if ($_.Extension -in '.sh', '.exe') { '0755' } else { '0644' }
        "$name type=file mode=$mode size=$($_.Length) time=$time.0"
    }
})
$list = [IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllText($list, ($mtree -join "`n") + "`n")
    & "$env:SystemRoot\System32\tar.exe" -a -cf $zip -C (Split-Path $out) "@$list"
    if ($LASTEXITCODE) { throw "Zipping failed." }
} finally { Remove-Item $list }
Write-Host "`nDone: $zip" -ForegroundColor Green

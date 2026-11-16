# Builds the COMPLETE Celestellaris mod zip for players (code + maps + mountain + audio + graphics).
# The project's own Release zip omits Maps/Mountain/Overworld/GameBoyCarts (fine for dev, not for players).
# Usage: .\make-release.ps1 [-ModRoot <path to Mods\CEL>] [-Out release\payload\Celestellaris.zip]
param(
  [string]$ModRoot = "C:\Users\Owner\Desktop\Celeste\celeste\Mods\CEL",
  [string]$Out = "$PSScriptRoot\release\payload\Celestellaris.zip"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$root = (Resolve-Path $ModRoot).Path.TrimEnd('\')

# Players must not receive backups, design scratch files or editor projects.
$skip = '\.bak[^\\]*$|\.design\.json$|\.ldtk$|\.pre-.*$|~$|\.tmp$'

$files = [System.Collections.Generic.List[string]]::new()
foreach ($f in 'everest.yaml','CollabUtils2CollabID.txt') { if (Test-Path "$root\$f") { $files.Add("$root\$f") } }
foreach ($d in 'bin','Assets','Audio','Dialog','Graphics','Ahorn','Loenn','Maps','Mountain','Overworld','GameBoyCarts') {
  if (Test-Path "$root\$d") { Get-ChildItem "$root\$d" -Recurse -File | Where-Object { $_.FullName -notmatch $skip } | ForEach-Object { $files.Add($_.FullName) } }
}
if (Test-Path "$root\Rysy\bin") { Get-ChildItem "$root\Rysy\bin" -File | ForEach-Object { $files.Add($_.FullName) } }

New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
if (Test-Path $Out) { Remove-Item $Out -Force }
$zip = [IO.Compression.ZipFile]::Open($Out, 'Create')
try {
  $i = 0
  foreach ($f in $files) {
    $name = $f.Substring($root.Length + 1).Replace('\', '/')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f, $name, [IO.Compression.CompressionLevel]::Fastest)
    if ((++$i % 5000) -eq 0) { Write-Host "$i / $($files.Count)" }
  }
} finally { $zip.Dispose() }
$h = (Get-FileHash $Out -Algorithm SHA256).Hash.ToLower()
"{0}  {1}  ({2:N0} MB, {3} files)" -f $h, (Split-Path $Out -Leaf), ((Get-Item $Out).Length/1MB), $files.Count

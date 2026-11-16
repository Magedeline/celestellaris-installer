# Sign the installer after `dotnet publish`. Needs a code-signing certificate (see TRUST.md) and signtool
# (Windows SDK). Usage:  .\sign.ps1 -Thumbprint <cert thumbprint>
param([Parameter(Mandatory)][string]$Thumbprint, [string]$Exe = "publish\Install-Celestellaris.exe")
$signtool = (Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe | Where-Object FullName -match 'x64' | Select-Object -Last 1).FullName
if (-not $signtool) { throw "signtool.exe not found; install the Windows SDK." }
& $signtool sign /sha1 $Thumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $Exe
& $signtool verify /pa /v $Exe

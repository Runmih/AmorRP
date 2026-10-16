param([string]$Destination = "$PSScriptRoot/../.tools/dalamud")
$ErrorActionPreference = 'Stop'
$reference = Get-Content "$PSScriptRoot/dalamud-reference.json" -Raw | ConvertFrom-Json
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$archive = "$destinationPath.zip"
New-Item -ItemType Directory -Force -Path (Split-Path $destinationPath) | Out-Null
Invoke-WebRequest -Uri $reference.url -OutFile $archive
if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $reference.sha256) {
    Remove-Item $archive
    throw 'Dalamud reference checksum mismatch; refusing to extract.'
}
if (Test-Path $destinationPath) { Remove-Item $destinationPath -Recurse -Force }
Expand-Archive $archive -DestinationPath $destinationPath
Remove-Item $archive
$version = [System.Reflection.AssemblyName]::GetAssemblyName("$destinationPath/Dalamud.dll").Version.ToString()
if ($version -ne $reference.version) { throw "Unexpected Dalamud assembly version: $version" }
Write-Host "Verified Dalamud $version at $destinationPath"
Write-Host "Set DALAMUD_HOME to this directory before restoring/building the plugin."

<#
.SYNOPSIS
    Builds RaidPad against a local SPT installation, optionally installs it and
    packages a release archive.

.PARAMETER SptGamePath
    Root folder of the SPT installation (the one containing EscapeFromTarkov.exe).

.PARAMETER Package
    Also write build\RaidPad-<version>.zip with the release layout
    (BepInEx\plugins\RaidPad\...).

.PARAMETER SkipDeploy
    Only build, do not copy anything into the game folder.
#>
[CmdletBinding()]
param(
    [string]$SptGamePath = 'D:\Games\SPT 4.1.6 essential',
    [string]$Configuration = 'Release',
    [switch]$Package,
    [switch]$SkipDeploy
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$proj = Join-Path $repo 'RaidPad\RaidPad.csproj'

if (-not (Test-Path (Join-Path $SptGamePath 'EscapeFromTarkov.exe'))) {
    throw "No SPT installation found at '$SptGamePath' (EscapeFromTarkov.exe missing)."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK not found. This is an SDK-style project: install the .NET SDK (8.0 or newer) and the .NET Framework 4.7.2 targeting pack from the VS Build Tools.'
}

Write-Host "SPT path : $SptGamePath"
Write-Host "project  : $proj"

& dotnet build $proj -c $Configuration "/p:SPTGamePath=$SptGamePath" -v minimal --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

$out = Join-Path $repo "RaidPad\bin\$Configuration"
$version = ([regex]::Match((Get-Content $proj -Raw), '<ModVersion>([^<]+)</ModVersion>')).Groups[1].Value
if (-not $version) { throw 'ModVersion not found in the project file.' }
Write-Host "built version $version -> $out"

$stage = Join-Path $repo "build\RaidPad\BepInEx\plugins\RaidPad"
if ($Package) {
    if (Test-Path (Join-Path $repo 'build')) { Remove-Item (Join-Path $repo 'build') -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $stage, (Join-Path $stage 'assets') | Out-Null
    Copy-Item (Join-Path $out '*.dll') $stage -Force
    Copy-Item (Join-Path $repo 'RaidPad\assets\*') (Join-Path $stage 'assets') -Recurse -Force
    $zip = Join-Path $repo "build\RaidPad-$version.zip"
    Compress-Archive -Path (Join-Path $repo 'build\RaidPad\*') -DestinationPath $zip -Force
    Write-Host "package  : $zip"
}

if ($SkipDeploy) { Write-Host 'Built, deployment skipped.'; return }
if (Get-Process -Name 'EscapeFromTarkov' -ErrorAction SilentlyContinue) {
    Write-Warning 'EscapeFromTarkov is running - deployment skipped. Close the game and re-run.'
    return
}

$mod = Join-Path $SptGamePath 'BepInEx\plugins\RaidPad'
New-Item -ItemType Directory -Force -Path $mod, (Join-Path $mod 'assets') | Out-Null
Copy-Item (Join-Path $out '*.dll') $mod -Force
Copy-Item (Join-Path $repo 'RaidPad\assets\*') (Join-Path $mod 'assets') -Recurse -Force

Write-Host ''
Write-Host "Installed to: $mod"
Get-ChildItem $mod -File | Select-Object Name,Length | Format-Table -AutoSize
Write-Host ('asset files: ' + (Get-ChildItem (Join-Path $mod 'assets') -Recurse -File | Measure-Object).Count)

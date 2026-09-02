<#
.SYNOPSIS
    Builds the Quasimorph Nemesis mod and assembles the release folder.

.DESCRIPTION
    Compiles the C# mod against the game's own assemblies, stages the release
    folder, and optionally installs it straight into LocalUserPresets for testing.

.PARAMETER GameManaged
    Path to <game>\Quasimorph_Data\Managed. The mod compiles against the game's
    own assemblies, so point this at the version you are targeting.

.PARAMETER OutDir
    Where to put the release folder.

.PARAMETER Install
    Also copy the built mod into LocalUserPresets so the next launch picks it up.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Install
#>
[CmdletBinding()]
param(
    [string]$GameManaged = "C:\Users\Administrator\Desktop\Quasimorph.v1.0.3\game\Quasimorph_Data\Managed",
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "Quasimorph_Nemesis_v0.1"),
    [string]$PresetsDir = "$env:USERPROFILE\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets",
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$Project = $PSScriptRoot

if (-not (Test-Path $GameManaged)) {
    throw "Game assemblies not found: $GameManaged`nPass -GameManaged <path to Quasimorph_Data\Managed>."
}

# The .NET SDK lives in the user profile, so it is not on PATH by default.
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet not found. Install the .NET 8 SDK: https://dot.net"
}

$stage = Join-Path $Project "build\mod"
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Write-Host "[1/3] Compiling the mod" -ForegroundColor Cyan
dotnet build "$Project\mod_src\QuasimorphNemesis\QuasimorphNemesis.csproj" `
    -c Release -o $stage --nologo -v q -p:GameManaged=$GameManaged |
    Select-String -Pattern "error|warning|Build succeeded"
if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }

Write-Host "[2/3] Staging the release folder" -ForegroundColor Cyan
Remove-Item $OutDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$OutDir\mod" | Out-Null
Copy-Item -Path "$Project\mod_src\QuasimorphNemesis\modmanifest.json" -Destination "$OutDir\mod" -Force
Copy-Item -Path "$stage\QuasimorphNemesis.dll" -Destination "$OutDir\mod" -Force
if (Test-Path "$Project\README.md") {
    Copy-Item -Path "$Project\README.md" -Destination "$OutDir\mod\README.txt" -Force
}

Write-Host "[3/3] Verifying every game reference still resolves" -ForegroundColor Cyan
# A sibling mod shipped a call to a SpawnItem overload the game had already dropped.
# Resolving our references against the real assemblies each build makes that whole
# class of bug impossible to ship.
$checker = Join-Path $Project "tools\apicheck.py"
if (Test-Path $checker) {
    python $checker "$stage\QuasimorphNemesis.dll" $GameManaged
    if ($LASTEXITCODE -ne 0) { throw "Unresolved game references - see above." }
} else {
    Write-Host "  (tools\apicheck.py not present - skipping)" -ForegroundColor DarkYellow
}

if ($Install) {
    $target = Join-Path $PresetsDir "QuasimorphNemesis"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path "$OutDir\mod\*" -Destination $target -Force
    Write-Host "Installed to $target" -ForegroundColor Green
}

Write-Host "`nDone. Release folder: $OutDir" -ForegroundColor Green
Get-ChildItem "$OutDir" -Recurse -File |
    Select-Object @{n = 'File'; e = { $_.FullName.Replace($OutDir, '') } }, Length |
    Format-Table -AutoSize

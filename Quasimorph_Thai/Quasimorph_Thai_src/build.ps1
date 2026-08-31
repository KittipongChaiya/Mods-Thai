<#
.SYNOPSIS
    Builds the Quasimorph Thai mod and assembles the release folder + zip.

.DESCRIPTION
    Compiles the C# mod, packs translations/*.json into the shipped override
    table, stages everything into a release folder, and zips it.

    Run this after translating more strings, or after a new game version.

.PARAMETER GameManaged
    Path to <game>\Quasimorph_Data\Managed. The mod compiles against the game's
    own assemblies, so point this at the version you are targeting.

.PARAMETER OutDir
    Where to put the release folder. A .zip is written next to it.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -GameManaged "D:\Quasimorph\Quasimorph_Data\Managed"
#>
[CmdletBinding()]
param(
    [string]$GameManaged = "C:\Users\Administrator\Desktop\Quasimorph.v1.0.3\game\Quasimorph_Data\Managed",
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "Quasimorph_Thai_v1.4"),
    [switch]$SkipZip
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

Write-Host "[1/5] Validating translations" -ForegroundColor Cyan
python "$Project\tools\check_translations.py"
if ($LASTEXITCODE -ne 0) { throw "Translation validation failed - fix the problems above before building." }

# A character no shipped font can draw renders as nothing at all, with no error
# in the log - so it has to be caught here rather than found by a player.
python "$Project\tools\check_font.py"
if ($LASTEXITCODE -ne 0) { throw "Unrenderable character(s) found - see above." }

# One Thai rendering per English string, unless consistency_allow.json says the
# divergence was reviewed and is deliberate (hit = ตะปบ / ต่อย / ทุบ, and so on).
python "$Project\tools\consistency.py" --strict --limit 10
if ($LASTEXITCODE -ne 0) { throw "Unreviewed translation divergence - see above." }

Write-Host "[2/5] Packing translations into the override table" -ForegroundColor Cyan
python "$Project\tools\build_overrides.py" "$stage\thai_overrides.tsv" --gzip
if ($LASTEXITCODE -ne 0) { throw "Building the override table failed." }
# Only the compressed copy ships; the plain one is a build artefact.
Remove-Item "$stage\thai_overrides.tsv" -ErrorAction SilentlyContinue

Write-Host "[3/5] Compiling the mod" -ForegroundColor Cyan
dotnet build "$Project\mod_src\QuasimorphThai\QuasimorphThai.csproj" `
    -c Release -o $stage --nologo -v q -p:GameManaged=$GameManaged |
    Select-String -Pattern "error|Build succeeded"
if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }

Write-Host "[4/5] Staging the release folder" -ForegroundColor Cyan
Remove-Item $OutDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$OutDir\mod" | Out-Null
# The installer and readme have Thai filenames. PowerShell 5.1 reads a BOM-less .ps1
# as ANSI, which would mangle those names if they were written as literals here, so
# pick them up by extension instead.
Get-ChildItem -Path $Project -File |
    Where-Object { $_.Extension -in '.py', '.txt' } |
    Copy-Item -Destination $OutDir -Force
Copy-Item -Path "$Project\mod_src\QuasimorphThai\modmanifest.json" -Destination "$OutDir\mod" -Force
Copy-Item -Path "$Project\assets\quasimorph_tahoma_tmp.bundle" -Destination "$OutDir\mod" -Force
Copy-Item -Path "$stage\QuasimorphThai.dll", "$stage\thai_overrides.tsv.gz" -Destination "$OutDir\mod" -Force

Write-Host "[5/5] Packaging" -ForegroundColor Cyan
if (-not $SkipZip) {
    $zip = "$OutDir.zip"
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path "$OutDir\*" -DestinationPath $zip -CompressionLevel Optimal
    Write-Host ("  {0}  ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
}

Write-Host "`nDone. Release folder: $OutDir" -ForegroundColor Green
Get-ChildItem "$OutDir" -Recurse -File |
    Select-Object @{n = 'File'; e = { $_.FullName.Replace($OutDir, '') } }, Length |
    Format-Table -AutoSize

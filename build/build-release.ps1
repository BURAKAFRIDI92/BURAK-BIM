#Requires -Version 5.1
<#
.SYNOPSIS
  Packages BURAK BIM for release: a source ZIP and the NSIS Setup.exe, written to .\dist

.DESCRIPTION
  1. (unless -NoBuild) publishes the MCP server as a single-file win-x64 executable into .\out\server
  2. stages a clean copy of the package (no .git, bin/obj, dist/out, old Setup.exe files)
  3. creates dist\BURAK-BIM-<version>.zip
  4. builds dist\BURAK-BIM-Setup-<version>.exe with NSIS (unless -NoInstaller)
  5. copies the standalone server exe to dist\ when .\out\server\BurakBim.Server.exe exists

  The installer itself builds the Revit / AutoCAD / Navisworks bridges on the user's PC against their own
  Autodesk versions, so the bridges are NOT compiled here. CI only compile-checks them.

.PARAMETER NoBuild      Skip 'dotnet publish' (CI already built .\out\server in an earlier step).
.PARAMETER NoInstaller  Create only the ZIP, skip NSIS (useful on machines without NSIS).
.PARAMETER Makensis     Full path to makensis.exe. Default: PATH, then Program Files (x86)\NSIS.
.PARAMETER Version      Override the version. Default: <Version> from BurakBim.Server.csproj.

.EXAMPLE
  ./build/build-release.ps1
  ./build/build-release.ps1 -NoBuild -Makensis "C:\Program Files (x86)\NSIS\makensis.exe"
  ./build/build-release.ps1 -NoInstaller
#>
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$NoInstaller,
    [string]$Makensis,
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

function Write-Step([string]$m) { Write-Host ""; Write-Host "==> $m" -ForegroundColor Cyan }

# ---------------------------------------------------------------- version
$serverProj = Join-Path $Root 'src/BurakBim.Server/BurakBim.Server.csproj'
if (-not (Test-Path $serverProj)) { throw "Cannot find $serverProj. Run this script from inside the BURAK BIM repository." }
if (-not $Version) {
    $m = [regex]::Match((Get-Content $serverProj -Raw), '<Version>\s*([0-9][0-9A-Za-z\.\-]*)\s*</Version>')
    if (-not $m.Success) { throw "No <Version> found in $serverProj. Pass -Version." }
    $Version = $m.Groups[1].Value
}
Write-Host "BURAK BIM release packaging, version $Version"

# ---------------------------------------------------------------- sanity check
$required = @('LICENSE', 'README.md', 'AGENTS.md', 'CHANGELOG.md',
              'installer/BurakBim.nsi', 'installer/install.ps1', 'installer/uninstall.ps1',
              'assets/burakbim.ico', 'assets/wizard.bmp', 'assets/header.bmp', 'src')
foreach ($r in $required) {
    if (-not (Test-Path (Join-Path $Root $r))) { throw "Required path missing: $r" }
}

# ---------------------------------------------------------------- 1. build the server
$outServer = Join-Path $Root 'out/server'
if (-not $NoBuild) {
    Write-Step "Publishing server (win-x64, self-contained, single file)"
    & dotnet publish $serverProj -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true -p:DebugType=none -o $outServer
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
}

# ---------------------------------------------------------------- 2. stage a clean package
$dist = Join-Path $Root 'dist'
$stageRoot = Join-Path $dist '_stage'
$stage = Join-Path $stageRoot "BURAK-BIM-$Version"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

try {
Write-Step "Staging package"
# top-level folders and file patterns that must not ship
$skipDirs = '^(\.git|\.vs|\.idea|\.vscode|bin|obj|dist|out|refs|build)(/|$)'
$skipFiles = '(^|/)(BURAK-BIM-Setup-.*\.exe|.*\.user|.*\.suo)$'
$rootFull = (Resolve-Path $Root).Path.TrimEnd('\', '/')
$copied = 0
Get-ChildItem -Path $Root -Recurse -File -Force | ForEach-Object {
    $rel = $_.FullName.Substring($rootFull.Length + 1) -replace '\\', '/'
    # skip repo plumbing and build output (the .github folder is CI only, not part of the package)
    if ($rel -match $skipDirs) { return }
    if ($rel -match '(^|/)(bin|obj)/') { return }
    if ($rel -match '^\.github(/|$)') { return }
    if ($rel -match $skipFiles) { return }
    $target = Join-Path $stage $rel
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $target
    $copied++
}
Write-Host "    staged $copied files"

# ---------------------------------------------------------------- 3. ZIP
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "BURAK-BIM-$Version.zip"
Write-Step "Creating $([IO.Path]::GetFileName($zip))"
Compress-Archive -Path $stage -DestinationPath $zip -Force

# ---------------------------------------------------------------- 4. Setup.exe
if (-not $NoInstaller) {
    if (-not $Makensis) {
        $cmd = Get-Command makensis -ErrorAction SilentlyContinue
        if ($cmd) { $Makensis = $cmd.Source }
        else {
            foreach ($p in @("${env:ProgramFiles(x86)}\NSIS\makensis.exe", "$env:ProgramFiles\NSIS\makensis.exe")) {
                if ($p -and (Test-Path $p)) { $Makensis = $p; break }
            }
        }
    }
    if (-not $Makensis -or -not (Test-Path $Makensis)) {
        throw "makensis.exe not found. Install NSIS (https://nsis.sourceforge.io) or pass -Makensis <path>, or use -NoInstaller."
    }
    $setup = Join-Path $dist "BURAK-BIM-Setup-$Version.exe"
    Write-Step "Building $([IO.Path]::GetFileName($setup)) with NSIS"
    & $Makensis "-DVERSION=$Version" "-DPKG=$stage" "-DOUTFILE=$setup" (Join-Path $stage 'installer/BurakBim.nsi')
    if ($LASTEXITCODE -ne 0) { throw "makensis failed (exit $LASTEXITCODE)" }
    if (-not (Test-Path $setup)) { throw "Setup.exe was not produced" }
}

# ---------------------------------------------------------------- 5. standalone server exe
$serverExe = Join-Path $outServer 'BurakBim.Server.exe'
if (Test-Path $serverExe) {
    Copy-Item $serverExe (Join-Path $dist "BurakBim.Server-$Version-win-x64.exe")
}

}
finally {
    if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
}

# ---------------------------------------------------------------- summary
Write-Step "Done. Files in dist\"
Get-ChildItem $dist | ForEach-Object { "{0,10:N0} KB  {1}" -f ($_.Length / 1KB), $_.Name } | Write-Host

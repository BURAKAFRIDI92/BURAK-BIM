#Requires -Version 5.1
<#
.SYNOPSIS
  Packages BURAK BIM for release: a source ZIP and the NSIS Setup.exe, written to .\dist

.DESCRIPTION
  1. (unless -NoBuild) publishes the MCP server as a single-file win-x64 executable into .\out\server
  2. stages a clean copy of the package (no .git, bin/obj, dist/out, old Setup.exe files)
  3. creates dist\BURAK-BIM-<version>.zip
  4. builds dist\BURAK-BIM-Setup-<version>.exe with NSIS (unless -NoInstaller)
  5. creates dist\BURAK-BIM-<version>.zip (complete package incl. Setup.exe and BURAK-BIM.mcpb)

  1.3: the package also carries
    server\BurakBim.Server.exe   self-contained server (from .\out\server)
    prebuilt\<host>\<year>\     bridges compiled in CI for every Revit / AutoCAD / Navisworks year (from .\out\prebuilt)
    tools\roslyn\               offline C# compiler for versions without a prebuilt bridge (from .\out\tools\roslyn)
    BURAK-BIM.mcpb              one-click Claude Desktop extension
  so the installer works without internet and without build tools.

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
              'installer/BurakBim.nsi', 'installer/BurakBimUninstaller.nsi', 'installer/README-INSTALL.txt', 'installer/install.ps1', 'installer/uninstall.ps1',
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
$skipDirs = '^(\.git|\.vs|\.idea|\.vscode|bin|obj|dist|out|refs|build|server|tools)(/|$)'
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

# 1.3 build outputs
$serverExe = Join-Path $outServer 'BurakBim.Server.exe'

# Optional Authenticode signing (removes SmartScreen "unknown publisher" warnings once the certificate has reputation).
# Set SIGN_PFX (path to .pfx) and SIGN_PFX_PASSWORD, e.g. from GitHub secrets. Skipped when not set.
function Invoke-Sign([string[]]$Files) {
    if (-not $env:SIGN_PFX -or -not (Test-Path $env:SIGN_PFX)) { return }
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match 'x64' } | Select-Object -Last 1
    if (-not $signtool) { Write-Warning 'signtool.exe not found - files not signed'; return }
    foreach ($f in $Files) {
        & $signtool.FullName sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f $env:SIGN_PFX /p $env:SIGN_PFX_PASSWORD /d 'BURAK BIM' $f | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "signing failed: $f" } else { Write-Host "    signed $([IO.Path]::GetFileName($f))" }
    }
}
if (Test-Path $serverExe) {
    Invoke-Sign (@($serverExe) + @(Get-ChildItem $outServer -Filter 'BurakBim.*.dll' | ForEach-Object { $_.FullName }))
    # the server is a self-contained FOLDER (no self-extracting single file - nothing unpacks itself into %TEMP%)
    Copy-Item $outServer (Join-Path $stage 'server') -Recurse -Force
    Write-Host "    + server\ (self-contained, $(@(Get-ChildItem (Join-Path $stage 'server') -Recurse -File).Count) files)"
} else { Write-Warning "out\server\BurakBim.Server.exe not found - the package will need a .NET SDK on the user's PC." }
$outPre = Join-Path $Root 'out/prebuilt'
if (Test-Path $outPre) {
    Copy-Item (Join-Path $outPre '*') (Join-Path $stage 'prebuilt') -Recurse -Force
    $n = @(Get-ChildItem (Join-Path $stage 'prebuilt') -Recurse -Filter 'BurakBim.Bridge.*.dll').Count
    Write-Host "    + prebuilt bridges: $n"
}
$outRoslyn = Join-Path $Root 'out/tools/roslyn'
if (Test-Path (Join-Path $outRoslyn 'csc.exe')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $stage 'tools/roslyn') | Out-Null
    Copy-Item (Join-Path $outRoslyn '*') (Join-Path $stage 'tools/roslyn') -Recurse -Force
    Write-Host "    + tools\roslyn (offline compiler)"
}

# Claude Desktop extension (.mcpb = zip with manifest.json + server)
New-Item -ItemType Directory -Force -Path $dist | Out-Null
if (Test-Path $serverExe) {
    $mcpbDir = Join-Path $stageRoot 'mcpb'
    New-Item -ItemType Directory -Force -Path (Join-Path $mcpbDir 'server') | Out-Null
    (Get-Content (Join-Path $Root 'installer/mcpb/manifest.json') -Raw).Replace('__VERSION__', $Version) | Set-Content (Join-Path $mcpbDir 'manifest.json') -Encoding ascii
    Copy-Item (Join-Path $Root 'assets/burakbim-256.png') (Join-Path $mcpbDir 'icon.png')
    Copy-Item (Join-Path $outServer '*') (Join-Path $mcpbDir 'server') -Recurse -Force
    $mcpbZip = Join-Path $stageRoot 'BURAK-BIM.zip'
    Compress-Archive -Path (Join-Path $mcpbDir '*') -DestinationPath $mcpbZip -Force
    Copy-Item $mcpbZip (Join-Path $dist "BURAK-BIM-$Version.mcpb")
    Copy-Item $mcpbZip (Join-Path $stage 'BURAK-BIM.mcpb')
    Write-Host "    + BURAK-BIM.mcpb (Claude Desktop extension)"
}

# ---------------------------------------------------------------- 3. ZIP
New-Item -ItemType Directory -Force -Path $dist | Out-Null

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
    Invoke-Sign @($setup)
    # the complete ZIP also contains the installer at its top level
    Copy-Item $setup (Join-Path $stage "BURAK-BIM-Setup-$Version.exe")

    # standalone uninstaller + the ZIP people share: one folder with the installer, uninstaller, guide, checksums
    $uninstaller = Join-Path $stageRoot 'BURAK-BIM-Uninstaller.exe'
    Write-Step "Building BURAK-BIM-Uninstaller.exe with NSIS"
    & $Makensis "-DVERSION=$Version" "-DPKG=$stage" "-DOUTFILE=$uninstaller" (Join-Path $stage 'installer/BurakBimUninstaller.nsi')
    if ($LASTEXITCODE -ne 0) { throw "makensis failed for the uninstaller (exit $LASTEXITCODE)" }
    Invoke-Sign @($uninstaller)
    Copy-Item $uninstaller (Join-Path $stage 'BURAK-BIM-Uninstaller.exe')

    $kitRoot = Join-Path $stageRoot 'kit'
    $kit = Join-Path $kitRoot "BURAK-BIM-$Version"
    New-Item -ItemType Directory -Force -Path $kit | Out-Null
    Copy-Item $setup (Join-Path $kit 'BURAK-BIM-Installer.exe')
    Copy-Item $uninstaller (Join-Path $kit 'BURAK-BIM-Uninstaller.exe')
    Copy-Item (Join-Path $Root 'installer/README-INSTALL.txt') (Join-Path $kit 'README-INSTALL.txt')
    Copy-Item (Join-Path $Root 'LICENSE') (Join-Path $kit 'LICENSE.txt')
    if (Test-Path (Join-Path $stage 'BURAK-BIM.mcpb')) { Copy-Item (Join-Path $stage 'BURAK-BIM.mcpb') (Join-Path $kit 'BURAK-BIM.mcpb') }
    $sums = Get-ChildItem $kit -File | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
    Set-Content (Join-Path $kit 'SHA256SUMS.txt') $sums -Encoding ascii
    Compress-Archive -Path $kit -DestinationPath (Join-Path $dist "BURAK-BIM-$Version-Complete.zip") -Force
}

# ---------------------------------------------------------------- 5. complete ZIP (Setup.exe + .mcpb + full package)
$zip = Join-Path $dist "BURAK-BIM-$Version.zip"
Write-Step "Creating $([IO.Path]::GetFileName($zip))"
Compress-Archive -Path $stage -DestinationPath $zip -Force


}
finally {
    if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
}

# ---------------------------------------------------------------- summary
Write-Step "Done. Files in dist\"
Get-ChildItem $dist | ForEach-Object { "{0,10:N0} KB  {1}" -f ($_.Length / 1KB), $_.Name } | Write-Host

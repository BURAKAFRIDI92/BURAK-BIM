#Requires -Version 5.1
<#
.SYNOPSIS
  BURAK BIM installer: one MCP connector for Revit, AutoCAD (+ Civil 3D and all AutoCAD verticals),
  Navisworks, Inventor, ReCap Pro, Robot - versions 2022-2027.

.DESCRIPTION
  1. Picks the install folder (default D:\BURAK BIM - nothing large is written to C:).
  2. Detects every installed Autodesk product and version (and CSI, Bluebeam, PlanSwift, Archicad ...).
  3. 1.3: installs the PREBUILT server (self-contained, needs no .NET) and the PREBUILT bridge for each detected
     Revit / AutoCAD-family / Navisworks version 2022-2027. No internet and no build tools are needed.
  4. Only for a version without a prebuilt bridge: compiles it on this PC against that version's own API files
     with the bundled offline C# compiler (tools\roslyn) - or, last resort, a .NET SDK.
  5. Registers the bridges (Revit .addin, Autodesk autoloader bundles) - Inventor/Robot use COM, no add-in.
  6. Registers "BURAK-BIM" in every AI client found: Claude Desktop (classic + Store), Claude Code, Codex,
     Cursor, Windsurf, Google Antigravity, Gemini CLI, VS Code, Visual Studio, Cline, Roo, Kilo, LM Studio, Kiro,
     Amazon Q, Trae, Continue, opencode (BurakBim.Server.exe --connect-clients; backups are kept).
  7. Removes the "BURAK BIM Sentinel" logon task of older versions. With -Sentinel it installs the task instead,
     so new Autodesk versions installed later are detected and connected automatically.

.PARAMETER InstallDir   Install folder. Default: D:\BURAK BIM (falls back to another non-system drive, then %LOCALAPPDATA%).
.PARAMETER Clients      auto (every detected client) | none | comma list:
                        claude-desktop,claude-code,codex,cursor,windsurf,antigravity,gemini,vscode
.PARAMETER Rescan       Sentinel mode: only detect new software and build/register missing bridges.
.PARAMETER Quiet        Less console output.
.PARAMETER Sentinel     Install the logon sentinel task (off by default).
.PARAMETER NoSentinel   Kept for compatibility; the task is off unless -Sentinel is given.
.PARAMETER SearchPaths  Extra folders that contain Autodesk software installed in custom places
                        (e.g. "D:\ALL PROGRAMS"). Remembered for later rescans.
.PARAMETER LocalBuild   Ignore prebuilt bridges and compile every bridge on this PC.
.PARAMETER WhatIf       Show what would be done, change nothing.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File install.ps1
  powershell -ExecutionPolicy Bypass -File install.ps1 -InstallDir "E:\BURAK BIM" -Clients claude-desktop,codex
  powershell -ExecutionPolicy Bypass -File install.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InstallDir,
    [string]$Clients = 'auto',
    [switch]$Rescan,
    [switch]$Quiet,
    [switch]$Sentinel,
    [switch]$NoSentinel,
    [string[]]$SearchPaths,
    [switch]$LocalBuild
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$BurakVersion = '1.3.0'
$RevitAddInId = '8E3B2F7A-4C1D-4B9E-A2F5-6D7C8B9A0E1F'
$ServerName = 'BURAK-BIM'
$Package = Split-Path -Parent $PSScriptRoot      # package root (contains src\, installer\, prebuilt\)
if (-not (Test-Path (Join-Path $Package 'src'))) { $Package = $PSScriptRoot }   # running from <install>\setup
$script:Problems = @()

function Write-Step([string]$m) { Write-Host ""; Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok([string]$m) { if (-not $Quiet) { Write-Host "    [OK] $m" -ForegroundColor Green } }
function Write-Info([string]$m) { if (-not $Quiet) { Write-Host "    $m" } }
function Write-Warn2([string]$m) { Write-Host "    [!] $m" -ForegroundColor Yellow; $script:Problems += $m }

function Write-Utf8([string]$Path, [string]$Text) {
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false)))
}

# ------------------------------------------------------------------ install folder
function Get-DefaultInstallDir {
    $reg = Get-ItemProperty -Path 'HKCU:\Software\BURAK BIM' -Name InstallDir -ErrorAction SilentlyContinue
    if ($reg -and $reg.InstallDir) { return $reg.InstallDir }
    $fixed = @(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' -ErrorAction SilentlyContinue | Sort-Object DeviceID)
    $d = $fixed | Where-Object { $_.DeviceID -eq 'D:' } | Select-Object -First 1
    if ($d) { return 'D:\BURAK BIM' }
    $sys = $env:SystemDrive
    $other = $fixed | Where-Object { $_.DeviceID -ne $sys } | Sort-Object FreeSpace -Descending | Select-Object -First 1
    if ($other) { return "$($other.DeviceID)\BURAK BIM" }
    return (Join-Path $env:LOCALAPPDATA 'BURAK BIM')
}

if (-not $InstallDir) { $InstallDir = Get-DefaultInstallDir }
$InstallDir = $InstallDir.TrimEnd('\')
$LogDir = Join-Path $InstallDir 'logs'

Write-Host ""
Write-Host "  ____  _   _ ____      _    _  __  ____ ___ __  __" -ForegroundColor DarkCyan
Write-Host " | __ )| | | |  _ \    / \  | |/ / | __ )_ _|  \/  |" -ForegroundColor DarkCyan
Write-Host " |  _ \| | | | |_) |  / _ \ | ' /  |  _ \| || |\/| |" -ForegroundColor DarkCyan
Write-Host " | |_) | |_| |  _ <  / ___ \| . \  | |_) | || |  | |" -ForegroundColor DarkCyan
Write-Host " |____/ \___/|_| \_\/_/   \_\_|\_\ |____/___|_|  |_|  $BurakVersion" -ForegroundColor DarkCyan
Write-Host "  One AI connector for Revit, AutoCAD, Civil 3D, Navisworks, SAP2000, ETABS, CSiBridge, SAFE, Archicad, Bluebeam, PlanSwift" -ForegroundColor Gray
Write-Host "  Install folder: $InstallDir   Mode: $(if ($Rescan) {'rescan'} else {'install'})$(if ($WhatIfPreference) {' (WhatIf - no changes)'})"

if (-not $WhatIfPreference) {
    New-Item -ItemType Directory -Force -Path $InstallDir, $LogDir | Out-Null
    try { Start-Transcript -Path (Join-Path $LogDir ("install-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".log")) | Out-Null } catch { }
}

# ------------------------------------------------------------------ detection
function Get-FileYear([string]$s) { if ($s -match '(20\d\d)') { return [int]$Matches[1] } return 0 }

# Year of an Autodesk exe from its version resource (works for installs in ANY folder, e.g. D:\ALL PROGRAMS\...)
function Get-ExeInfo([string]$exe) {
    try { $vi = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe) } catch { return $null }
    $name = "$($vi.ProductName)"; $y = Get-FileYear $name
    if ($y -eq 0) { $y = Get-FileYear $vi.FileDescription }
    if ($y -eq 0) { $y = Get-FileYear (Split-Path (Split-Path $exe -Parent) -Leaf) }
    if ($y -eq 0) { $y = Get-FileYear (Split-Path (Split-Path (Split-Path $exe -Parent) -Parent) -Leaf) }
    if ($y -eq 0 -and $vi.ProductMajorPart -gt 0) {
        $leaf = [IO.Path]::GetFileName($exe).ToLower()
        $m = $vi.ProductMajorPart
        switch ($leaf) {
            'roamer.exe'   { $y = 2003 + $m }                     # Navisworks 19 = 2022 ... 24 = 2027
            'revit.exe'    { if ($m -gt 2000) { $y = $m } else { $y = 2000 + $m } }
            'inventor.exe' { $y = 1996 + $m }                     # Inventor 26 = 2022
        }
    }
    return [pscustomobject]@{ Year = $y; ProductName = $name }
}

# Every place an Autodesk exe can be found: standard roots on all drives, Uninstall InstallLocation + DisplayIcon,
# App Paths, the whole HKLM\SOFTWARE\Autodesk tree, Start-menu shortcuts, running processes, -SearchPaths.
function Get-AutodeskExeCandidates {
    $names = 'Revit.exe', 'Roamer.exe', 'Inventor.exe', 'ReCap.exe', 'robot.exe', 'acad.exe'
    $set = @{}
    $addExe = { param($f) if ($f -and (Test-Path -LiteralPath $f -PathType Leaf)) { $set[(Resolve-Path -LiteralPath $f).Path.ToLower()] = (Resolve-Path -LiteralPath $f).Path } }
    $addDir = { param($d, $depth)
        if (-not $d) { return }
        $d = "$d".Trim().Trim('"').TrimEnd('\')
        if (-not (Test-Path -LiteralPath $d -PathType Container)) { return }
        foreach ($n in $names) { & $addExe (Join-Path $d $n); & $addExe (Join-Path $d "Bin\$n"); & $addExe (Join-Path $d "System\Exe\$n") }
        if ($depth -gt 0) {
            Get-ChildItem -LiteralPath $d -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch '^(Windows|\$Recycle\.Bin|System Volume Information|ProgramData|Users|Recovery|PerfLogs)$' } |
                ForEach-Object { & $addDir $_.FullName ($depth - 1) }
        }
    }
    # 1) standard roots on every fixed drive
    foreach ($drv in Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' -ErrorAction SilentlyContinue) {
        foreach ($sub in 'Program Files\Autodesk', 'Program Files (x86)\Autodesk', 'Autodesk') { & $addDir (Join-Path ($drv.DeviceID + '\') $sub) 2 }
    }
    # 2) Uninstall entries (InstallLocation and DisplayIcon)
    foreach ($hive in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall', 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall') {
        foreach ($k in Get-ChildItem $hive -ErrorAction SilentlyContinue) {
            $e = Get-ItemProperty $k.PSPath -ErrorAction SilentlyContinue
            if ("$($e.Publisher) $($e.DisplayName)" -notmatch 'Autodesk') { continue }
            & $addDir $e.InstallLocation 2
            if ($e.DisplayIcon) { $ico = ("$($e.DisplayIcon)" -split ',')[0].Trim('"'); if ($ico -match '\.exe$') { & $addExe $ico; & $addDir (Split-Path $ico -Parent) 1 } }
        }
    }
    # 3) App Paths
    foreach ($n in $names) {
        foreach ($ap in "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\$n", "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\$n") {
            try { & $addExe ((Get-ItemProperty $ap -ErrorAction Stop).'(default)'.Trim('"')) } catch { }
        }
    }
    # 4) any path-like value under HKLM\SOFTWARE\Autodesk (Revit / Navisworks / Inventor product keys)
    try {
        Get-ChildItem 'HKLM:\SOFTWARE\Autodesk' -Recurse -Depth 4 -ErrorAction SilentlyContinue | ForEach-Object {
            $props = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
            if (-not $props) { return }
            foreach ($pn in 'InstallLocation', 'InstallationLocation', 'Location', 'InstallPath', 'InstallDir', 'Path', 'ProductPath') {
                $v = $props.$pn
                if ($v -is [string] -and $v -match '^[A-Za-z]:\\') { if ($v -match '\.exe$') { & $addExe $v } else { & $addDir $v 1 } }
            }
        }
    } catch { }
    # 5) Start-menu shortcuts
    try {
        $sh = New-Object -ComObject WScript.Shell
        foreach ($sm in "$env:ProgramData\Microsoft\Windows\Start Menu\Programs", "$env:APPDATA\Microsoft\Windows\Start Menu\Programs") {
            Get-ChildItem $sm -Recurse -Filter *.lnk -ErrorAction SilentlyContinue | ForEach-Object {
                try { $t = $sh.CreateShortcut($_.FullName).TargetPath; if ($t -and ($names -contains [IO.Path]::GetFileName($t))) { & $addExe $t } } catch { }
            }
        }
    } catch { }
    # 6) running Autodesk apps
    foreach ($pn in 'Revit', 'Roamer', 'Inventor', 'ReCap', 'robot', 'acad') {
        Get-Process -Name $pn -ErrorAction SilentlyContinue | ForEach-Object { try { & $addExe $_.Path } catch { } }
    }
    # 7) user-supplied folders (-SearchPaths, remembered in config\searchpaths.txt)
    foreach ($sp in $script:SearchRoots) { & $addDir $sp 4 }
    return $set.Values
}

function Get-BurakProducts {
    $list = New-Object System.Collections.ArrayList
    # AutoCAD family from the product registry (AutoCAD, Civil 3D, Plant 3D, Map 3D, MEP, Architecture, Electrical, Mechanical, Advance Steel)
    $acadRoot = 'HKLM:\SOFTWARE\Autodesk\AutoCAD'
    if (Test-Path $acadRoot) {
        foreach ($rel in Get-ChildItem $acadRoot -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like 'R*' }) {
            foreach ($prod in Get-ChildItem $rel.PSPath -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like 'ACAD-*' }) {
                $p = Get-ItemProperty $prod.PSPath -ErrorAction SilentlyContinue
                $loc = $p.AcadLocation; if (-not $loc) { $loc = $p.Location }
                if (-not $p.ProductName -or -not $loc) { continue }
                if (-not (Test-Path (Join-Path $loc 'acad.exe'))) { continue }
                [void]$list.Add([pscustomobject]@{ Family = 'autocad'; Name = $p.ProductName; Year = (Get-FileYear $p.ProductName); Dir = $loc.TrimEnd('\'); Release = $rel.PSChildName; ProductKey = $prod.PSChildName })
            }
        }
    }
    foreach ($exe in Get-AutodeskExeCandidates) {
        $leaf = [IO.Path]::GetFileName($exe).ToLower()
        $dir = Split-Path $exe -Parent
        $info = Get-ExeInfo $exe; if (-not $info) { continue }
        $y = $info.Year; $pn = $info.ProductName
        switch ($leaf) {
            'revit.exe' {
                if ($pn -match 'Revit' -or (Test-Path (Join-Path $dir 'RevitAPI.dll'))) { [void]$list.Add([pscustomobject]@{ Family = 'revit'; Name = "Autodesk Revit $y"; Year = $y; Dir = $dir }) } }
            'roamer.exe' {
                if (-not (Test-Path (Join-Path $dir 'Autodesk.Navisworks.Api.dll'))) { break }
                if ($pn -match 'Freedom' -or $dir -match 'Freedom') { break }
                $ed = $(if ($pn -match 'Simulate' -or $dir -match 'Simulate') { 'NAVSIM' } else { 'NAVMAN' })
                [void]$list.Add([pscustomobject]@{ Family = 'navisworks'; Name = "Autodesk Navisworks $(if ($ed -eq 'NAVSIM') { 'Simulate' } else { 'Manage' }) $y"; Year = $y; Dir = $dir; Edition = $ed }) }
            'inventor.exe' {
                $root = Split-Path $dir -Parent
                [void]$list.Add([pscustomobject]@{ Family = 'inventor'; Name = "Autodesk Inventor $y"; Year = $y; Dir = $root }) }
            'recap.exe' { [void]$list.Add([pscustomobject]@{ Family = 'recap'; Name = 'Autodesk ReCap Pro'; Year = $y; Dir = $dir }) }
            'robot.exe' {
                $root = Split-Path (Split-Path $dir -Parent) -Parent
                [void]$list.Add([pscustomobject]@{ Family = 'robot'; Name = "Autodesk Robot Structural Analysis $y"; Year = $y; Dir = $root }) }
            'acad.exe' {
                if (-not ($list | Where-Object { $_.Family -eq 'autocad' -and $_.Dir -eq $dir })) {
                    [void]$list.Add([pscustomobject]@{ Family = 'autocad'; Name = $(if ($pn) { $pn } else { "AutoCAD $y" }); Year = $y; Dir = $dir }) } }
        }
    }
    # one entry per family/year/folder
    $seen = @{}; $out = New-Object System.Collections.ArrayList
    foreach ($p in $list) { $k = "$($p.Family)|$($p.Year)|$($p.Dir.ToLower())"; if (-not $seen.ContainsKey($k)) { $seen[$k] = $true; [void]$out.Add($p) } }
    return $out
}

$script:SearchRoots = @()
$spFile = Join-Path $InstallDir 'config\searchpaths.txt'
if (Test-Path $spFile) { $script:SearchRoots += @(Get-Content $spFile | Where-Object { $_.Trim() }) }
if ($SearchPaths) {
    $SearchPaths = @($SearchPaths | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim().Trim("'").Trim('"').TrimEnd('\') } | Where-Object { $_ })
    $script:SearchRoots += $SearchPaths
    if (-not $WhatIfPreference) { Write-Utf8 $spFile ((($script:SearchRoots | Select-Object -Unique) -join "`r`n") + "`r`n") }
}
Write-Step 'Detecting Autodesk software'
$products = @(Get-BurakProducts)
if ($products.Count -eq 0) { Write-Warn2 'No Autodesk products found. BURAK BIM installs anyway; the sentinel connects software you install later.' }
foreach ($p in $products | Sort-Object Family, Year) {
    $supported = ($p.Year -ge 2022 -and $p.Year -le 2027)
    $how = switch ($p.Family) { 'revit' { 'bridge add-in' } 'autocad' { 'bridge add-in + COM' } 'navisworks' { 'bridge plug-in' } 'inventor' { 'COM (no add-in needed)' } 'robot' { 'COM' } 'recap' { 'launch + UI automation' } default { 'UI automation' } }
    if (-not $supported -and $p.Family -in 'revit', 'autocad', 'navisworks') { $how = 'UI automation only (version outside 2022-2027)' }
    Write-Info ("{0,-48} {1}" -f $p.Name, $how)
}

# previous state (for rescan)
$StateFile = Join-Path $InstallDir 'config\installed.json'
$state = @{ bridges = @() }
if (Test-Path $StateFile) { try { $state = Get-Content $StateFile -Raw | ConvertFrom-Json } catch { } }
$haveBridges = @(); if ($state.bridges) { $haveBridges = @($state.bridges) }

$bridgeTargets = @()
foreach ($p in $products) {
    if ($p.Year -lt 2022 -or $p.Year -gt 2027) { continue }
    if ($p.Family -notin 'revit', 'autocad', 'navisworks') { continue }
    $key = "$($p.Family)-$($p.Year)"
    if ($bridgeTargets | Where-Object { $_.Key -eq $key }) { continue }
    $bridgeTargets += [pscustomobject]@{ Key = $key; Family = $p.Family; Year = $p.Year; Dir = $p.Dir }
}
if ($Rescan) {
    $new = @($bridgeTargets | Where-Object { $haveBridges -notcontains $_.Key -or -not (Test-Path (Join-Path $InstallDir "bridges\$($_.Family)\$($_.Year)")) })
    if ($new.Count -eq 0 -and (Test-Path (Join-Path $InstallDir 'server'))) {
        Write-Ok 'No new Autodesk versions since last run. Nothing to do.'
        try { Stop-Transcript | Out-Null } catch { }
        exit 0
    }
    Write-Info ("New versions to connect: " + (($new | ForEach-Object { $_.Key }) -join ', '))
}

if ($WhatIfPreference) {
    Write-Step 'WhatIf summary'
    Write-Info "Would copy BURAK BIM to $InstallDir, build server + bridges for: $(($bridgeTargets | ForEach-Object { $_.Key }) -join ', ')"
    Write-Info "Would register AI clients: $Clients ; sentinel: $($Sentinel -and -not $NoSentinel)"
    exit 0
}

# ------------------------------------------------------------------ copy package
Write-Step 'Preparing install folder'
foreach ($sub in 'server', 'bridges', 'setup', 'tools', 'cache', 'config', 'runtime', 'logs', 'audit', 'reports', 'exports', 'scratch', 'captures', 'toolpacks') {
    New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir $sub) | Out-Null
}
Write-Utf8 (Join-Path $InstallDir 'burakbim.root') "BURAK BIM $BurakVersion install root"
$setup = Join-Path $InstallDir 'setup'
if ((Resolve-Path $Package).Path.TrimEnd('\') -ne (Resolve-Path $setup).Path.TrimEnd('\')) {
    foreach ($item in 'src', 'installer', 'prebuilt', 'server', 'tools', 'docs', 'assets', 'README.md', 'AGENTS.md', 'LICENSE', 'CHANGELOG.md', 'BURAK-BIM.mcpb') {
        $from = Join-Path $Package $item
        if (Test-Path $from) { Copy-Item $from -Destination $setup -Recurse -Force }
    }
}
# the sentinel and Apps & Features call <install>\setup\install.ps1 / uninstall.ps1
foreach ($ps in 'install.ps1', 'uninstall.ps1') {
    $from = Join-Path $Package "installer\$ps"
    if (-not (Test-Path $from)) { $from = Join-Path $PSScriptRoot $ps }
    $to = Join-Path $setup $ps
    if ((Test-Path $from) -and ((Resolve-Path $from).Path -ne $to)) { Copy-Item $from $to -Force }
}
$Src = Join-Path $setup 'src'
Write-Ok "Package copied to $setup"

# 1.3: files extracted from a downloaded ZIP carry the 'downloaded from the internet' mark. Revit / AutoCAD /
# Navisworks (.NET Framework) then REFUSE to load the bridge DLLs ("Operation is not supported", 0x80131515) -
# one of the reasons BURAK BIM worked on some PCs and not on others. Remove the mark from everything we install.
function Unblock-Tree([string]$Path) {
    if (-not (Test-Path $Path)) { return }
    Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { try { Unblock-File -LiteralPath $_.FullName -ErrorAction Stop } catch { } }
}
Unblock-Tree $Package
Unblock-Tree $setup
Write-Ok 'Download marks removed (files can be loaded by Revit / AutoCAD / Navisworks)'

New-Item -Path 'HKCU:\Software\BURAK BIM' -Force | Out-Null
Set-ItemProperty -Path 'HKCU:\Software\BURAK BIM' -Name InstallDir -Value $InstallDir
Set-ItemProperty -Path 'HKCU:\Software\BURAK BIM' -Name Version -Value $BurakVersion

# keep every build cache on the install drive
$env:DOTNET_CLI_HOME = Join-Path $InstallDir 'cache\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $InstallDir 'cache\nuget'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $InstallDir 'cache\nuget-http'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:BURAK_BIM_HOME = $InstallDir

# ------------------------------------------------------------------ .NET SDK
function Get-Dotnet {
    $private = Join-Path $InstallDir 'tools\dotnet\dotnet.exe'
    if (Test-Path $private) {
        $sdks = & $private --list-sdks 2>$null | Out-String
        if ($sdks -match '(?m)^8\.') { return $private }
    }
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) {
        $sdks = & $cmd.Source --list-sdks 2>$null | Out-String
        if ($sdks -match '(?m)^(8|9|10)\.') { return $cmd.Source }
    }
    Write-Info 'Installing a private .NET 8 SDK into the install folder (one-time, ~250 MB, not on C:)...'
    $tools = Join-Path $InstallDir 'tools'
    $scriptPath = Join-Path $tools 'dotnet-install.ps1'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile $scriptPath
    $oldTemp = $env:TEMP
    $env:TEMP = Join-Path $InstallDir 'cache'; $env:TMP = $env:TEMP
    $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & $scriptPath -Channel 8.0 -InstallDir (Join-Path $tools 'dotnet') -NoPath | Out-Host
    $ErrorActionPreference = $eap
    $env:TEMP = $oldTemp; $env:TMP = $oldTemp
    if (-not (Test-Path $private)) { throw '.NET SDK installation failed (internet access to dot.net / nuget.org is required once).' }
    return $private
}

# 1.3: the .NET SDK is only needed when something has to be compiled and the offline compiler is missing.
$dotnet = $null
$existing = Get-Command dotnet -ErrorAction SilentlyContinue
if ($existing) {
    $sdks = & $existing.Source --list-sdks 2>$null | Out-String
    if ($sdks -match '(?m)^(8|9|10)\.') { $dotnet = $existing.Source }
}
$privateDotnet = Join-Path $InstallDir 'tools\dotnet\dotnet.exe'
if (-not $dotnet -and (Test-Path $privateDotnet)) { $dotnet = $privateDotnet }

function Invoke-Build([string]$Project, [string[]]$Properties, [string]$Out, [switch]$Publish) {
    $bargs = @()
    if ($Publish) { $bargs += 'publish' } else { $bargs += 'build' }
    $bargs += "`"$Project`"", '-c', 'Release', '-o', "`"$Out`"", '--nologo', '-v', 'minimal'
    foreach ($p in $Properties) { $bargs += "`"-p:$p`"" }
    $stamp = Get-Date -Format 'HHmmss'
    $name = [IO.Path]::GetFileNameWithoutExtension($Project)
    $log = Join-Path $LogDir "build-$name-$stamp.log"
    $err = Join-Path $LogDir "build-$name-$stamp.err.log"
    # Start-Process instead of '& dotnet *> log': Windows PowerShell 5.1 turns native stderr output
    # into terminating errors when $ErrorActionPreference = 'Stop'.
    $proc = Start-Process -FilePath $dotnet -ArgumentList $bargs -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $log -RedirectStandardError $err
    if ($proc.ExitCode -ne 0) {
        $tail = (Get-Content $log -Tail 15 -ErrorAction SilentlyContinue) -join "`n"
        throw "build failed (exit $($proc.ExitCode)), see $log`n$tail"
    }
}

# ------------------------------------------------------------------ 1.3: offline bridge compiler
# Uses the bundled Roslyn compiler (tools\roslyn\csc.exe, runs on the .NET Framework 4.8 that every Windows has)
# against the API DLLs of the INSTALLED product and the framework reference assemblies already on this PC.
function Get-OfflineCsc {
    foreach ($c in @((Join-Path $Package 'tools\roslyn\csc.exe'), (Join-Path $setup 'tools\roslyn\csc.exe'), (Join-Path $InstallDir 'tools\roslyn\csc.exe'))) { if (Test-Path $c) { return $c } }
    return $null
}

function Get-NetCoreRefDirs([int]$Major) {
    $dirs = @()
    foreach ($root in @("$env:ProgramFiles\dotnet\shared")) {
        foreach ($fw in 'Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App') {
            $v = Get-ChildItem (Join-Path $root $fw) -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "^$Major\." } | Sort-Object { [version]($_.Name -replace '-.*$', '') } -Descending | Select-Object -First 1
            if ($v) { $dirs += $v.FullName }
        }
    }
    return $dirs
}

function Invoke-OfflineBridgeBuild($t, [string]$Out) {
    $csc = Get-OfflineCsc
    if (-not $csc) { throw 'bundled compiler not present' }
    $shared = Join-Path $setup 'prebuilt\_shared'
    $proj = switch ($t.Family) { 'revit' { 'BurakBim.Bridge.Revit' } 'autocad' { 'BurakBim.Bridge.AutoCAD' } 'navisworks' { 'BurakBim.Bridge.Navisworks' } }
    $netcore = ($t.Family -eq 'revit' -and $t.Year -ge 2025) -or ($t.Family -eq 'autocad' -and $t.Year -ge 2025)
    $major = if ($t.Year -ge 2027) { 10 } else { 8 }
    $defines = @('TRACE', 'BURAK_ROSLYN')
    $refs = New-Object System.Collections.ArrayList
    switch ($t.Family) {
        'revit' {
            $defines += "REVIT$($t.Year)"; if ($t.Year -ge 2024) { $defines += 'REVIT_LONG_IDS' }
            foreach ($d in 'RevitAPI.dll', 'RevitAPIUI.dll') { [void]$refs.Add((Join-Path $t.Dir $d)) }
        }
        'autocad' { $defines += "ACAD$($t.Year)"; foreach ($d in 'accoremgd.dll', 'acdbmgd.dll', 'acmgd.dll') { [void]$refs.Add((Join-Path $t.Dir $d)) } }
        'navisworks' { $defines += "NAVIS$($t.Year)"; [void]$refs.Add((Join-Path $t.Dir 'Autodesk.Navisworks.Api.dll')) }
    }
    foreach ($r in $refs) { if (-not (Test-Path $r)) { throw "missing $r" } }
    $kind = if ($netcore) { 'netcore' } else { 'net48' }
    $roslynDir = Join-Path $shared $kind
    if (-not (Test-Path (Join-Path $roslynDir 'Microsoft.CodeAnalysis.dll'))) { throw "Roslyn libraries missing in $roslynDir" }
    Get-ChildItem $roslynDir -Filter *.dll | ForEach-Object { [void]$refs.Add($_.FullName) }
    $nostd = $false
    if ($netcore) {
        $fw = Get-NetCoreRefDirs $major
        if ($fw.Count -lt 2) { throw ".NET $major desktop runtime not found (it is installed with Autodesk $($t.Year))" }
        foreach ($d in $fw) { Get-ChildItem $d -Filter *.dll | Where-Object { $_.Name -notmatch '^(api-ms|mscordaccore|mscordbi|clrjit|coreclr|hostpolicy|Microsoft\.DiaSymReader|vcruntime|ucrtbase|D3DCompiler|PenImc|wpfgfx|PresentationNative)' } | ForEach-Object { [void]$refs.Add($_.FullName) } }
        $defines += 'NETCOREAPP'; $defines += 'NET'; $nostd = $true
    } else {
        $fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
        foreach ($d in 'mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'System.Data.dll', 'Microsoft.CSharp.dll', 'System.Drawing.dll',
                       'System.Windows.Forms.dll', 'System.Xaml.dll', 'System.Net.Http.dll', 'System.Numerics.dll', 'System.Runtime.Serialization.dll', 'netstandard.dll',
                       'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll') {
            $f = Join-Path $fx $d; if (Test-Path $f) { [void]$refs.Add($f) }
        }
        foreach ($f in (Get-ChildItem (Join-Path $fx 'Facades') -Filter *.dll -ErrorAction SilentlyContinue)) { [void]$refs.Add($f.FullName) }
        $nostd = $true
    }
    $srcFiles = @()
    $srcFiles += Get-ChildItem (Join-Path $Src 'Shared') -Filter *.cs
    $srcFiles += Get-ChildItem (Join-Path $Src 'BurakBim.Bridge.Core') -Filter *.cs
    $srcFiles += Get-ChildItem (Join-Path $Src $proj) -Filter *.cs
    New-Item -ItemType Directory -Force -Path $Out | Out-Null
    $rsp = Join-Path $LogDir "csc-$($t.Key).rsp"
    $lines = @('/nologo', '/target:library', '/platform:x64', '/optimize+', '/langversion:latest', '/nowarn:CS0618,CS0219,CS1701,CS1702,CS8632,CS0168', '/noconfig')
    if ($nostd) { $lines += '/nostdlib+' }
    $lines += ('/define:' + ($defines -join ';'))
    $lines += ('/out:"' + (Join-Path $Out "$proj.dll") + '"')
    foreach ($r in ($refs | Select-Object -Unique)) { $lines += ('/reference:"' + $r + '"') }
    if ($t.Family -eq 'revit') {
        foreach ($png in 'burakbim-16.png', 'burakbim-32.png') { $pf = Join-Path $setup "assets\$png"; if (Test-Path $pf) { $lines += ('/resource:"' + $pf + '",brand.' + $png) } }
    }
    foreach ($f in $srcFiles) { $lines += ('"' + $f.FullName + '"') }
    [IO.File]::WriteAllLines($rsp, $lines)
    $log = Join-Path $LogDir "csc-$($t.Key).log"
    $proc = Start-Process -FilePath $csc -ArgumentList "@`"$rsp`"" -NoNewWindow -Wait -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err"
    if ($proc.ExitCode -ne 0) { $tail = (Get-Content $log -Tail 12 -ErrorAction SilentlyContinue) -join "`n"; throw "compile failed (see $log)`n$tail" }
    $sharedDest = Join-Path $InstallDir 'bridges\_shared'
    Copy-Item $shared (Join-Path $InstallDir 'bridges') -Recurse -Force
    Unblock-Tree $sharedDest
    return (Test-Path (Join-Path $Out "$proj.dll"))
}

# ------------------------------------------------------------------ server
$ServerExe = Join-Path $InstallDir 'server\BurakBim.Server.exe'
$ServerCommand = $ServerExe
$ServerArgs = @()
if (-not $Rescan -or -not (Test-Path $ServerExe)) {
    Write-Step 'Installing BURAK BIM server'
    $built = $false
    Get-Process BurakBim.Server -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$InstallDir*" } | ForEach-Object {
        Write-Info "Stopping running server (pid $($_.Id)) - restart your AI client afterwards"; try { Stop-Process -Id $_.Id -Force } catch { } }
    Start-Sleep -Milliseconds 500
    # 1.3: the release ships a self-contained server (no .NET runtime, no SDK, no internet needed)
    foreach ($cand in @((Join-Path $Package 'server\BurakBim.Server.exe'), (Join-Path $setup 'server\BurakBim.Server.exe'))) {
        if (-not $built -and (Test-Path $cand)) {
            try {
                New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir 'server') | Out-Null
                Copy-Item $cand $ServerExe -Force
                Unblock-Tree (Join-Path $InstallDir 'server')
                $built = Test-Path $ServerExe
                if ($built) { Write-Ok "Prebuilt server installed: $ServerExe" }
            } catch { Write-Warn2 ("Could not copy the prebuilt server (is an AI client still using it? close it): " + $_.Exception.Message) }
        }
    }
    if (-not $built -and -not $dotnet) {
        try { $dotnet = Get-Dotnet; Write-Ok "dotnet: $dotnet" } catch { Write-Warn2 ("No .NET SDK: " + $_.Exception.Message) }
    }
    if (-not $built -and $dotnet) {
        try {
            Get-Process BurakBim.Server -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$InstallDir*" } | ForEach-Object {
                Write-Info "Stopping running server (pid $($_.Id)) - restart your AI client afterwards"; Stop-Process -Id $_.Id -Force }
            Invoke-Build (Join-Path $Src 'BurakBim.Server\BurakBim.Server.csproj') @('RuntimeIdentifier=win-x64', 'SelfContained=true', 'PublishSingleFile=true', 'IncludeNativeLibrariesForSelfExtract=true', 'DebugType=none') (Join-Path $InstallDir 'server') -Publish
            $built = Test-Path $ServerExe
        } catch { Write-Warn2 ("Server build: " + $_.Exception.Message) }
    }
    if ($built) { Write-Ok "Server: $ServerExe" }
    else {
        $pre = Join-Path $setup 'prebuilt\server'
        if (Test-Path $pre) {
            Copy-Item "$pre\*" (Join-Path $InstallDir 'server') -Recurse -Force
            $dn = $dotnet
            if (-not $dn) { $c = Get-Command dotnet -ErrorAction SilentlyContinue; if ($c) { $dn = $c.Source } }
            if ($dn) {
                $ServerCommand = $dn; $ServerArgs = @((Join-Path $InstallDir 'server\BurakBim.Server.dll'))
                Write-Warn2 'Using the prebuilt portable server (UI-automation tools disabled). Re-run the installer with internet access for the full build.'
            } else { Write-Warn2 'Server could not be built and no .NET runtime was found. Install .NET 8 and re-run.' }
        }
    }
} else {
    Write-Ok 'Server already installed'
}

# ------------------------------------------------------------------ bridges
$bundleRoot = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins'
$builtBridges = @()
$toBuild = $bridgeTargets
if ($Rescan) { $toBuild = $new }
if ($toBuild.Count -gt 0) { Write-Step 'Building and registering bridges for each detected version' }

function Get-RunningFamily([string]$Family) {
    $n = switch ($Family) { 'revit' { 'Revit' } 'autocad' { 'acad' } 'navisworks' { 'Roamer' } }
    return @(Get-Process $n -ErrorAction SilentlyContinue)
}

foreach ($t in $toBuild) {
    $out = Join-Path $InstallDir "bridges\$($t.Family)\$($t.Year)"
    if ((Get-RunningFamily $t.Family).Count -gt 0) { Write-Warn2 "$($t.Family) is running: close it if the $($t.Year) bridge files are locked. Restart it afterwards to load BURAK BIM." }
    $ok = $false
    # 1) prebuilt bridge for this exact host/year (compiled in CI against that year's API)
    $pre = Join-Path $setup "prebuilt\$($t.Family)\$($t.Year)"
    if (-not $LocalBuild -and (Test-Path (Join-Path $pre "BurakBim.Bridge.*.dll"))) {
        try {
            New-Item -ItemType Directory -Force -Path $out | Out-Null
            Copy-Item "$pre\*" $out -Recurse -Force
            $shared = Join-Path $setup 'prebuilt\_shared'
            if (Test-Path $shared) { Copy-Item $shared (Join-Path $InstallDir 'bridges') -Recurse -Force }
            Unblock-Tree $out; Unblock-Tree (Join-Path $InstallDir 'bridges\_shared')
            $ok = $true
            Write-Ok "$($t.Key): prebuilt bridge installed"
        } catch { Write-Warn2 ("$($t.Key): prebuilt copy failed (close the application if files are locked): " + $_.Exception.Message) }
    }
    # 2) offline compile with the bundled C# compiler against the installed product (no internet needed)
    if (-not $ok) {
        try { if (Invoke-OfflineBridgeBuild $t $out) { $ok = $true; Write-Ok "$($t.Key): bridge compiled on this PC (offline compiler)" } }
        catch { Write-Info ("$($t.Key): offline compile not possible: " + $_.Exception.Message) }
    }
    # 3) .NET SDK build (downloads the SDK once if allowed)
    if (-not $ok -and -not $dotnet) { try { $dotnet = Get-Dotnet } catch { Write-Warn2 ("No .NET SDK: " + $_.Exception.Message) } }
    if (-not $ok -and $dotnet) {
        try {
            switch ($t.Family) {
                'revit' { Invoke-Build (Join-Path $Src 'BurakBim.Bridge.Revit\BurakBim.Bridge.Revit.csproj') @("RevitYear=$($t.Year)", "RevitDir=$($t.Dir)") $out }
                'autocad' { Invoke-Build (Join-Path $Src 'BurakBim.Bridge.AutoCAD\BurakBim.Bridge.AutoCAD.csproj') @("AcadYear=$($t.Year)", "AcadDir=$($t.Dir)") $out }
                'navisworks' { Invoke-Build (Join-Path $Src 'BurakBim.Bridge.Navisworks\BurakBim.Bridge.Navisworks.csproj') @("NavisYear=$($t.Year)", "NavisDir=$($t.Dir)") $out }
            }
            $ok = $true
        } catch { Write-Warn2 ("$($t.Key): " + $_.Exception.Message) }
    }
    if (-not $ok) { Write-Warn2 "$($t.Key): no bridge could be installed. This version still works through capture + computer control; re-run setup with internet access to compile it."; continue }
    $builtBridges += $t.Key

    switch ($t.Family) {
        'revit' {
            $addinDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$($t.Year)"
            New-Item -ItemType Directory -Force -Path $addinDir | Out-Null
            $dll = Join-Path $out 'BurakBim.Bridge.Revit.dll'
            $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BURAK BIM</Name>
    <Assembly>$([Security.SecurityElement]::Escape($dll))</Assembly>
    <AddInId>$RevitAddInId</AddInId>
    <FullClassName>BurakBim.Bridge.Revit.RevitApp</FullClassName>
    <VendorId>BURAKBIM</VendorId>
    <VendorDescription>BURAK BIM - AI connector for Revit</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
            Write-Utf8 (Join-Path $addinDir 'BurakBim.addin') $xml
            Write-Ok "Revit $($t.Year): add-in registered (BURAK BIM tab)"
        }
        'autocad' {
            # Autoloader bundle = trusted location (no SECURELOAD prompt); works for every AutoCAD vertical.
            $bundle = Join-Path $bundleRoot 'BurakBim.AutoCAD.bundle'
            $contents = Join-Path $bundle "Contents\$($t.Year)"
            New-Item -ItemType Directory -Force -Path $contents | Out-Null
            Copy-Item (Join-Path $out 'BurakBim.Bridge.AutoCAD.dll') $contents -Force
            Write-Utf8 (Join-Path $contents 'burakbim.probe') $out
            Write-Ok "AutoCAD-family $($t.Year): bridge staged"
        }
        'navisworks' {
            $bundle = Join-Path $bundleRoot 'BurakBim.Navisworks.bundle'
            $contents = Join-Path $bundle "Contents\$($t.Year)"
            New-Item -ItemType Directory -Force -Path $contents | Out-Null
            Copy-Item (Join-Path $out 'BurakBim.Bridge.Navisworks.dll') $contents -Force
            Write-Utf8 (Join-Path $contents 'burakbim.probe') $out
            Write-Ok "Navisworks $($t.Year): plug-in staged"
        }
    }
}

# PackageContents.xml for the bundles, generated from ALL connected versions
$allBridges = @($haveBridges + $builtBridges | Select-Object -Unique)
$acadComponents = ''
foreach ($p in $products | Where-Object { $_.Family -eq 'autocad' -and $allBridges -contains "autocad-$($_.Year)" } | Sort-Object Release -Unique) {
    $acadComponents += @"
  <Components Description="$($p.Release)">
    <RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="$($p.Release)" SeriesMax="$($p.Release)" />
    <ComponentEntry AppName="BurakBim$($p.Year)" ModuleName="./Contents/$($p.Year)/BurakBim.Bridge.AutoCAD.dll" AppDescription="BURAK BIM AI connector" LoadOnAutoCADStartup="True" />
  </Components>

"@
}
if ($acadComponents) {
    Write-Utf8 (Join-Path $bundleRoot 'BurakBim.AutoCAD.bundle\PackageContents.xml') @"
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage SchemaVersion="1.0" AutodeskProduct="AutoCAD" ProductType="Application" Name="BURAK BIM" Description="BURAK BIM - AI connector for AutoCAD and AutoCAD-based products" AppVersion="$BurakVersion" Author="BURAK BIM" ProductCode="{5F2C9A41-7B3E-4D2A-9C61-1E8B0D4F7A22}" UpgradeCode="{0B7E4D19-3A6C-4F58-8E2D-6C1A9F3B5D70}">
  <CompanyDetails Name="BURAK BIM" />
$acadComponents</ApplicationPackage>
"@
    Write-Ok 'AutoCAD autoloader bundle written (loads in AutoCAD, Civil 3D, Plant 3D, Map 3D, MEP, Advance Steel...)'
}
$navComponents = ''
$series = @{ 2022 = 'Nw19'; 2023 = 'Nw20'; 2024 = 'Nw21'; 2025 = 'Nw22'; 2026 = 'Nw23'; 2027 = 'Nw24' }
foreach ($y in ($allBridges | Where-Object { $_ -like 'navisworks-*' } | ForEach-Object { [int]($_ -replace 'navisworks-', '') } | Sort-Object -Unique)) {
    foreach ($plat in 'NAVMAN', 'NAVSIM') {
        $navComponents += @"
  <Components>
    <RuntimeRequirements OS="Win64" Platform="$plat" SeriesMin="$($series[$y])" SeriesMax="$($series[$y])" />
    <ComponentEntry AppName="BurakBim.Navisworks.$y.$plat" AppType="ManagedPlugin" ModuleName="./Contents/$y/BurakBim.Bridge.Navisworks.dll" />
  </Components>

"@
    }
}
if ($navComponents) {
    Write-Utf8 (Join-Path $bundleRoot 'BurakBim.Navisworks.bundle\PackageContents.xml') @"
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage SchemaVersion="1.0" AppVersion="$BurakVersion" AutodeskProduct="Navisworks" Name="BURAK BIM" Description="BURAK BIM - AI connector for Navisworks" ProductCode="{A3D1E6B2-58C4-4F0B-9E7A-2D4C6B8F1E33}">
  <CompanyDetails Name="BURAK BIM" />
$navComponents</ApplicationPackage>
"@
    Write-Ok 'Navisworks plug-in bundle written'
}

# ------------------------------------------------------------------ config + state
$cfg = Join-Path $InstallDir 'config\burakbim.json'
if (-not (Test-Path $cfg)) {
    Write-Utf8 $cfg @'
{
  "readOnly": false,
  "allowScripts": true,
  "allowComAutomation": true,
  "allowUiAutomation": true,
  "toolsets": ["auto"],
  "bridgeAutoStart": true,
  "requestTimeoutSeconds": 180,
  "maxPointsPerRequest": 400000,
  "inspectorName": "",
  "company": "BURAK BIM"
}
'@
}
$stateObj = [ordered]@{
    version = $BurakVersion; updated = (Get-Date).ToString('s'); installDir = $InstallDir
    bridges = $allBridges
    products = @($products | ForEach-Object { [ordered]@{ family = $_.Family; name = $_.Name; year = $_.Year; dir = $_.Dir } })
    server = [ordered]@{ command = $ServerCommand; args = $ServerArgs }
}
Write-Utf8 $StateFile ($stateObj | ConvertTo-Json -Depth 6)

# ------------------------------------------------------------------ AI clients
function Set-JsonMcpEntry([string]$Path, [string]$Container, [hashtable]$Entry, [string]$Label) {
    $obj = $null
    if (Test-Path $Path) {
        Copy-Item $Path "$Path.bak" -Force
        $raw = Get-Content $Path -Raw
        if ($raw -and $raw.Trim()) {
            # tolerate // comments (JSONC)
            $clean = ($raw -split "`n" | Where-Object { $_ -notmatch '^\s*//' }) -join "`n"
            try { $obj = $clean | ConvertFrom-Json } catch { Write-Warn2 "$Label config is not valid JSON ($Path) - left unchanged."; return }
        }
    }
    if (-not $obj) { $obj = New-Object PSObject }
    if (-not ($obj.PSObject.Properties.Name -contains $Container)) { $obj | Add-Member -NotePropertyName $Container -NotePropertyValue (New-Object PSObject) }
    $servers = $obj.$Container
    if ($servers.PSObject.Properties.Name -contains $ServerName) { $servers.PSObject.Properties.Remove($ServerName) }
    $servers | Add-Member -NotePropertyName $ServerName -NotePropertyValue ([pscustomobject]$Entry)
    Write-Utf8 $Path ($obj | ConvertTo-Json -Depth 32)
    try { Get-Content $Path -Raw | ConvertFrom-Json | Out-Null } catch { Copy-Item "$Path.bak" $Path -Force; Write-Warn2 "$Label config write failed verification - restored backup."; return }
    Write-Ok "$Label -> $Path"
}

function Set-CodexEntry([string]$Path) {
    $cmd = $ServerCommand
    $argList = ($ServerArgs | ForEach-Object { "'" + $_ + "'" }) -join ', '
    $block = "[mcp_servers.$ServerName]`r`ncommand = '$cmd'`r`nargs = [$argList]`r`nstartup_timeout_sec = 30`r`ntool_timeout_sec = 600`r`n"
    $text = ''
    if (Test-Path $Path) { Copy-Item $Path "$Path.bak" -Force; $text = Get-Content $Path -Raw }
    if (-not $text) { $text = '' }
    $pattern = "(?ms)^\[mcp_servers\.$([regex]::Escape($ServerName))\]\s*.*?(?=^\[|\z)"
    if ($text -match $pattern) { $text = [regex]::Replace($text, $pattern, $block + "`r`n") }
    else { $text = $text.TrimEnd() + "`r`n`r`n" + $block }
    Write-Utf8 $Path $text
    Write-Ok "Codex -> $Path"
}

$clientsDone = $false
if ((-not $Rescan) -and $Clients -ne 'none' -and (Test-Path $ServerExe) -and $ServerArgs.Count -eq 0) {
    # 1.3: done by the server itself (strict JSON without BOM, verified, backups *.burakbim.bak)
    Write-Step "Connecting AI clients ($Clients)"
    $env:BURAK_BIM_HOME = $InstallDir
    $out = & $ServerExe --home $InstallDir --connect-clients $Clients 2>&1
    $out | ForEach-Object { Write-Info "$_" }
    if ($LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq 2) { $clientsDone = $true }
    Write-Info 'Restart each AI app (fully quit Claude Desktop from the tray icon). Claude Desktop users can also double-click BURAK-BIM.mcpb.'
}
if ((-not $Rescan) -and $Clients -ne 'none' -and -not $clientsDone) {
    Write-Step "Connecting AI clients ($Clients)"
    $want = @(); if ($Clients -ne 'auto') { $want = $Clients.Split(',') | ForEach-Object { $_.Trim().ToLower() } }
    function Want([string]$name, [bool]$detected) { if ($Clients -eq 'auto') { return $detected } return ($want -contains $name) }
    $entry = @{ command = $ServerCommand; args = $ServerArgs; env = @{ BURAK_BIM_HOME = $InstallDir } }
    $home2 = $env:USERPROFILE

    # Claude Desktop (classic + Microsoft Store/MSIX)
    $cdPaths = @()
    $classic = Join-Path $env:APPDATA 'Claude'
    if (Test-Path $classic) { $cdPaths += (Join-Path $classic 'claude_desktop_config.json') }
    Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter 'Claude_*' -ErrorAction SilentlyContinue | ForEach-Object {
        $p = Join-Path $_.FullName 'LocalCache\Roaming\Claude'
        if (Test-Path $p) { $cdPaths += (Join-Path $p 'claude_desktop_config.json') }
    }
    if (Want 'claude-desktop' ($cdPaths.Count -gt 0)) {
        if ($cdPaths.Count -eq 0) { $cdPaths = @((Join-Path $classic 'claude_desktop_config.json')) }
        foreach ($p in $cdPaths) { Set-JsonMcpEntry $p 'mcpServers' $entry 'Claude Desktop' }
        Write-Info 'Fully quit Claude Desktop (also from the tray) and start it again to load BURAK BIM.'
    }
    # Claude Code
    $claudeCli = Get-Command claude -ErrorAction SilentlyContinue
    if (Want 'claude-code' ([bool]$claudeCli -or (Test-Path (Join-Path $home2 '.claude.json')))) {
        if ($claudeCli) {
            $json = (@{ type = 'stdio'; command = $ServerCommand; args = $ServerArgs; env = @{ BURAK_BIM_HOME = $InstallDir } } | ConvertTo-Json -Compress -Depth 5)
            & $claudeCli.Source mcp remove $ServerName --scope user *> $null
            & $claudeCli.Source mcp add-json $ServerName $json --scope user *> $null
            if ($LASTEXITCODE -eq 0) { Write-Ok 'Claude Code (user scope)' } else { Set-JsonMcpEntry (Join-Path $home2 '.claude.json') 'mcpServers' $entry 'Claude Code' }
        } else { Set-JsonMcpEntry (Join-Path $home2 '.claude.json') 'mcpServers' $entry 'Claude Code' }
    }
    if (Want 'codex' (Test-Path (Join-Path $home2 '.codex'))) { Set-CodexEntry (Join-Path $home2 '.codex\config.toml') }
    if (Want 'cursor' (Test-Path (Join-Path $home2 '.cursor'))) { Set-JsonMcpEntry (Join-Path $home2 '.cursor\mcp.json') 'mcpServers' $entry 'Cursor' }
    if (Want 'windsurf' (Test-Path (Join-Path $home2 '.codeium\windsurf'))) { Set-JsonMcpEntry (Join-Path $home2 '.codeium\windsurf\mcp_config.json') 'mcpServers' $entry 'Windsurf' }
    if (Want 'antigravity' (Test-Path (Join-Path $home2 '.gemini\antigravity'))) { Set-JsonMcpEntry (Join-Path $home2 '.gemini\antigravity\mcp_config.json') 'mcpServers' $entry 'Google Antigravity' }
    if (Want 'gemini' (Test-Path (Join-Path $home2 '.gemini\settings.json'))) { Set-JsonMcpEntry (Join-Path $home2 '.gemini\settings.json') 'mcpServers' $entry 'Gemini CLI' }
    $vsUser = Join-Path $env:APPDATA 'Code\User'
    if (Want 'vscode' (Test-Path $vsUser)) {
        $vsEntry = @{ type = 'stdio'; command = $ServerCommand; args = $ServerArgs; env = @{ BURAK_BIM_HOME = $InstallDir } }
        Set-JsonMcpEntry (Join-Path $vsUser 'mcp.json') 'servers' $vsEntry 'VS Code (Copilot agent)'
    }
    Write-Utf8 (Join-Path $InstallDir 'config\mcp-client-snippet.json') ((@{ mcpServers = @{ $ServerName = $entry } }) | ConvertTo-Json -Depth 6)
    Write-Info "Any other MCP client: use config\mcp-client-snippet.json (stdio, command = $ServerCommand)."
}

# ------------------------------------------------------------------ sentinel + uninstall entry
if ((-not $Rescan) -and (-not ($Sentinel -and -not $NoSentinel))) {
    try { Unregister-ScheduledTask -TaskName 'BURAK BIM Sentinel' -Confirm:$false -ErrorAction Stop; Write-Ok 'Old logon task removed' } catch { }
}
if ((-not $Rescan) -and $Sentinel -and (-not $NoSentinel)) {
    Write-Step 'Installing BURAK BIM Sentinel (auto-connects Autodesk versions installed later)'
    try {
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ("-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"" + (Join-Path $setup 'install.ps1') + "`" -Rescan -Quiet -InstallDir `"$InstallDir`"")
        $trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 1) -StartWhenAvailable
        Register-ScheduledTask -TaskName 'BURAK BIM Sentinel' -Action $action -Trigger $trigger -Settings $settings -Description 'BURAK BIM: detect newly installed Autodesk versions and connect them.' -Force | Out-Null
        Write-Ok 'Sentinel scheduled at logon'
    } catch { Write-Warn2 ("Sentinel not installed: " + $_.Exception.Message) }

    $un = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BURAK BIM'
    New-Item -Path $un -Force | Out-Null
    Set-ItemProperty $un DisplayName 'BURAK BIM'
    Set-ItemProperty $un DisplayVersion $BurakVersion
    Set-ItemProperty $un Publisher 'BURAK BIM'
    Set-ItemProperty $un InstallLocation $InstallDir
    Set-ItemProperty $un UninstallString ("powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"" + (Join-Path $setup 'uninstall.ps1') + "`"")
    $ico = Join-Path $setup 'assets\burakbim.ico'
    if (Test-Path $ico) { Set-ItemProperty $un DisplayIcon $ico }
    New-ItemProperty $un -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty $un -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
}

# ------------------------------------------------------------------ verify
Write-Step 'Verifying'
$ErrorActionPreference = 'Continue'
if (Test-Path $ServerExe) {
    $doc = & $ServerExe --doctor --home $InstallDir 2>&1
    if (-not $Quiet) { $doc | ForEach-Object { Write-Info $_ } }
    # the exact handshake every AI client performs
    try {
        $req = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","clientInfo":{"name":"burak-installer","version":"1"},"capabilities":{}}}' + "`n" +
               '{"jsonrpc":"2.0","method":"notifications/initialized"}' + "`n" + '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' + "`n"
        $psi = New-Object Diagnostics.ProcessStartInfo $ServerExe
        $psi.Arguments = "--home `"$InstallDir`""; $psi.UseShellExecute = $false; $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $p = [Diagnostics.Process]::Start($psi)
        $p.StandardInput.Write($req); $p.StandardInput.Close()
        $outText = $p.StandardOutput.ReadToEnd(); [void]$p.WaitForExit(30000)
        if ($outText -match '"serverInfo"' -and $outText -match '"tools"') { Write-Ok 'MCP handshake OK - AI clients can start and talk to BURAK BIM' }
        else { Write-Warn2 'MCP handshake failed - see logs. Antivirus may be blocking BurakBim.Server.exe (allow it), then re-run setup.' }
    } catch { Write-Warn2 ("MCP handshake could not run: " + $_.Exception.Message) }
} elseif ($ServerArgs.Count -gt 0) {
    & $ServerCommand $ServerArgs[0] --doctor --home $InstallDir 2>&1 | ForEach-Object { Write-Info $_ }
}

Write-Host ""
if ($script:Problems.Count -eq 0) { Write-Host "BURAK BIM $BurakVersion installed in $InstallDir" -ForegroundColor Green }
else {
    Write-Host "BURAK BIM installed with $($script:Problems.Count) warning(s):" -ForegroundColor Yellow
    $script:Problems | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}
if (-not $Rescan) {
    Write-Host "Next: restart your AI client, open Revit / AutoCAD / Navisworks, then ask: 'Use BURAK BIM: burak_status'." -ForegroundColor Gray
}
try { Stop-Transcript | Out-Null } catch { }
if ($script:Problems.Count -gt 0) { exit 2 } else { exit 0 }

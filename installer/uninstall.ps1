#Requires -Version 5.1
<#
.SYNOPSIS
  Remove BURAK BIM: Revit add-ins, AutoCAD/Navisworks bundles, AI-client entries, sentinel task,
  server, bridges and build tools. Audit trail, reports, config and logs are KEPT (inspector records)
  unless -Purge is given.

.PARAMETER InstallDir  Defaults to the registered install folder.
.PARAMETER Purge       Also delete audit trail, reports, exports, config and logs (whole install folder).
.PARAMETER KeepClients Do not touch AI-client configuration files.
.PARAMETER Yes         No confirmation prompt.
.PARAMETER WhatIf      Show the plan only.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InstallDir,
    [switch]$Purge,
    [switch]$KeepClients,
    [switch]$Yes
)
$ErrorActionPreference = 'Continue'
$ServerName = 'BURAK-BIM'
$RevitAddInId = '8E3B2F7A-4C1D-4B9E-A2F5-6D7C8B9A0E1F'

if (-not $InstallDir) {
    $reg = Get-ItemProperty 'HKCU:\Software\BURAK BIM' -Name InstallDir -ErrorAction SilentlyContinue
    if ($reg) { $InstallDir = $reg.InstallDir } else { $InstallDir = 'D:\BURAK BIM' }
}

Write-Host "BURAK BIM uninstall - $InstallDir" -ForegroundColor Cyan
$plan = @(
    'Revit add-in manifests (BurakBim.addin, all years 2022-2027)',
    'Autodesk bundles: BurakBim.AutoCAD.bundle, BurakBim.Navisworks.bundle',
    'Scheduled task "BURAK BIM Sentinel"',
    'Registry: HKCU\Software\BURAK BIM and the Apps & Features entry'
)
if (-not $KeepClients) { $plan += "AI client entries named '$ServerName' (backups *.bak are written first)" }
if ($Purge) { $plan += "DELETE the whole folder $InstallDir (audit trail, reports, config, logs included)" }
else { $plan += "Delete server, bridges, tools, cache, setup, runtime from $InstallDir (KEEP audit, reports, exports, config, logs, toolpacks)" }
$plan | ForEach-Object { Write-Host "  - $_" }
if ($WhatIfPreference) { return }
if (-not $Yes) {
    $a = Read-Host 'Proceed? (y/N)'
    if ($a -notmatch '^(y|yes)$') { Write-Host 'Aborted.'; return }
}

$busy = @(Get-Process Revit, acad, Roamer -ErrorAction SilentlyContinue)
if ($busy.Count -gt 0) { Write-Host "  [!] Close Revit / AutoCAD / Navisworks first if files are locked: $($busy.ProcessName -join ', ')" -ForegroundColor Yellow }
Get-Process BurakBim.Server -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# Revit manifests (only ours - matched by AddInId)
foreach ($y in 2022..2027) {
    foreach ($root in @((Join-Path $env:APPDATA "Autodesk\Revit\Addins\$y"))) {
        Get-ChildItem $root -Filter *.addin -ErrorAction SilentlyContinue | ForEach-Object {
            if ((Get-Content $_.FullName -Raw) -match $RevitAddInId) { Remove-Item $_.FullName -Force; Write-Host "  removed $($_.FullName)" }
        }
    }
}
foreach ($b in 'BurakBim.AutoCAD.bundle', 'BurakBim.Navisworks.bundle') {
    $p = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\$b"
    if (Test-Path $p) { Remove-Item $p -Recurse -Force; Write-Host "  removed $p" }
}
Unregister-ScheduledTask -TaskName 'BURAK BIM Sentinel' -Confirm:$false -ErrorAction SilentlyContinue

function Remove-JsonEntry([string]$Path, [string]$Container) {
    if (-not (Test-Path $Path)) { return }
    try {
        $obj = Get-Content $Path -Raw | ConvertFrom-Json
        if ($obj.$Container -and ($obj.$Container.PSObject.Properties.Name -contains $ServerName)) {
            Copy-Item $Path "$Path.bak" -Force
            $obj.$Container.PSObject.Properties.Remove($ServerName)
            [IO.File]::WriteAllText($Path, ($obj | ConvertTo-Json -Depth 32), (New-Object Text.UTF8Encoding($false)))
            Write-Host "  removed $ServerName from $Path"
        }
    } catch { Write-Host "  [!] could not edit $Path : $($_.Exception.Message)" -ForegroundColor Yellow }
}
if (-not $KeepClients) {
    $h = $env:USERPROFILE
    Remove-JsonEntry (Join-Path $env:APPDATA 'Claude\claude_desktop_config.json') 'mcpServers'
    Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter 'Claude_*' -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-JsonEntry (Join-Path $_.FullName 'LocalCache\Roaming\Claude\claude_desktop_config.json') 'mcpServers' }
    $claude = Get-Command claude -ErrorAction SilentlyContinue
    if ($claude) { & $claude.Source mcp remove $ServerName --scope user *> $null }
    Remove-JsonEntry (Join-Path $h '.claude.json') 'mcpServers'
    Remove-JsonEntry (Join-Path $h '.cursor\mcp.json') 'mcpServers'
    Remove-JsonEntry (Join-Path $h '.codeium\windsurf\mcp_config.json') 'mcpServers'
    Remove-JsonEntry (Join-Path $h '.gemini\antigravity\mcp_config.json') 'mcpServers'
    Remove-JsonEntry (Join-Path $h '.gemini\settings.json') 'mcpServers'
    Remove-JsonEntry (Join-Path $env:APPDATA 'Code\User\mcp.json') 'servers'
    $codex = Join-Path $h '.codex\config.toml'
    if (Test-Path $codex) {
        $t = Get-Content $codex -Raw
        $pattern = "(?ms)^\[mcp_servers\.$([regex]::Escape($ServerName))\]\s*.*?(?=^\[|\z)"
        if ($t -match $pattern) { Copy-Item $codex "$codex.bak" -Force; [IO.File]::WriteAllText($codex, [regex]::Replace($t, $pattern, ''), (New-Object Text.UTF8Encoding($false))); Write-Host "  removed $ServerName from $codex" }
    }
}

Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BURAK BIM' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\BURAK BIM' -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path $InstallDir) {
    if ($Purge) {
        Set-Location $env:TEMP
        Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  deleted $InstallDir"
    } else {
        foreach ($sub in 'server', 'bridges', 'tools', 'cache', 'runtime', 'scratch', 'captures') {
            $p = Join-Path $InstallDir $sub
            if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue }
        }
        Remove-Item (Join-Path $InstallDir 'burakbim.root') -Force -ErrorAction SilentlyContinue
        # setup last (this script may run from it)
        $setup = Join-Path $InstallDir 'setup'
        if ($PSScriptRoot -ne $setup) { Remove-Item $setup -Recurse -Force -ErrorAction SilentlyContinue }
        else { Start-Process cmd.exe -ArgumentList "/c timeout /t 3 >nul & rmdir /s /q `"$setup`"" -WindowStyle Hidden }
        Write-Host "  kept audit\, reports\, exports\, config\, logs\, toolpacks\ in $InstallDir (use -Purge to delete)"
    }
}
Write-Host 'BURAK BIM removed. Restart your AI clients.' -ForegroundColor Green

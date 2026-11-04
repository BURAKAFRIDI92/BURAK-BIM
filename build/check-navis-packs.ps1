#Requires -Version 5.1
<#
.SYNOPSIS
  Compiles every Navisworks tool-pack snippet against one year's real API, so an API that Autodesk changed
  fails here instead of on a user's PC.

.DESCRIPTION
  Each snippet becomes a method of a class deriving from the bridge's NavisScriptGlobals. Snippets are normally
  compiled as C# scripts at run time, so two script-only differences are ignored: CS0161 (a script may end without
  return) and CS0246/CS0234 for the TimeLiner and COM namespaces when that year's reference for them is not available.

.PARAMETER Csc       Path to csc.exe.
.PARAMETER Server    Path to BurakBim.Server.exe (provides the snippets with --packs-json).
.PARAMETER Bridge    Path to that year's BurakBim.Bridge.Navisworks.dll.
.PARAMETER ApiDir    Folder with that year's Autodesk.Navisworks.*.dll.
.PARAMETER Label     Name for the report, e.g. the year.
#>
param(
    [Parameter(Mandatory)][string]$Csc,
    [Parameter(Mandatory)][string]$Server,
    [Parameter(Mandatory)][string]$Bridge,
    [Parameter(Mandatory)][string]$ApiDir,
    [Parameter(Mandatory)][string]$Label
)

$ErrorActionPreference = 'Stop'
$work = Join-Path ([IO.Path]::GetTempPath()) "navis-packs-$Label"
New-Item -ItemType Directory -Force -Path $work | Out-Null

# assigned first: Windows PowerShell 5.1 does not unroll a JSON array inside a pipeline
$all = (& $Server --packs-json) -join "`n" | ConvertFrom-Json
$tools = @($all | Where-Object { $_.host -eq 'navisworks' })
$hasTimeliner = Test-Path (Join-Path $ApiDir 'Autodesk.Navisworks.Timeliner.dll')
$hasClash = Test-Path (Join-Path $ApiDir 'Autodesk.Navisworks.Clash.dll')

$sb = New-Object Text.StringBuilder
foreach ($u in 'System', 'System.Linq', 'System.Collections.Generic', 'Autodesk.Navisworks.Api') { [void]$sb.AppendLine("using $u;") }
if ($hasClash) { [void]$sb.AppendLine('using Autodesk.Navisworks.Api.Clash;') }
if ($hasTimeliner) { [void]$sb.AppendLine('using Autodesk.Navisworks.Api.Timeliner;') }
$i = 0
foreach ($t in $tools) {
    $i++
    [void]$sb.AppendLine("public class P$i : BurakBim.Bridge.Navisworks.NavisScriptGlobals { public object Run() {")
    [void]$sb.AppendLine("#line 1 `"$($t.name)`"")
    [void]$sb.AppendLine($t.code)
    [void]$sb.AppendLine('#line default')
    [void]$sb.AppendLine('} }')
}
$source = Join-Path $work 'packs.cs'
[IO.File]::WriteAllText($source, $sb.ToString())

$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @($Bridge) + @(Get-ChildItem $ApiDir -Filter 'Autodesk.Navisworks.*.dll' | ForEach-Object { $_.FullName })
foreach ($n in 'mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Data', 'Microsoft.CSharp', 'System.Drawing', 'System.Windows.Forms', 'netstandard', 'System.Runtime', 'WPF\WindowsBase', 'WPF\PresentationCore', 'WPF\PresentationFramework') {
    $f = Join-Path $fx "$n.dll"
    if (Test-Path $f) { $refs += $f }
}
$rsp = Join-Path $work 'packs.rsp'
$lines = @('/nologo', '/target:library', '/platform:x64', '/nostdlib+', '/langversion:latest', '/nowarn:CS0162,CS0168,CS0219,CS0618,CS1701,CS1702,CS8632', "/out:`"$(Join-Path $work 'packs.dll')`"")
$lines += $refs | ForEach-Object { "/reference:`"$_`"" }
$lines += "`"$source`""
[IO.File]::WriteAllLines($rsp, $lines)

# /noconfig only works on the command line: inside a response file the compiler ignores it
$output = & $Csc /noconfig "@$rsp" 2>&1 | ForEach-Object { "$_" }
$errors = @($output | Where-Object { $_ -match ': error CS' })
$hasComApi = Test-Path (Join-Path $ApiDir 'Autodesk.Navisworks.ComApi.dll')
$ignored = @($errors | Where-Object { $_ -match 'error CS0161' -or (-not $hasTimeliner -and $_ -match 'error CS02(46|34).*Timeliner') -or (-not $hasComApi -and $_ -match 'error CS02(46|34).*ComApi') })
$real = @($errors | Where-Object { $ignored -notcontains $_ })
Write-Host "Navisworks tools $Label : $($tools.Count) tools, $($real.Count) errors"
$real | ForEach-Object { $_ -replace '^.*\\', '' } | Select-Object -Unique -First 40 | ForEach-Object { Write-Host "  PACK $Label $_" }
if ($errors.Count -eq 0 -and $output.Count -gt 0) { $output | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" } }
exit ($(if ($real.Count -gt 0) { 1 } else { 0 }))

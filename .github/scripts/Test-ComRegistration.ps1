<#
.SYNOPSIS
    Fails when the installer's authored COM/SOLIDWORKS registry rows drift from the assembly.

.DESCRIPTION
    Package.wxs authors every COM registration row by hand (no regasm at install time), so a
    changed [Guid], ProgId, class name or AssemblyVersion in code silently leaves the MSI
    registering a class that no longer exists - SOLIDWORKS then fails to load the add-in, and
    nothing in the build notices. This script closes that gap:

      1. `regasm /regfile /codebase` against the merged DLL (writes a .reg file only - no
         registry access, no admin rights) produces the ground truth for the HKCR rows.
      2. Every HKCR RegistryValue/RegistryKey in Package.wxs must match that output exactly
         (case-insensitive, CodeBase normalized to the installer's [INSTALLFOLDER] form), and
         vice versa.
      3. Every GUID under SOFTWARE\SOLIDWORKS\ in Package.wxs (AddIns, AddInsStartup) must be
         the SwAddin CLSID regasm reported - [ComRegisterFunction] output is not part of a
         regfile, so this is checked separately.

.PARAMETER DllPath
    The merged GladosSwAddin.dll to compare against (post-ILRepack).

.PARAMETER WxsPath
    The installer source to check.
#>
param(
    [Parameter(Mandatory = $true)] [string] $DllPath,
    [Parameter(Mandatory = $true)] [string] $WxsPath
)

$ErrorActionPreference = 'Stop'

$regasm = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe'
$regFile = Join-Path ([System.IO.Path]::GetTempPath()) ("GladosSwAddin-{0}.reg" -f [guid]::NewGuid())
$codeBase = 'file:///[INSTALLFOLDER]GladosSwAddin.dll'

& $regasm (Resolve-Path $DllPath).Path /codebase "/regfile:$regFile" /nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "regasm /regfile failed with exit code $LASTEXITCODE" }

# --- Expected rows, from the regfile. One entry per value ("key|name=value", name '@' for the
# default value) plus one "key|<key>" entry per key that carries no values at all.
$expected = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$addinClsid = $null
$currentKey = $null
$currentKeyHasValues = $false

function Close-Key {
    if ($script:currentKey -and -not $script:currentKeyHasValues) {
        [void]$expected.Add("$($script:currentKey)|<key>")
    }
}

foreach ($line in Get-Content $regFile) {
    if ($line -match '^\[HKEY_CLASSES_ROOT\\(.+)\]$') {
        Close-Key
        $currentKey = $Matches[1]
        $currentKeyHasValues = $false
    }
    elseif ($line -match '^(@|"([^"]+)")="(.*)"$') {
        $name = if ($Matches[1] -eq '@') { '@' } else { $Matches[2] }
        $value = $Matches[3] -replace '\\\\', '\'
        if ($name -eq 'CodeBase') { $value = $codeBase }
        if ($currentKey -eq 'GladosSwAddin.SwAddin\CLSID' -and $name -eq '@') { $addinClsid = $value }
        [void]$expected.Add("$currentKey|$name=$value")
        $currentKeyHasValues = $true
    }
}
Close-Key
Remove-Item $regFile -Force

if (-not $addinClsid) { throw "regasm output has no GladosSwAddin.SwAddin CLSID - is SwAddin still [ComVisible]?" }

# --- Authored rows, from Package.wxs.
[xml]$wxs = Get-Content $WxsPath -Raw
$actual = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$problems = New-Object System.Collections.Generic.List[string]

foreach ($node in $wxs.SelectNodes("//*[local-name()='RegistryValue' or local-name()='RegistryKey']")) {
    $key = $node.GetAttribute('Key')

    if ($node.GetAttribute('Root') -eq 'HKCR') {
        if ($node.LocalName -eq 'RegistryKey') {
            [void]$actual.Add("$key|<key>")
        }
        else {
            $name = if ($node.HasAttribute('Name')) { $node.GetAttribute('Name') } else { '@' }
            [void]$actual.Add("$key|$name=$($node.GetAttribute('Value'))")
        }
    }
    elseif ($key -match '^SOFTWARE\\SOLIDWORKS\\[^\\]+\\(\{[0-9A-Fa-f-]+\})') {
        if ($Matches[1] -ne $addinClsid) {
            $problems.Add("SOLIDWORKS row '$($node.GetAttribute('Root'))\$key' uses $($Matches[1]), but SwAddin's CLSID is $addinClsid")
        }
    }
}

foreach ($row in $expected) { if (-not $actual.Contains($row)) { $problems.Add("missing from Package.wxs: HKCR\$row") } }
foreach ($row in $actual) { if (-not $expected.Contains($row)) { $problems.Add("not produced by regasm: HKCR\$row") } }

if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Output "::error file=$WxsPath::$_" }
    throw "Package.wxs COM registration does not match $DllPath ($($problems.Count) difference(s))"
}

Write-Output "Package.wxs COM registration matches the assembly ($($expected.Count) HKCR rows, SwAddin CLSID $addinClsid)."

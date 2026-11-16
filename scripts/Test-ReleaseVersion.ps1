<#
.SYNOPSIS
    Fails unless the checked-out tree is ready to be released as the given version.

.DESCRIPTION
    Checks that the three version attributes in GladosSwAddin/Properties/AssemblyInfo.cs carry
    the version (AssemblyVersion and AssemblyFileVersion as X.Y.Z.0, AssemblyInformationalVersion
    as X.Y.Z), that CHANGELOG.md has a section for it, and that the tag vX.Y.Z does not exist on
    origin yet. ci.yml's release-check job, release.yml's guard and scripts/Start-Release.ps1 all
    run this script, so they agree on what releasable means. It never changes git state.

.PARAMETER Version
    The version to check, X.Y.Z.
#>
param(
    [Parameter(Mandatory = $true)] [string] $Version
)

$ErrorActionPreference = 'Stop'

function Fail([string] $Message) {
    Write-Host "::error::$Message"
    exit 1
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') { Fail "'$Version' is not an X.Y.Z version" }

$root = Split-Path -Parent $PSScriptRoot
$assemblyInfo = Get-Content -Raw (Join-Path $root 'GladosSwAddin/Properties/AssemblyInfo.cs')

$expected = [ordered]@{
    AssemblyVersion              = "$Version.0"
    AssemblyFileVersion          = "$Version.0"
    AssemblyInformationalVersion = $Version
}
foreach ($attribute in $expected.Keys) {
    $match = [regex]::Match($assemblyInfo, "\[assembly: $attribute\(""([^""]+)""\)\]")
    if (-not $match.Success) { Fail "AssemblyInfo.cs has no $attribute attribute" }
    if ($match.Groups[1].Value -ne $expected[$attribute]) {
        Fail "AssemblyInfo.cs has $attribute $($match.Groups[1].Value), expected $($expected[$attribute])"
    }
}

$changelog = Get-Content -Raw (Join-Path $root 'CHANGELOG.md')
if ($changelog -notmatch "(?m)^## \[$([regex]::Escape($Version))\]") {
    Fail "CHANGELOG.md has no '## [$Version]' section"
}

git -C $root ls-remote --exit-code --tags origin "refs/tags/v$Version" | Out-Null
if ($LASTEXITCODE -eq 0) { Fail "tag v$Version already exists on origin" }
$global:LASTEXITCODE = 0

Write-Host "Ready to release $Version."

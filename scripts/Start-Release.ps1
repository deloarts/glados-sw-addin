<#
.SYNOPSIS
    Starts a release: branches release/X.Y.Z from an up-to-date main, bumps the version, opens
    the CHANGELOG section, commits and pushes.

.DESCRIPTION
    Sets the three version attributes in GladosSwAddin/Properties/AssemblyInfo.cs and the
    AssemblyVersion that Package.wxs restates in its hand-authored COM registry rows, then turns
    the [Unreleased] section of CHANGELOG.md into [X.Y.Z] - YYYY-MM-DD under a fresh empty
    [Unreleased] heading. Merging the release pull request into main is what tags and publishes
    the release (release.yml). Works in Windows PowerShell 5.1 and PowerShell 7.

.PARAMETER Version
    The version to release, X.Y.Z.
#>
param(
    [Parameter(Mandatory = $true)] [string] $Version
)

$ErrorActionPreference = 'Stop'

function Invoke-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed with exit code $LASTEXITCODE" }
}

# Rewrites a file through a transform while keeping its encoding (BOM or not) and line endings,
# which Visual Studio and .gitattributes both care about.
function Update-File([string] $Path, [scriptblock] $Transform) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [System.IO.File]::ReadAllText($Path)
    $updated = & $Transform $text
    [System.IO.File]::WriteAllText($Path, $updated, (New-Object System.Text.UTF8Encoding($hasBom)))
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "'$Version' is not an X.Y.Z version" }
$branch = "release/$Version"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (git status --porcelain) { throw 'working tree is not clean' }

Invoke-Git checkout main
Invoke-Git pull --ff-only

git show-ref --verify --quiet "refs/heads/$branch"
$localExists = $LASTEXITCODE -eq 0
git ls-remote --exit-code --heads origin $branch | Out-Null
$remoteExists = $LASTEXITCODE -eq 0
if ($localExists -or $remoteExists) { throw "branch $branch already exists" }
git ls-remote --exit-code --tags origin "refs/tags/v$Version" | Out-Null
if ($LASTEXITCODE -eq 0) { throw "tag v$Version already exists" }

Invoke-Git checkout -b $branch

$assemblyInfoPath = Join-Path $root 'GladosSwAddin/Properties/AssemblyInfo.cs'
$assemblyInfo = [System.IO.File]::ReadAllText($assemblyInfoPath)
$oldAssemblyVersion = [regex]::Match($assemblyInfo, '\[assembly: AssemblyVersion\("([^"]+)"\)\]').Groups[1].Value
if (-not $oldAssemblyVersion) { throw 'AssemblyInfo.cs has no AssemblyVersion attribute' }
$newAssemblyVersion = "$Version.0"

Update-File $assemblyInfoPath {
    param($text)
    $text = $text -replace '\[assembly: AssemblyVersion\("[^"]+"\)\]', "[assembly: AssemblyVersion(""$newAssemblyVersion"")]"
    $text = $text -replace '\[assembly: AssemblyFileVersion\("[^"]+"\)\]', "[assembly: AssemblyFileVersion(""$newAssemblyVersion"")]"
    $text -replace '\[assembly: AssemblyInformationalVersion\("[^"]+"\)\]', "[assembly: AssemblyInformationalVersion(""$Version"")]"
}

# The COM rows name the assembly version twice: in the Assembly value and as the versioned
# InprocServer32 subkey. Test-ComRegistration.ps1 fails the build if any of them is missed.
if ($oldAssemblyVersion -ne $newAssemblyVersion) {
    $old = [regex]::Escape($oldAssemblyVersion)
    Update-File (Join-Path $root 'GladosSwAddin.Installer/Package.wxs') {
        param($text)
        $text = $text -replace "GladosSwAddin, Version=$old,", "GladosSwAddin, Version=$newAssemblyVersion,"
        $text -replace "InprocServer32\\$old""", "InprocServer32\$newAssemblyVersion"""
    }
}

$changelogPath = Join-Path $root 'CHANGELOG.md'
$changelog = [System.IO.File]::ReadAllText($changelogPath)
if ($changelog -notmatch "(?m)^## \[$([regex]::Escape($Version))\]") {
    $newline = if ($changelog.Contains("`r`n")) { "`r`n" } else { "`n" }
    $date = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
    Update-File $changelogPath {
        param($text)
        ([regex] '(?m)^## \[Unreleased\](?=\r?$)').Replace($text, "## [Unreleased]$newline$newline## [$Version] - $date", 1)
    }
}

Invoke-Git add GladosSwAddin/Properties/AssemblyInfo.cs GladosSwAddin.Installer/Package.wxs CHANGELOG.md
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    Invoke-Git commit -m "release: $Version"
} else {
    Write-Host "Nothing to commit, main already carries $Version and its CHANGELOG section."
}

& (Join-Path $PSScriptRoot 'Test-ReleaseVersion.ps1') -Version $Version
if ($LASTEXITCODE -ne 0) { throw 'release check failed' }

Invoke-Git push -u origin $branch

Write-Host ''
Write-Host "Next: review CHANGELOG.md on $branch (state the supported backend versions), then open the PR into main:"
Write-Host "  gh pr create --base main --head $branch --title ""release: $Version"""
Write-Host "Merging it tags v$Version and publishes the GitHub Release."

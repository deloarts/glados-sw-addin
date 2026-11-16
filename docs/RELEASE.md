# release

- [release](#release)
  - [1 versioning policy](#1-versioning-policy)
  - [2 branches and release flow](#2-branches-and-release-flow)
    - [2.1 day-to-day work](#21-day-to-day-work)
    - [2.2 cutting a release](#22-cutting-a-release)
    - [2.3 what ci does at each step](#23-what-ci-does-at-each-step)
    - [2.4 when the release run fails](#24-when-the-release-run-fails)
  - [3 what a github release contains](#3-what-a-github-release-contains)
  - [4 building the msi locally](#4-building-the-msi-locally)
  - [5 install, upgrade, uninstall](#5-install-upgrade-uninstall)
    - [5.1 what uninstall leaves behind](#51-what-uninstall-leaves-behind)
  - [6 unsigned installer: smartscreen and the signing follow-up](#6-unsigned-installer-smartscreen-and-the-signing-follow-up)
  - [7 upgrading the solidworks interop assemblies](#7-upgrading-the-solidworks-interop-assemblies)

This document covers how `glados-sw-addin` versions and releases are cut. It
follows the same branch and release flow as `glados/docs/RELEASING.md`,
adapted to a single Windows MSI instead of a set of Docker images: there is
no test-server channel mechanism here, no OpenAPI contract to protect, and
nothing is published anywhere except a GitHub Release.

Development is trunk-based: `main` is the only long-lived branch, every
change reaches it as a squash-merged pull request, and `main` is always
releasable. A release is a pull request from `release/X.Y.Z` that only bumps
the version and the CHANGELOG. Merging it is the release: a workflow tags
the merge commit and publishes the GitHub Release, nobody pushes a tag by
hand. See [section 2](#2-branches-and-release-flow).

## 1 versioning policy

This add-in versions **independently** of `glados` (backend and frontend
share one version). It is a separate deliverable with its own release
cadence. Its only cross-repo obligation is to state, in every release, the
backend versions it was built and tested against. The backend's version
policy allows a minor to change or remove `pat` endpoints, so the statement
is a range of backend minors, not only a floor:

> Backend 0.1.x to 0.3.x

(the exact range is whatever the current release's CHANGELOG.md entry says;
the add-in talks only to the backend's `api/v1/pat/*` and `api/v1/auth/*`
surface, see `CLAUDE.md`). When a backend minor removes or changes something
the add-in uses, a new add-in release goes out with it.

At runtime the add-in compares the backend's `pat` contract (`GET api/v1/pat/version`) with `PatContract.Supported` in `GladosSwAddin/Api/PatContract.cs` after every connect and shows a non-blocking warning on a mismatch (see docs/DESIGN.md "Backend contract check"). A release that moves to a new backend minor updates that constant together with the CHANGELOG statement.

**Single version source:** `GladosSwAddin/Properties/AssemblyInfo.cs`,
specifically `AssemblyVersion`, `AssemblyFileVersion` (both `X.Y.Z.0`) and
`AssemblyInformationalVersion` (`X.Y.Z`), kept in lockstep (see the comment
on those attributes). Derived from it:

- The merged `GladosSwAddin.dll`'s own file version comes from
  `AssemblyFileVersion` at compile time (ILRepack preserves the primary
  assembly's version attributes across the merge, see `ILRepack.targets`).
- The MSI's `ProductVersion` is bound to that same file's version at build
  time (`!(bind.fileVersion.GladosSwAddinDll)` in `GladosSwAddin.Installer/Package.wxs`),
  never typed in separately, so the two can't drift apart.
- The MSI's file name (`GladosSwAddin-X.Y.Z.msi`) is derived from the same
  DLL by `GladosSwAddin.Installer/GladosSwAddin.Installer.wixproj`'s post-build rename.

The one place that restates it is `Package.wxs`'s hand-authored COM rows,
which name `AssemblyVersion` in their `Assembly` values and versioned
`InprocServer32` subkeys. `scripts/Start-Release.ps1` updates them together
with `AssemblyInfo.cs`, and CI's COM registration check fails if they ever
disagree.

The add-in's first release is `0.1.0`, and nothing is released before it.
It goes out together with or after the backend's first release, which is
also `0.1.0`.

Semver is used conventionally here (no shared `major.minor` constraint with
the other repos): **major** for a breaking change to the add-in's own
behavior or its supported backend range, **minor** for new functionality,
**patch** for fixes that change neither.

## 2 branches and release flow

### 2.1 day-to-day work

1. Branch from an up-to-date `main`: `git checkout main && git pull &&
   git checkout -b feat/something`.
2. Commit and push as often as you like. For a testable MSI before a pull
   request exists, run *Actions → CI → Run workflow* on the branch and
   install the artifact.
3. Open a pull request into `main`. CI runs on it; fix and push until it is
   green.
4. Squash-merge it. The pull request title becomes the single commit on
   `main`, and the branch is deleted. Nothing is released yet.

Unfinished work does not go to `main`, since everything on `main` ships with
the next release; keep it on its branch and rebase it onto `main` now and
then. A branch that lives through a release must be rebased onto `main`
before its pull request, because the release squash replaced the commits it
shares with `main`.

### 2.2 cutting a release

1. Run `scripts/Start-Release.ps1 X.Y.Z` (Windows PowerShell or PowerShell
   7, from the repository root). It refuses a dirty tree or an existing
   branch or tag, branches `release/X.Y.Z` from an up-to-date `main`, sets
   the three version attributes and the `Package.wxs` COM rows, turns the
   `[Unreleased]` section of `CHANGELOG.md` into `[X.Y.Z] - YYYY-MM-DD`,
   commits `release: X.Y.Z` and pushes the branch.
2. The push runs CI including `release-check` and uploads the candidate MSI
   as a workflow artifact. Install it and verify in SOLIDWORKS. Complete the
   CHANGELOG section, including the supported backend range (see
   [section 1](#1-versioning-policy)), and add small release fixes on the
   branch as needed; every push rebuilds the candidate.
3. Open a pull request into `main`. If `main` moved in the meantime, use
   "Update branch" on the pull request; the `main` ruleset refuses the merge
   until the branch is up to date, so the tagged commit is exactly what the
   candidate contained.
4. Squash-merge it. `release.yml` tags the merge commit `vX.Y.Z`, builds the
   add-in and the MSI from the tag and publishes the GitHub Release (see
   [section 3](#3-what-a-github-release-contains)).

The same sequence covers hotfixes: a `fix/*` pull request into `main`,
followed directly by `scripts/Start-Release.ps1` for the next patch.

### 2.3 what ci does at each step

- **On every pull request into `main` and on pushes to `release/**`:** `ci.yml` restores, builds Release (with `/p:RegisterForComInterop=false` passed explicitly, belt-and-braces alongside the csproj's own Release-only default, see `CLAUDE.md`'s "Commands"), verifies the merged `GladosSwAddin.dll` carries no `Newtonsoft.Json` assembly reference, builds and runs the `GladosSwAddin.Tests` unit tests (results uploaded as a `.trx` artifact), checks that every COM and SOLIDWORKS registry row in `Package.wxs` matches what `regasm /regfile` reports for that DLL (`.github/scripts/Test-ComRegistration.ps1`; a changed `[Guid]`, ProgId or `AssemblyVersion` fails here instead of shipping an MSI SOLIDWORKS can't load, and `release.yml` runs the same gate), builds the MSI, and uploads both as workflow artifacts. The `gitleaks` job scans the history for secrets. A red run does not close the PR; fix the issue, push again, CI re-runs. Neither trigger has a path filter. Every PR runs CI, documentation-only ones included, so its jobs can be required status checks (a check a path filter never starts would stay "pending" and block the merge), and every release branch push runs it, including the release commit itself, which touches only `CHANGELOG.md` when the version attributes already match. Work-in-progress branches never trigger it on push: a runner cannot exercise the add-in, so a run is a merge gate, not a commit gate.
- **On release branches only:** `release-check` runs
  `scripts/Test-ReleaseVersion.ps1` (the branch's version is in all three
  attributes, `CHANGELOG.md` has its section, the tag is still free) and
  verifies that the branch contains the current `main`. It is skipped for
  every other branch, which counts as passing.
- **On demand:** *Actions → CI → Run workflow* builds the same artifacts from
  any branch, the way to get a testable MSI before a PR exists.
- Both workflows pin the runner to `windows-2025-vs2026` (Visual Studio
  2026 / MSBuild 18; the .NET 10 SDK in `global.json` refuses an older
  MSBuild, so `windows-2022` cannot build the SDK-style test project) and
  the .NET SDK to `global.json` (`actions/setup-dotnet`) so a build behaves
  the same on every run instead of drifting with `windows-latest`. Every action is
  pinned to a full commit SHA (the version as a trailing comment), so a
  moved or compromised tag can't change what runs; `~/.nuget/packages`
  (the shared global NuGet cache, used by both the add-in's and the
  installer's `PackageReference` restores) is cached between runs to cut
  restore time.
- **On merging a release pull request:** `release.yml` runs three jobs:
  - `guard` accepts only a merged pull request from a `release/X.Y.Z`
    branch of this repository, reruns `Test-ReleaseVersion.ps1` on the
    merge commit, and checks that the release branch contained `main` as it
    was right before the merge. A failing guard tags and publishes nothing.
  - `tag` creates the annotated tag `vX.Y.Z` on the merge commit.
  - `build` repeats the CI build and its gates (unit tests, COM registration
    check) on exactly the binaries it publishes, computes `SHA256SUMS.txt`,
    attests build provenance (public repository only, see
    [section 3](#3-what-a-github-release-contains)), and creates the GitHub
    Release.

  Tagging and building run in one workflow because a tag pushed with the
  workflow token does not start other workflows. For the same reason a tag
  pushed by hand publishes nothing.
- **CodeQL (`codeql.yml`):** static security analysis of the C# code
  (from a real Release build) and of the workflow files, on PRs into and
  pushes to `main`, weekly, and on demand. Code scanning is free only for
  public repositories, so its jobs skip themselves while the repository is
  private.

### 2.4 when the release run fails

If `guard` failed, nothing was tagged, but `main` now carries the new
version. Check what went wrong in the run log. When `main` is still the
state you want to ship, start *Actions → Release → Run workflow* on `main`
with the version as input: it runs the same checks against the current
`main` head and tags and publishes it. Otherwise fix `main` through normal
pull requests and cut the next patch.

If `tag` succeeded and `build` failed, re-run the failed job from the run
page; the tag stays where it is.

**GitHub setup** (configured by hand in the repository settings, rulesets
need a public repository): squash merging only, "Always suggest updating
pull request branches" and "Automatically delete head branches" on; a `main`
branch ruleset with *Restrict deletions*, *Block force pushes*, *Require
linear history*, *Require a pull request before merging* (0 approvals,
squash only) and *Require status checks to pass* with *Require branches to
be up to date before merging* and the checks `build`, `gitleaks` and
`release-check`; a `v*` tag ruleset with *Restrict updates*, *Restrict
deletions* and *Block force pushes* (creation stays open for the release
workflow).

## 3 what a github release contains

Every release publishes exactly three assets:

- `GladosSwAddin-X.Y.Z.msi` - the installer, see [section 4](#4-building-the-msi-locally).
- `GladosSwAddin.dll` - the same merged assembly the MSI installs, unpacked,
  for anyone who wants to inspect it or deploy it without the MSI (e.g. a
  side-by-side manual `regasm` setup, see the README's Installation section). The
  unpacked DLL alone shows English only: the translations are satellite
  assemblies (`de-AT\GladosSwAddin.resources.dll`, ...) that the MSI installs next to
  it, so a manual setup that wants German copies that folder from an installed copy.
- `SHA256SUMS.txt` - checksums for both files above, so a download can be
  verified independently of GitHub's own transport integrity.

The MSI installs `LICENSE.md`, `THIRD-PARTY-NOTICES.md` and the third-party
license texts from `licenses\` next to the add-in, so every installation
carries the license terms. Whoever passes on the unpacked DLL has to include
`LICENSE.md` themselves.

Once the repository is public, the MSI and the DLL also carry a signed
build-provenance attestation (SLSA, via `actions/attest-build-provenance`)
that ties each file to the exact workflow run and commit that built it:
`gh attestation verify GladosSwAddin-X.Y.Z.msi --repo deloarts/glados-sw-addin`.
GitHub offers attestations for private repositories only on Enterprise
Cloud, so the step is skipped while the repository is private.

The release body is the matching `## [X.Y.Z] - ...` section of
`CHANGELOG.md`, extracted verbatim by `release.yml`.

## 4 building the msi locally

One command builds both: the installer project references the add-in
project, so MSBuild compiles (and ILRepack-merges) the add-in first, in the
same configuration, then packages it:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  GladosSwAddin.Installer\GladosSwAddin.Installer.wixproj -restore /p:Configuration=Release
```

Use the full Visual Studio MSBuild (or Build > Build Solution in Visual Studio
with the Release configuration), not `dotnet build`: the referenced add-in is
a classic .NET Framework WinForms project that the .NET SDK's MSBuild cannot
compile. This is also exactly what `ci.yml` and `release.yml` run.

The MSI lands at `GladosSwAddin.Installer\bin\x64\Release\GladosSwAddin-X.Y.Z.msi` (the
version comes from the DLL just built, see [section 1](#1-versioning-policy)).
Visual Studio loads a WiX `.wixproj` only with the HeatWave extension
(FireGiant, free Community edition) installed; without it the installer
project shows as "incompatible" in Solution Explorer - harmless, it is simply
skipped, and the add-in project still builds. The command line and CI need no
extension. With HeatWave installed, add the project to the solution (Add >
Existing Project) to build the MSI from Build > Build Solution and edit
`Package.wxs` with IntelliSense.

To package a DLL built somewhere else, override the source path:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  GladosSwAddin.Installer\GladosSwAddin.Installer.wixproj -restore /p:Configuration=Release `
  /p:GladosSwAddinDllSourcePath=C:\path\to\GladosSwAddin.dll
```

## 5 install, upgrade, uninstall

- **Install:** run the MSI. It is per-machine (installs under
  `Program Files\DELOARTS\GLADOS SOLIDWORKS Add-in`, requires admin) and registers
  the add-in in `HKLM\SOFTWARE\SOLIDWORKS\AddIns` plus a
  "load at startup" flag under `HKCU\SOFTWARE\SOLIDWORKS\AddInsStartup` **for
  the installing user only** - a second person on a shared machine who wants
  the add-in loaded at SOLIDWORKS startup needs their own
  `AddInsStartup\{BF78E037-8884-435D-966F-6EAA619F35B0}` = 1 (SOLIDWORKS'
  Tools > Add-Ins dialog can also set this per-user, without the registry).
  See `GladosSwAddin.Installer/Package.wxs` for the exact registry rows and why that one
  key is deliberately per-user in an otherwise per-machine package (it trips
  ICE64, suppressed on purpose - SOLIDWORKS reads that startup flag from
  HKCU, so a single per-machine key can't represent "which of several
  Windows users on this machine want it auto-loaded").
- **Upgrade:** run a newer MSI over an existing install; `MajorUpgrade`
  removes the old version first (same `UpgradeCode` across every release,
  see `GladosSwAddin.Installer/Package.wxs`'s comment on it - never regenerate that GUID).
  Installing an **older** MSI over a newer install is blocked with a message
  instead of silently downgrading. A rebuilt MSI of the **same** version replaces
  the installed copy too (`AllowSameVersionUpgrades`), so a re-cut build never ends
  up installed twice.
- **Uninstall:** the usual Windows "Apps & features" removes the installed
  files and every registry row the MSI authored (both `AddIns` and the
  installing user's own `AddInsStartup` entry).

### 5.1 what uninstall leaves behind

Uninstalling the MSI does **not** remove:

- **`%LOCALAPPDATA%\GladosSwAddin\cache\`** - the per-server offline lookup
  cache (`docs/DESIGN.md` "Lookup cache"). Per-user, outside the MSI's install
  folder, harmless to leave and safe to delete by hand if ever needed.
- **The PAT in Windows Credential Manager** - stored under the generic
  credential target `GladosSwAddin.Api` (`Services/CredentialService.cs`),
  per Windows user. Remove it via Control Panel > Credential Manager >
  Windows Credentials, or `cmdkey /delete:GladosSwAddin.Api`, if a user is
  done with the add-in entirely.
- **Other users' `AddInsStartup` key** - per [section 5](#5-install-upgrade-uninstall),
  the MSI only ever wrote this for the user who ran the installer; it never
  touches any other Windows user's copy of that key, so uninstalling as one
  user does not affect whether the add-in was set to auto-load for another.

None of these affect whether the add-in loads correctly after a reinstall;
they exist so a returning user doesn't have to re-authenticate or rebuild
the lookup cache from scratch.

## 6 unsigned installer: smartscreen and the signing follow-up

The MSI produced today is **unsigned**. Windows SmartScreen will show an
"Unknown publisher" warning on first run (`More info` > `Run anyway` to
proceed), and `regasm /codebase`-style CodeBase registration on an unsigned
assembly is inherently a same-machine convenience, not a distribution
guarantee - see the `RegAsm` warning surfaced during installer development
(`RA0000`, harmless in this per-machine, single-file-location setup, but a
reminder of the same underlying fact).

Signing (an Authenticode code-signing certificate, applied to both
`GladosSwAddin.dll` and the MSI) is a deliberate follow-up, not done here -
it needs an organizational certificate this repo has no access to. When it
lands: sign the merged DLL as a post-ILRepack build step, sign the MSI as a
post-MSBuild step in `release.yml`, and this section should note the
certificate's issuer/thumbprint expectations so a release can be verified
independently of GitHub.

## 7 upgrading the solidworks interop assemblies

The SOLIDWORKS API interop assemblies are not part of the repository. They
come from the `Visiativ.SOLIDWORKS.Interop` NuGet package (Visiativ is the
SOLIDWORKS reseller; one package version per SOLIDWORKS major release,
`33.x` = 2025, `34.x` = 2026), restored like any other package. Their types
are embedded into `GladosSwAddin.dll` (`EmbedInteropTypes`, forced by the
`EmbedSolidWorksInteropTypes` target in `GladosSwAddin.csproj`), so no
interop assembly ships, and CI fails if the merged DLL ever references one.

**Rule: build against the oldest supported SOLIDWORKS version.** SOLIDWORKS
releases only add API members, so building against the oldest supported
release keeps the add-in loadable on every release from that one onward;
building against a newer one risks calling a member an older SOLIDWORKS
doesn't have - which compiles fine and only fails at runtime there. The
package version therefore names the *oldest supported* release and is
only ever moved deliberately, as below.

To change the supported baseline: set the package version in
`GladosSwAddin.csproj` to that release's major (e.g. `34.0.0` once 2025 is
dropped), rebuild, fix anything that no longer compiles, re-verify manually
in SOLIDWORKS (there is no automated coverage of the SOLIDWORKS API surface,
see `CLAUDE.md`), and note the new minimum SOLIDWORKS version in
`CHANGELOG.md`. This is independent of the add-in's own version unless it
changes add-in behaviour.

**Fallback: a local SOLIDWORKS installation.** Building with
`/p:SolidWorksInteropPath="C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist"`
(or the same name as an environment variable) uses that folder's
`SolidWorks.Interop.*.dll` instead of the package - for an offline machine,
or to check against a specific service pack. Mind the rule above: a newer
local installation can hide a dependency on members the baseline lacks; CI
always builds against the package.

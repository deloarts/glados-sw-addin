# GLADOS SOLIDWORKS Add-in

## Project overview

WinForms task-pane add-in (.NET Framework 4.8, COM-registered, C# 7.3-era) for SolidWorks.
It shows how the open document's custom properties compare against the Glados catalogue
(bought and made items), lets the engineer edit the add-in-owned properties (including a made
item's routing in `Glados.Processes`), and validates a document before PDM release. The
backend lives in the `glados` repository under `backend/` (FastAPI; this add-in talks only to
its `api/v1/pat/*` + `api/v1/auth/*` surface), the web frontend under `frontend/` — sessions
here routinely read both for reference. Machine-specific paths (where the `glados` checkout
and MSBuild live, how a shell reaches them) belong in a gitignored `CLAUDE.local.md`, never in
this file or other committed files.

**The invariant** (README "Sync strategy"): the add-in never authors item data in Glados.
Every write goes to the SolidWorks document; API calls are reads, plus the advisory
`POST pat/pdm/validate` (changes nothing server-side) and one documented exception: the
made-item source-item picker can create a source item (`SourceItemsApiService.CreateSourceItemAsync`,
an idempotent POST gated by `write:source_items`, the same permission as the web create).
No other catalogue entity is ever created or edited.

## Architecture

- `GladosSwAddin/Api/` — `GladosApi` facade (one per server/PAT pair) over per-resource `Services/*ApiService`
  classes; `GladosApiClient` owns HTTP + `JsonSettings` (snake_case, nulls ignored,
  `TolerantStringEnumConverter` — unknown enum values map to member 0, never throw).
  `Models/*Response` mirror backend schemas (XML-doc says "mirrors"/"subset of" which one);
  `Filters/*FilterParams` only carry what the add-in actually sends. Errors surface as
  `GladosApiException` with `StatusCode` + backend `Code` (exception class name) — branch on
  those, not on message text.
- `GladosSwAddin/Models/` — file-side property models (`BoughtItemProperties`, `MadeItemProperties`,
  `ProcessStep`), lookup results, and `FieldMapping`: `FieldOwner`/`ItemKind`/`ItemField`
  enums plus compiled-in default mappings.
- `GladosSwAddin/Services/` — `ApplicationServices` (static singleton facade); `SwCustomPropertyService`
  (all document property I/O); read-only `BoughtItemSyncService`/`MadeItemSyncService`
  (match by `PDM.PartNumber`, divergence over keys only); `FieldMappingService` (resolves
  which custom property backs each backend field — synced from the server, compiled-in
  defaults as fallback); `LookupService` + `LookupCacheService` (in-memory snapshot,
  persisted atomically per server under `%LOCALAPPDATA%\GladosSwAddin\cache\`);
  `CredentialService` (PAT in Windows Credential Manager via advapi32 `Cred*` P/Invoke — the
  stored format must stay what the retired CredentialManagement package wrote, or saved PATs
  stop loading).
- `GladosSwAddin/Forms/` — `GladosTaskPaneControl` (the task pane), `ProcessGroupingControl` (routing
  chips + link/split boundaries), `SettingsForm`. `SwAddin.cs` is the COM entry point.
  `GladosTaskPaneControl` tracks connection state as two independent facts, `ServerKnown` (a
  field mapping has synced from the configured server before) and `Online` (this session's PAT
  test succeeded); any future enable/disable decision for a connection-dependent control belongs
  in `ApplyConnectionState`, the one place that reads both, not scattered across call sites.
- `GladosSwAddin/Helpers/` — `FontManager` (embedded Audiowide/Material Icons/Play), `MaterialIcons`
  (glyph constants), `ThemeColors` (frontend PrimeVue palette mirror), `IconManager`.

### Field ownership rules (docs/FIELD_OWNERSHIP.md is THE domain-rules doc)

- Property names are resolved per call through `FieldMappingService` — never hardcode a
  `Glados.*`/`PDM.*` name where a mapping lookup belongs.
- `FieldOwner` decides what the add-in may touch: `AddIn` (and `AddInWeb`) are writable;
  `Pdm` is never written (`GuardNotPdmOwned` throws); `Web` is never touched; `Server`
  write-backs (`Glados.Ids`, `Glados.NewestRevisionSyncState`, `Glados.NewestRevisionSyncedAt`)
  are never read or written. `Glados.Kind` is add-in-owned but write-once.
- Derived `*Name`/label properties are display copies: never read back as input, never part
  of divergence comparison, re-resolved on every save, never blanked when unresolvable.
- Every successful property write marks the document dirty (`MarkDirty` →
  `ModelDoc2.SetSaveFlag()`) — the SolidWorks custom-property APIs don't set the modified
  flag themselves, and without it users can close the file losing everything just saved.
- Writes fan out to document level and every configuration in the same Save
  (`WriteText` → `WriteAllScopes`); reads prefer document level. See README
  "Write scopes and timing".

## Commands

- Build, with the full Visual Studio MSBuild:
  `MSBuild.exe GladosSwAddin\GladosSwAddin.csproj /t:Build /p:Configuration=Debug /nologo /v:m`
  — the `MSB3216` COM-registration access-denied error at the end is expected without admin
  rights and is NOT a build failure; compile errors and new warnings are. This only applies to
  Debug: `RegisterForComInterop` is Debug-only (explicitly `false` in Release), so a Release
  build never attempts COM registration and never hits MSB3216. The mirror image, `MSB3392`
  (cannot *unregister*), appears when an unelevated Debug build follows a Visual Studio Debug
  build that registered `bin\Debug\GladosSwAddin.dll` — same cause, same verdict. For a plain
  compile check, build Release to a throwaway `/p:OutputPath=bin\VerifyBuild\` and
  delete it afterwards; Release never registers or unregisters anything.
- Release builds set `TreatWarningsAsErrors`: any compiler warning or raised analyzer rule
  (`Microsoft.CodeAnalysis.NetAnalyzers`; `.editorconfig` raises the Globalization,
  Reliability, Security and Usage categories plus CA1001) fails the build and CI. Fix the
  cause; a `#pragma warning disable` needs a comment saying why the rule is wrong there (e.g.
  the deliberate `CurrentUICulture` timestamp formatting). `LangVersion` is pinned to 7.3.
- Every build (both configurations) ends with an ILRepack step (`ILRepack.targets`) that merges
  `Newtonsoft.Json.dll` into `GladosSwAddin.dll` and deletes the
  input (plus `GladosSwAddin.dll.config`) from the output folder — the shipped/registered
  DLL is always the single merged file, in Debug too.
- Release/MSI build (one command, as CI does it):
  `MSBuild.exe GladosSwAddin.Installer\GladosSwAddin.Installer.wixproj -restore /p:Configuration=Release /nologo /v:m`
  — the wixproj's ProjectReference builds the add-in first in the same configuration, then
  packages `GladosSwAddin.Installer\bin\x64\Release\GladosSwAddin-X.Y.Z.msi`. Use the full
  Visual Studio MSBuild, not `dotnet build`: the referenced add-in is a classic .NET Framework
  WinForms project. See [docs/RELEASE.md](docs/RELEASE.md) for the full release flow.
- Unit tests (`GladosSwAddin.Tests`, MSTest, net48) cover pure logic only — parsing,
  canonicalisation, mapping resolution, routing normalisation. Build with the full MSBuild,
  then run the built assembly (not the project — `dotnet test` on the project would try to
  build the classic add-in):
  `MSBuild.exe GladosSwAddin.Tests\GladosSwAddin.Tests.csproj -restore /p:Configuration=Release /nologo /v:m`
  then `dotnet.exe test GladosSwAddin.Tests\bin\Release\net48\GladosSwAddin.Tests.dll`.
  New pure logic gets tests; a `private static` helper may become `internal static` to be
  testable (`InternalsVisibleTo` is set).
- CI (`.github/workflows/ci.yml`) builds, runs the unit tests and packages on pull
  requests into `main`, on pushes to `release/**`, and on demand (`workflow_dispatch`, any branch) —
  never on work-in-progress pushes — but can't exercise the SolidWorks API surface — verification there is still clean
  compile + manual checks in SolidWorks (sample parts live in `.playground/`), stated honestly
  in reports.
- COM registration/regasm needs an elevated Windows shell — leave it to the user.

## Coding conventions

- C# 7.3-era syntax only (net48 default LangVersion): no records, init-only setters, switch
  expressions, or target-typed `new`.
- Every new `.cs`/`.resx` file must be added to `GladosSwAddin.csproj` explicitly — the
  project enumerates `<Compile>` items, no globs (the SDK-style `GladosSwAddin.Tests` project
  does glob). UserControls get `SubType` +
  `DependentUpon` entries like the existing ones.
- `*.Designer.cs` files are hand-edited (no Visual Studio designer pass in this
  environment): follow the existing `InitializeComponent` conventions exactly — field
  declarations, Suspend/ResumeLayout blocks, anchors, explicit `Location`/`Size` points.
- Localization: user-visible strings never appear as literals in code. Each string exists in
  three places: `Resources/Strings.resx` (en), `Resources/Strings.de-AT.resx`, and a
  hand-maintained accessor in `Resources/Strings.Designer.cs` (follows the
  ResXFileCodeGenerator pattern so it could be regenerated later). Reuse the established
  de-AT terminology (e.g. "Teilenummer", "Katalogeintrag") instead of inventing new terms.
- Material Icons: a codepoint is valid only if it appears in the embedded
  `Resources/Fonts/MaterialIcons-Regular.ttf` cmap AND matches the icon name in Google's
  official `MaterialIcons-Regular.codepoints` file — presence in the font alone is not
  enough (U+E155 exists but is "forward", not "call_split"; U+E0A0 is absent entirely).
- Line endings: the working tree is CRLF while HEAD blobs are LF (`core.autocrlf` unset), so
  a plain `git diff` on touched files shows whole-file rewrites. Preserve CRLF when editing;
  use `git -c core.autocrlf=input diff --stat` for honest change sizes.
- UI code: marshal to the UI thread via `OnUiThread`, wrap async entry points in
  `RunGuarded`, and respect `_loadGeneration` so stale async work never paints over a newer
  document/configuration/kind load.
- Every class carries an XML-doc summary explaining purpose and the non-obvious decision
  behind it, in the same voice as its neighbours; inline comments stay sparse.

## Don't

1. Never commit or push unprompted, and never add a "Co-Authored-By: Claude" (or any AI)
   trailer to commit messages.
2. Never write a `PDM.*` or Server-owned property, never overwrite a stamped `Glados.Kind`,
   and never make the add-in author item data in Glados.
3. Never read a derived `*Name` label back as input or include it in divergence comparison.
4. Never hardcode a custom property name that the field mapping is supposed to resolve.
5. Never write German in code, identifiers, comments, or docs — English only. German belongs
   solely in `Strings.de-AT.resx`, in informal "Du".
6. Never reference `.playground/` files or plan/decision points in code comments or
   committed docs — plans are local working artifacts; code must stand alone.
7. Never let a made-tab change regress the bought tab (they share the property, mapping,
   indicator and save plumbing) — and vice versa.
8. Never change existing behaviour to make new code fit without flagging it first.

## Workflow

- Branches: trunk-based. `main` is the only long-lived branch; work happens on `feat/*` or
  `fix/*` branches cut from `main`, and every change reaches `main` as a squash-merged PR (the
  only merge method allowed). A release is a `release/X.Y.Z` PR started with
  `scripts/Start-Release.ps1 X.Y.Z`; merging it tags and publishes the release (see
  `docs/RELEASE.md`).
- No local git hooks: secrets are caught by CI's `gitleaks` job on every pull request (a
  required check) and, where enabled, by GitHub's secret scanning push protection. Don't add a
  pre-commit setup.
- `.playground/` (gitignored) holds plans, decision records, TODOs, and the sample
  SolidWorks parts used for manual verification.
- Line-number references go stale fast here — the repo is also edited in Visual Studio
  between sessions. Locate code by shape, re-verify before editing.

## Docs

- `README.md` — sync strategy, custom property convention, write scopes/timing, linked
  expressions, pre-release validation, lookup cache + Reload. Read before touching sync or
  property code.
- `docs/FIELD_OWNERSHIP.md` — field-ownership rules (this repo's copy; the backend keeps the
  system-wide version at `glados/docs/FIELD_OWNERSHIP.md`).
- `docs/RELEASE.md` — versioning policy, the branch and release flow, the release guard, what a GitHub Release
  contains, install/upgrade/uninstall, and when/how to move the SOLIDWORKS interop baseline
  (`Visiativ.SOLIDWORKS.Interop` package; `SolidWorksInteropPath` falls back to a local install). Read before
  touching `GladosSwAddin/Properties/AssemblyInfo.cs`, `GladosSwAddin/ILRepack.targets`,
  `GladosSwAddin.Installer/`, or the release workflows.
- `glados/docs/PDM_INTEGRATION.md` — the server side of PDM ingest and validation.

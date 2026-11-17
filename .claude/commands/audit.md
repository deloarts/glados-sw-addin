---
description: Run a read-only codebase audit of the add-in across the dimensions below
argument-hint: [dimension or path, e.g. field-ownership, localization, GladosSwAddin/Services, GladosSwAddin/Forms/ProcessRoutingControl.cs]
---

Run a READ-ONLY audit of the add-in (`GladosSwAddin/`, `GladosSwAddin.Tests/`,
`GladosSwAddin.Installer/`, `.github/workflows/`, and the docs as relevant to scope). Do not
modify any source file. Do not commit anything.

For a security-focused pass use `/security-audit`; to review only the pending branch diff use
the built-in `/code-review` or `/security-review`.

## Scope

Arguments given: `$ARGUMENTS`

- If `$ARGUMENTS` names a dimension (see list below), audit only that dimension across the
  whole repo.
- If `$ARGUMENTS` names a path, audit all applicable dimensions but restrict findings to that
  path.
- If `$ARGUMENTS` is empty, run a broad audit across all dimensions.

Dimensions (the slug in brackets is what `$ARGUMENTS` may name):

- **[architecture]** Layering: `Api/` (HTTP + response models only, no WinForms, no SolidWorks
  types), `Models/` (file-side models + `FieldMapping`), `Services/` (property I/O, sync,
  mapping, lookups, credentials), `Forms/` (UI only), `Helpers/`. Check that composition goes
  through `ApplicationServices`, that `SwCustomPropertyService` is the only place doing
  custom-property I/O, and that connection-dependent enable/disable logic lives in
  `GladosTaskPaneControl.ApplyConnectionState` and nowhere else. Use the `architecture` skill
  as the reference.
- **[sync-invariant]** The add-in never authors item data in Glados (docs/DESIGN.md "Sync strategy").
  Enumerate every non-GET request in `Api/` and its callers. The known, deliberate exceptions
  are the PAT test (`auth/login/test-personal-access-token`), the advisory
  `POST pat/pdm/validate`, and source-item creation through the idempotent POST
  (`SourceItemsApiService.CreateSourceItemAsync`). Any other server write is a high finding; a
  known exception that is undocumented in docs/DESIGN.md or CLAUDE.md is a docs finding.
- **[field-ownership]** Against `docs/FIELD_OWNERSHIP.md`: no write to a `Pdm`-owned property
  (every write path must pass `GuardNotPdmOwned`), no read or write of Server-owned
  write-backs, no touching `Web`-owned fields, `Glados.Kind` write-once, derived `*Name`/label
  properties never read back as input or compared for divergence and never blanked when
  unresolvable, every successful write followed by `MarkDirty`, writes fanned out through
  `WriteAllScopes`, reads preferring document level. Grep for hardcoded `"Glados.` / `"PDM.`
  string literals outside `FieldMapping`'s compiled-in defaults; each one is a finding unless
  it names something the mapping deliberately does not cover.
- **[parity]** Bought, made and assembly tabs share property, mapping, indicator and save
  plumbing. Look for fixes or checks applied to one kind and missing from the other(s), and for
  kind-specific branches inside shared code that could regress the other kind.
- **[ui-threading]** Every async entry point wrapped in `RunGuarded`; UI updates marshalled via
  `OnUiThread`; async results checked against `_loadGeneration` before painting; no
  `async void` outside event handlers; no `.Result`/`.Wait()`/`GetAwaiter().GetResult()` on the
  UI thread; `ConfigureAwait(false)` in `Api/` and services.
- **[com-interop]** SolidWorks/COM hygiene: event handlers subscribed in `SwAddin`/the task
  pane are unsubscribed on document close and `DisconnectFromSW`; exceptions never escape a
  COM callback; null-returning SolidWorks calls are checked; long-lived references to
  documents/configurations don't outlive the document.
- **[api-contract]** `Api/Models/*Response` and `Filters/*FilterParams` against the backend (the
  `glados` repository's `backend/`, located via `CLAUDE.local.md`) (`api/v1/pat/*` and `api/v1/auth/*` only): fields
  that were renamed, removed or retyped server-side, enums the add-in no longer recognises
  (they silently map to member 0 via `TolerantStringEnumConverter`, so check what member 0
  means at each use), XML-doc "mirrors"/"subset of" references that point at schemas that no
  longer exist, and error handling that branches on message text instead of
  `GladosApiException.StatusCode`/`Code`.
- **[duplication]** Repeated logic across services, forms and the bought/made/assembly paths
  (property parsing, label composition, lookup resolution, routing normalisation), and
  hand-rolled helpers that duplicate an existing one in `Helpers/` or `Services/`.
- **[code-rot]** Stale comments and XML-docs that no longer describe the code, TODOs, members
  left over from a refactor, classes missing the XML-doc summary the conventions require,
  comments that reference plans, decision points or `.playground/`.
- **[localization]** User-visible string literals in code (message boxes, labels, tooltips,
  status texts, exception messages shown to the user). Key parity across
  `Resources/Strings.resx`, `Resources/Strings.de-AT.resx` and the accessors in
  `Resources/Strings.Designer.cs` (diff the three key sets mechanically; report missing,
  orphaned and unused keys). de-AT strings use informal "Du" and the established terminology
  ("Teilenummer", "Katalogeintrag", ...). No German anywhere outside `Strings.de-AT.resx`.
- **[project-hygiene]** Every `.cs`/`.resx` under `GladosSwAddin/` listed in
  `GladosSwAddin.csproj` (and nothing listed that no longer exists); UserControls carry
  `SubType`/`DependentUpon`; hand-edited `*.Designer.cs` files follow the existing
  `InitializeComponent` conventions; C# 7.3-only syntax; every `#pragma warning disable` has a
  comment saying why the rule is wrong there; Material Icons codepoints in `MaterialIcons.cs`
  exist in the embedded TTF cmap AND match Google's official codepoints file for that name;
  stray build output folders in the tree.
- **[logging]** `Services/Log.cs` discipline: Debug for custom-property values (they must not
  reach the file), Info and above free of property values, PATs, and full request URLs with
  sensitive query parameters; caught exceptions logged once, not swallowed silently and not
  logged at every layer.
- **[performance]** Work on the UI thread that should not be (HTTP, file I/O, large property
  scans), repeated reads of the same property set within one load, N+1 API calls where a list
  endpoint or the lookup snapshot already has the data, cache invalidation that refetches more
  than needed, `HttpClient` instances created per call.
- **[complexity]** Measured, not guessed: line counts per `.cs` file (`wc -l`), and the
  longest methods per file (count lines between a method signature and its closing brace with
  a small local script). Report the top 10 files and top 15 methods. Compare with the previous
  audit's numbers if a report exists. `GladosTaskPaneControl` being large is known; report
  growth, not mere size.
- **[accessibility]** Tab order, `AccessibleName`/`AccessibleDescription` on icon-only
  controls (Material Icons glyph buttons have no readable text), keyboard reachability of
  routing chips and pickers, colour-only state indicators, DPI scaling of the hand-placed
  `Location`/`Size` layouts.
- **[tests]** Pure logic in `Services/`, `Models/`, `Helpers/` and `Api/` (parsing,
  canonicalisation, mapping resolution, routing normalisation, filter/query building) without
  a test in `GladosSwAddin.Tests`; tests that assert implementation details instead of
  behaviour; `private static` helpers that should become `internal static` to be testable.
- **[dead-code]** Unused classes, members, resources, icons, `ApiRoutes` entries, filter
  parameters, NuGet packages, and csproj items.
- **[docs]** README, `docs/DESIGN.md`, `docs/FIELD_OWNERSHIP.md`, `docs/RELEASE.md`, `SECURITY.md`, `CHANGELOG.md`
  and `CLAUDE.md` claims that no longer match the code (cite both the doc line and the code
  line).
- **[industry-standard]** Anything a reviewer of a production .NET Framework WinForms/COM
  add-in would flag that the dimensions above don't cover.

## Before starting

Read existing audit reports in `.audit/audit-*` (newest first) and `.audit/ACCEPTED.md` if it
exists. They track residual items and conventions or claims already accepted or rejected; do
not re-raise anything listed there as accepted/rejected.

## Method

1. Fan out parallel read-only Explore (or general-purpose) subagents, one per dimension (or
   a small group of related dimensions) in scope. Each subagent searches and reads only, with
   no edits. Line numbers go stale fast in this repo (it is also edited in Visual Studio), so
   subagents locate code by shape and report current `file:line`.
2. Before reporting any finding, verify it: it must cite a concrete `file:line`, and you must
   re-check that file:line yourself before it goes in the report. Do not report a finding you
   have not personally verified.
3. Cross-check candidate findings against the earlier reports and `.audit/ACCEPTED.md`, and
   drop duplicates (a residual item still open is reported as "still open", not as new).
4. A compile check is allowed when a dimension needs it (e.g. analyzer warnings): build
   Release to a throwaway output folder as CLAUDE.md describes
   (`/p:OutputPath=bin\VerifyBuild\`), quote the result, and delete that folder afterwards.

## Output

Write a report to `.audit/audit-<today's date>/report.md` (create the folder; use
`YYYY-MM-DD` for today). Structure:

- Scope line: dimensions covered, commit (`git rev-parse --short HEAD`), branch.
- Findings ranked by severity (high/medium/low).
- Each finding in the work-package style: **Findings** (what and why), **Files**
  (file:line references), **Approach** (how it would be fixed), **Risk**, **Effort**.
- A "Still open from previous audit" section and a "Measurements" section (complexity
  numbers) so the next audit can diff against them.

Then send the user a short summary: dimension(s) covered, count of findings by severity, and
the report path. Do not include the full report inline unless the user asks.

Hard constraints: this is read-only. Do not edit, create, or delete any source file. Do not
run `git add`/`git commit`. The only files you write are the audit report files (and the
throwaway build output, deleted afterwards).

## Models

Use model `haiku` for the investigation subagents; verification and the report stay with the
main session. Minimize token usage. Don't assume anything.

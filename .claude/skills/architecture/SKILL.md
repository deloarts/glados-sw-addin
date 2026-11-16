---
name: architecture
description: Where code belongs in the Glados SolidWorks add-in and which invariants it must respect. Load when adding features, new files, or wiring new components in this repo.
---

# Glados SolidWorks add-in architecture

A WinForms task-pane add-in (.NET Framework 4.8, COM) that compares the open document's
custom properties against the Glados catalogue over the backend's PAT API and edits the
add-in-owned properties. Place new code in the right layer and respect the ownership
invariants in CLAUDE.md / docs/FIELD_OWNERSHIP.md.

## Layout

- `SwAddin.cs` — COM entry point (`ISwAddin`, GUID `BF78E037-…`, `LoadAtStartup`). Creates
  the task pane and hooks `ActiveDocChangeNotify` / `ActiveModelDocChangeNotify` /
  `FileCloseNotify`. Keep it thin — logic lives in the task pane control and services.
- `Api/`
  - `GladosApiClient.cs` — HTTP + `JsonSettings` (snake_case, `NullValueHandling.Ignore`,
    `TolerantStringEnumConverter`: unknown wire value → enum member 0). 422 bodies and
    `{detail, code}` errors are parsed into `GladosApiException` (`StatusCode`, `Code`).
  - `GladosApi.cs` — facade; one instance per server/PAT pair; exposes each
    `Api/Services/*ApiService` as a property. New backend resources get a new service class
    plus a facade property.
  - `Api/Models/` mirror backend Pydantic schemas (doc-comment says which, "mirrors" vs
    "subset of"); `Api/Filters/` carry only the query params actually sent;
    `ApiRoutes.cs` holds the route prefixes (`api/v1/pat/`, `api/v1/auth/`, `pdm/validate`).
  - `ConditionalGetResult` + ETag revalidation back the lookup Reload path.
- `Models/`
  - `FieldMapping.cs` — `FieldOwner` (`Unknown|Pdm|AddIn|AddInWeb|Web|Server`), `ItemKind`
    (`made|bought`), `ItemField`, compiled-in default mappings per kind, derived-label
    mapping. The mapping, not constants, decides which custom property backs a field.
  - `BoughtItemProperties` / `MadeItemProperties` — the file-side view of one item;
    `ProcessStep` — one routing step of `Glados.Processes` (ids-only JSON, position = array
    order, `group_index` per step; `MethodName` is runtime-only, never serialized).
  - `*LookupResult` — sync match states (`NoPartNumber|NotFound|FoundInSync|FoundDiverges`,
    bought additionally `PossibleDuplicate`).
- `Services/`
  - `ApplicationServices` — static composition root: Settings, Credentials, LookupCache,
    Lookup, FieldMapping, Api, sync services.
  - `SwCustomPropertyService` — ALL document property I/O goes through here. Guards:
    `GuardNotPdmOwned`, write-once `Glados.Kind`, `MarkDirty` (→ `SetSaveFlag()`) on every
    successful write, fan-out to document level + all configurations (`WriteAllScopes`),
    tolerant `ParseProcesses`, locale-aware `ReadMassGrams`.
  - `BoughtItemSyncService` / `MadeItemSyncService` — read-only lookups by
    `PDM.PartNumber` (made: newest revision); divergence compares reference keys only,
    never derived labels.
  - `FieldMappingService` — resolves properties per (`ItemKind`, `ItemField`); syncs from
    the server per URL, tolerant of unknown fields/owners; compiled-in defaults otherwise.
  - `LookupService` / `LookupCacheService` — in-memory `LookupSnapshot` for pickers;
    persisted atomically (temp + `File.Replace`, named mutex) per server under
    `%LOCALAPPDATA%\GladosSwAddin\cache\<serverKey>\lookups.json`.
- `Forms/` — `GladosTaskPaneControl` (+ hand-edited `.Designer.cs`), `ProcessGroupingControl`
  (routing chips, link/split boundary toggles, inspection steps forced standalone),
  `SettingsForm` (server URL, PAT, field-mapping grid).
- `Helpers/` — `FontManager` (embedded fonts), `MaterialIcons` (verified glyph constants),
  `ThemeColors` (frontend palette mirror), `IconManager` (ribbon BMP extraction).
- `Resources/` — `Strings.resx` / `Strings.de-AT.resx` + hand-maintained
  `Strings.Designer.cs`; embedded fonts and icons.

## Flow

Document event → `GladosTaskPaneControl` (`RunGuarded`, `_loadGeneration`, `OnUiThread`) →
`SwCustomPropertyService.Read*` (via `FieldMappingService`) → populate UI → sync service
lookup → indicators. Save reads the UI back into the properties model, writes through
`SwCustomPropertyService` (labels re-resolved, groups normalized, dirty flag set), then
re-runs the lookup.

## When adding code

- New API surface → `Api/Services` + `Api/Models` (+ filter), wired into `GladosApi`; keep
  the response model doc-comment honest about what it mirrors.
- New document field → `ItemField` + default mapping entry in `FieldMapping.cs`, read/write
  in `SwCustomPropertyService`, divergence decision in the sync service, UI in the task pane.
  Check `docs/FIELD_OWNERSHIP.md` for the owner before writing anything.
- New file → explicit `<Compile>` (and `EmbeddedResource`) entry in `GladosSwAddin.csproj`.
- New user-visible string → all three resource files.

# Field Ownership — add-in side

The authority on field ownership is the backend document:
**`glados/docs/FIELD_OWNERSHIP.md`** (v1.0, 2026-08-27, approved).
It defines, per field, which system owns the value, the two number spaces,
the claim/parked-link rules, revision policy and lifecycle mapping. This
document does not restate it. It covers only (1) where the add-in differs
from or extends that document, and (2) the **field-mapping sync** that lets
the backend's configured custom-property names drive the add-in instead of
hardcoded constants.

## 1. Differences and add-in specifics

| # | Topic | Backend `FIELD_OWNERSHIP.md` | This add-in | Status |
| --- | --- | --- | --- | --- |
| 1 | Mass / material / base size | `Glados.Mass` / `Glados.Material` / `Glados.BaseSize`, editor **Add-in** | Code had migrated mass into the vault namespace (2026-08); vault-namespaced material/base-size properties were planned | **Resolved: backend is right.** These are add-in values — the document template binds them to SW-calculated expressions by default, but the user may override (with a warning; implemented: the Calc. buttons re-link mass/material). Reverted to `Glados.*`; the add-in now writes them on every Save, mirroring the template's linked expression to each configuration unless the user has overridden it — see README's "Write scopes and timing" / "Linked expressions on Save". |
| 2 | `PDM.*` namespace | Editor **PDM**: numbering, revisioning, vault identity | README rule: prefix = who writes it | Aligned. `PDM.*` shrinks to `PDM.PartNumber` + `PDM.Revision`. |
| 3 | Label properties (`Glados.ManufacturerName`, `Glados.ArticleGroupNames`, `Glados.ProcessNames`, `Glados.SourceItemName`) and the feature-tree summary (`Glados.FeatureTreeRepresentation`) | A label is a normal mappable, add-in-owned row in the field mapping now, `label_of` naming its source field; the feature-tree field holds `made_feature_tree_template`/`bought_feature_tree_template` verbatim | README derived-property rules: never read back as input, re-resolved on save, never blanked | Add-in-specific write path. A label's property name is resolved through `FieldMappingService.ResolvePropertyName` like any other field — no separate derivation rule or compiled-in fallback table. `Glados.SourceItemName` is the one label the add-in itself composes (`"<type_name> <dimension> <material>"`, see `Helpers/SourceItemLabelComposer.cs`) rather than reading a backend-resolved string. `Glados.FeatureTreeRepresentation` gets the kind's template written verbatim in each Save, and SolidWorks resolves its `$PRP:"name"` links to other custom properties itself, so it follows a later change to any of them, a part number or revision PDM assigns included — see `SwCustomPropertyService.WriteFeatureTreeRepresentation`. |
| 4 | *(removed)* `Glados.Source` (routing: `bought` / `made`) | Not a backend field | No longer read or written anywhere in the add-in — see row 4a and README's "Kind decides which catalogue". | Superseded by `Glados.Kind`. Row number kept unrenumbered — see row 10 and other cross-file `row N` references. |
| 4a | `Glados.Kind` (routing: `bought` / `made`) | Backend field `kind`, editor **Add-in** | Mapped like any other field (section 2/3); write-once — refuses to overwrite a different existing value | Add-in-specific. Sole driver of the Source dropdown; locks it the moment it holds a value. |
| 5 | `Glados.Processes` encoding | Authored in CAD; encoding "defined together with the add-in" | Not defined | Open — joint definition pending. |
| 6 | Pending link confirmation (`POST /bought-items/{public_id}/pdm-link`) | Designed (inexact match parks; engineer confirms from the add-in) | No UI yet | Pending — backend PDM module is not implemented (`is_pdm_enabled()` stub). |
| 7 | Sync watermark | Rows carry `pdm_file_version` / `pdm_synced_at` | README "known gap": can't split "ingest behind" from "diverged" | Closes the gap once the PDM module lands. |
| 8 | `source_item_id` | Dual-owned; PDM shadow `pdm_source_item_id`, CAD wins only when the CAD value changes | Made-item sync compares the file against the **shadow**, not the effective value | Implemented — `MadeItemSyncService.GetDivergentFields` compares `MadeItemProperties.SourceItemId` against `MadeItemResponse.PdmSourceItemId`; both null (no id picked, no shadow set — including on an older backend that doesn't send the field yet) reads as in sync, never as a mismatch. |
| 9 | Matching | Claim keys: `gtin`; `manufacturer_id` + `order_code_1` (exact). Identity `(vault_id, pdm_file_id)` | Tier 1 `PDM.PartNumber`, tier 2 informational duplicate check | Compatible; the add-in may later match by PDM file id (survives renumbering). |
| 10 | Field-name configuration | *Settings → PDM → Field mapping*; doc tables are "defaults of the field mapping, not code" | Field mapping sync (section 2) | Implemented — `GET /pat/settings/field-mapping`; add-in degrades to compiled-in defaults on an older backend or any sync failure. |
| 11 | Lookup cache endpoint | Not covered | Manufacturer/Article Group/Fabrication Method/Inspection Method/Source Item Type/Source Item picker data (distinct from the field-mapping sync in row 10) | Implemented: the endpoint now exists. The add-in's offline lookup cache is fed by a single `GET /pat/lookups` call, with ETag revalidation, instead of separate capped list requests — see README.md's "Lookup cache and Reload" section. |
| 12 | Scope (where properties live) | PDM ingest reads `@` only | Writes every property it authors to `@` *and* every configuration, in the same Save; reads the first non-empty value, `@` before the active configuration | Aligned — `@` is canonical for both systems, a configuration copy is a display convenience only. See README's "Write scopes and timing". |
| 13 | Other writers of add-in properties | Not covered | A PDM data card (or File > Properties, a macro) writes the same `Glados.*` properties the add-in authors; nothing notifies the task pane | Add-in-specific. The pane polls a property fingerprint and reloads (or, with unsaved edits, flags Reload From File) when it changes. Save re-reads the document and writes the user's edits on top of it (`SaveMergeService`): an outside change to a field the user did not edit is kept, a field both sides changed differently is put to the user. See README's "Data flow". |

## 2. Field mapping sync

The custom-property names the add-in reads and writes come from the backend's
field mapping, not from code. Hardcoded values exist only as the **defaults**
— byte-identical to the backend document's default tables — used until a
server mapping has been fetched.

### Semantics

- **Fetch on every successful connect**; the backend **always overwrites**
  the stored copy. Safe because the mapping is read-only in the add-in.
- **Keyed to the server.** The stored mapping records the base URL it came
  from; a URL change invalidates it (fall back to defaults until the next
  connect).
- **Degrade, don't fail.** Endpoint missing (older backend) or erroring →
  keep stored copy or defaults; connect still succeeds; the settings tab
  status line says which source is active.
- **Tolerant parsing.** Entries are keyed by the raw backend field name;
  unknown fields are preserved and shown greyed in the settings grid, never
  an error.
- **Unmapped = never written.** A field absent from the server mapping is
  not written to the document (mirrors ingest: "an unmapped field is never
  written").
- **Storage:** user settings via `SettingsService` (not the lookup cache file
  README.md describes), as JSON: schema version, server base URL, `synced_at`,
  entries.

### Required fields

The backend also serves, per field, whether ingest requires it: a fixed locked set
(`part_number`, `revision`, `article_name` for both kinds, plus `order_code_1` and
`manufacturer_id` for bought items) that is always required regardless of settings, and
whatever else an admin has additionally ticked in Glados' PDM settings, including one
pseudo-rule for made items, `fabrication_step`, satisfied once the routing has a fabrication
step with a method assigned (`glados/docs/PDM_INTEGRATION.md` section 4.4 is the
authority on both). The add-in reads this the same way it reads the rest of the field mapping —
`FieldMappingEntry.Required` per field, a separate `rules` map per kind — and only ever uses it
to render an advisory marker (README's "Required-field markers"); it plays no part in what Save
writes or allows. The one exception: a required field that is also PDM-owned (`part_number`/
`revision` by default) shows a placeholder instead of the marker, since PDM's own release
workflow — not the engineer — is expected to fill it in.

### Code shape

- `Models/FieldMapping.cs` — `ItemKind` (`made` / `bought`), `ItemField`
  (members PascalCased from backend field names: `part_number` →
  `PartNumber`, including the label fields `ArticleGroupNames` /
  `ManufacturerName` / `ProcessNames` / `SourceItemName` and
  `FeatureTreeRepresentation`), `FieldOwner` (wire values are the backend's
  canonical owner strings `pdm` / `addin` / `addin_web` / `web` / `server`;
  the display forms `PDM` / `Add-in` / `Add-in / Web` / `Web` / `Server` used
  in this document are grid-only),
  `FieldMappingEntry { Field, Owner, PropertyName, UpdateInPlace, Required, LabelOf }`
  (`LabelOf` names the field a label row is a label of, null for every other
  field including `feature_tree_representation`), `FieldMapping.Defaults`
  (section 3), `FeatureTreeTemplates` (the `Made`/`Bought` template strings,
  `FieldMapping.DefaultTemplates` before the first sync), and
  `RequiredFieldRule` / `RequiredFieldRuleName` (the `rules` map — see
  "Required fields" above), all persisted together in the same
  `FieldMappingJson` blob.
- `Services/FieldMappingService` — the only consumer of
  `FieldMapping.Defaults`/`DefaultTemplates`. Everything else calls
  `Resolve(kind, field)`/`ResolvePropertyName(kind, field)` for a field's own
  property — a label field goes through exactly the same call, there is no
  separate label-resolution path any more — and
  `MadeFeatureTreeTemplate`/`BoughtFeatureTreeTemplate` for the feature-tree
  summary, written verbatim.
  Ownership guards consult the entry's owner; the `PDM.*` prefix check stays
  as a second line of defense.
- Settings UI: `tabPageFieldMapping`, read-only grid (Kind · Field · Owner ·
  Custom Property · Update in Place) with a provenance line — *"Synced from
  `<server>` at `<time>`"* or *"Defaults — not yet synced"* — and a second,
  fixed line below the grid showing the two feature-tree templates. A label
  row is a normal grid row now, in whatever position `FieldMapping.Defaults`
  lists it, no longer inserted separately or greyed.

### Follow-ups (not implemented)

Override UI for mass/material/base size (warning + "restore calculated
value"); disabling unmapped fields in the task pane; made-item tab;
pending-link confirmation UI; `Glados.Processes` encoding; "Update in Place"
annotation in the divergence display.

## 3. Defaults

Keep in sync with `glados/docs/FIELD_OWNERSHIP.md` §3/§4. These are
the compiled-in values of `FieldMapping.Defaults` and the grid's content
before the first sync. *UIP* = PDM: update in place (made items only; bought
items keep no revisions).

### Made items

| Field | Owner | Custom property | UIP |
| --- | --- | --- | --- |
| `part_number` | PDM | `PDM.PartNumber` | — |
| `revision` | PDM | `PDM.Revision` | — |
| `revision_date` | PDM | — | yes |
| `revision_note` | PDM | — | yes |
| `article_name` | Add-in | `Glados.ArticleName` | no |
| `material` | Add-in | `Glados.Material` | no |
| `mass` | Add-in | `Glados.Mass` | yes |
| `base_size` | Add-in | `Glados.BaseSize` | no |
| `note` | Add-in | `Glados.Note` | yes |
| `weblink` | Add-in | `Glados.Weblink` | yes |
| `article_group_ids` | Add-in | `Glados.ArticleGroupIds` | yes |
| `bought_item_id` | Add-in | `Glados.BoughtItemId` | no |
| `source_item_id` | Add-in / Web | `Glados.SourceItemId` | no |
| `processes` | Add-in | `Glados.Processes` | yes |
| `status` | PDM | — | — |
| `successor_item_id` | Web | — | — |
| `approver_id` | Web | — | — |
| `files` | Web | — | — |
| `data_source` | Server | — | — |
| `kind` | Add-in | `Glados.Kind` | no |
| `article_group_names` | Add-in | `Glados.ArticleGroupNames` | no |
| `process_names` | Add-in | `Glados.ProcessNames` | no |
| `source_item_name` | Add-in | `Glados.SourceItemName` | no |
| `feature_tree_representation` | Add-in | `Glados.FeatureTreeRepresentation` | no |
| `customer_id` | Add-in | `Glados.CustomerId` | yes |
| `customer_part_number` | Add-in | `Glados.CustomerPartNumber` | yes |
| `customer_revision` | Add-in | `Glados.CustomerRevision` | yes |
| `customer_name` | Add-in | `Glados.CustomerName` | no |

### Bought items

| Field | Owner | Custom property |
| --- | --- | --- |
| `part_number` | PDM | `PDM.PartNumber` |
| `revision` | PDM | `PDM.Revision` |
| `article_name` | Add-in | `Glados.ArticleName` |
| `order_code_1` | Add-in | `Glados.OrderCode1` |
| `order_code_2` | Add-in | `Glados.OrderCode2` |
| `gtin` | Add-in | `Glados.Gtin` |
| `ean` | Add-in | `Glados.Ean` |
| `tariff_number` | Add-in | `Glados.TariffNumber` |
| `mass` | Add-in | `Glados.Mass` |
| `note` | Add-in | `Glados.Note` |
| `weblink` | Add-in | `Glados.Weblink` |
| `manufacturer_id` | Add-in | `Glados.ManufacturerId` |
| `article_group_ids` | Add-in | `Glados.ArticleGroupIds` |
| `edp_part_id` | Add-in / Web | `Glados.EdpPartId` |
| `status` | PDM | — |
| `successor_item_id` | Web | — |
| `approver_id` | Web | — |
| `files` | Web | — |
| `data_source` | Server | — |
| `kind` | Add-in | `Glados.Kind` |
| `manufacturer_name` | Add-in | `Glados.ManufacturerName` |
| `article_group_names` | Add-in | `Glados.ArticleGroupNames` |
| `feature_tree_representation` | Add-in | `Glados.FeatureTreeRepresentation` |

`edp_part_id` is the EPLAN Data Portal part the item was taken from. The
add-in writes it when the engineer takes a part from the Data Portal dialog;
the web links catalogue items to parts from its own lookups. Like
`source_item_id` it is dual-owned: PDM ingest relinks the catalogue item only
when the file's value changes, so a link made in the web survives a sync of an
unchanged file, and the add-in never compares it for divergence.

Labels (`Glados.ManufacturerName`, `Glados.ArticleGroupNames`,
`Glados.ProcessNames`, `Glados.SourceItemName`) and the feature-tree summary
(`Glados.FeatureTreeRepresentation`) are normal mapping entries now (row 3 of
the table in section 1) — a label's `LabelOf` names its source field
(`manufacturer_id`, `article_group_ids`, `processes` or `source_item_id`);
the feature-tree field's `LabelOf` is null, it holds the synced template, or
`FieldMapping.DefaultTemplates` before the first sync.

The compiled-in feature-tree template defaults, `FieldMapping.DefaultTemplates`,
match the backend's own `AdminPdmSettings.made_feature_tree_template`/
`bought_feature_tree_template` defaults exactly: `$PRP:"PDM.PartNumber"
Rev$PRP:"PDM.Revision" ($PRP:"Glados.ArticleName")` for made items,
`$PRP:"Glados.ArticleName" - $PRP:"Glados.OrderCode1" -
$PRP:"Glados.ManufacturerName"` for bought items.

Read fallbacks (never written): `mass` reads the mapped name first, then
`Glados.Mass` legacy — the latter now coincides with the primary name.

`kind` is write-once (README's "Kind decides which catalogue"): once
`Glados.Kind` holds a valid value, the add-in refuses to write a different
one over it rather than treating it like an ordinary Add-in-owned field.

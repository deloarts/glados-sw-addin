using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;
using Newtonsoft.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Reads/writes GladosSwAddin.Models.BoughtItemProperties and
    /// GladosSwAddin.Models.MadeItemProperties against a SolidWorks document's custom
    /// properties. Reads prefer document level, falling back to the active configuration's
    /// property (ResolveScope/Read) — document level is canonical, same as the backend's PDM
    /// ingest, and a configuration copy is a display convenience that can go stale. Writes go
    /// the other way: every property this addin authors is fanned out to document level *and*
    /// every configuration in the same Save (WriteText → WriteAllScopes), so a PDM data card
    /// bound to a configuration tab shows it too — see docs/DESIGN.md "Write scopes and timing".
    /// Which property backs each field is resolved per call through the FieldMappingService
    /// passed in, not fixed constants — see docs/FIELD_OWNERSHIP.md section 2. The one field
    /// this addin authors end-to-end rather than syncing is a made item's routing
    /// (Glados.Processes, see WriteProcesses and
    /// .playground/made-item-process-grouping-plan.md §3.2/§3.3).
    /// </summary>
    public static class SwCustomPropertyService
    {
        /// <summary>Separates the ids in the article-group-keys property.</summary>
        private const string IdSeparator = ",";

        /// <summary>Separates the labels in the article-group-labels property. Deliberately
        /// different from IdSeparator: a comma is common inside a company or group name, and
        /// PDM search matches on substrings.</summary>
        private const string LabelSeparator = ";";

        /// <summary>The SolidWorks-reserved property names the two "restore calculated value"
        /// buttons re-link Mass/Material to — see RestoreCalculatedMass/RestoreCalculatedMaterial
        /// and TryRewriteSimpleLinkForConfig, which already rewrites links built from these same
        /// two names for the per-configuration form.</summary>
        private const string SwMassPropertyName = "SW-Mass";

        private const string SwMaterialPropertyName = "SW-Material";

        public static BoughtItemProperties ReadBoughtItem(ModelDoc2 model, FieldMappingService fieldMapping)
        {
            string ReadMapped(ItemField field) =>
                ReadIfMapped(model, fieldMapping.ResolvePropertyName(ItemKind.Bought, field));

            var manufacturerIdProperty =
                fieldMapping.ResolvePropertyName(ItemKind.Bought, ItemField.ManufacturerId);
            var articleGroupIdsProperty =
                fieldMapping.ResolvePropertyName(ItemKind.Bought, ItemField.ArticleGroupIds);

            return new BoughtItemProperties
            {
                PartNumber = ReadMapped(ItemField.PartNumber),
                Revision = ReadMapped(ItemField.Revision),
                ArticleName = ReadMapped(ItemField.ArticleName),
                OrderCode1 = ReadMapped(ItemField.OrderCode1),
                OrderCode2 = ReadMapped(ItemField.OrderCode2),
                Gtin = ReadMapped(ItemField.Gtin),
                Ean = ReadMapped(ItemField.Ean),
                TariffNumber = ReadMapped(ItemField.TariffNumber),
                Note = ReadMapped(ItemField.Note),
                Weblink = ReadMapped(ItemField.Weblink),
                Mass = ReadMassGrams(model, fieldMapping, ItemKind.Bought),
                ManufacturerId = ParseIntOrNull(ReadIfMapped(model, manufacturerIdProperty)),
                ManufacturerName = ReadMapped(ItemField.ManufacturerName),
                ArticleGroupIds = ParseIntList(ReadIfMapped(model, articleGroupIdsProperty)),
                ArticleGroupNames = ParseLabelList(ReadMapped(ItemField.ArticleGroupNames)),
                EdpPartId = ParseIntOrNull(ReadMapped(ItemField.EdpPartId)),
            };
        }

        /// <summary>Mirrors ReadBoughtItem. Processes is read tolerantly (see ParseProcesses):
        /// unparseable or empty Glados.Processes never throws and never overwrites anything —
        /// it just yields an empty step list with ProcessesUnreadable set so the caller can
        /// show a hint instead of silently discarding whatever's on disk on the next save.
        /// </summary>
        public static MadeItemProperties ReadMadeItem(ModelDoc2 model, FieldMappingService fieldMapping)
        {
            string ReadMapped(ItemField field) =>
                ReadIfMapped(model, fieldMapping.ResolvePropertyName(ItemKind.Made, field));

            var articleGroupIdsProperty =
                fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.ArticleGroupIds);
            var processesProperty =
                fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.Processes);
            var sourceItemIdProperty =
                fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.SourceItemId);
            var customerIdProperty =
                fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.CustomerId);

            var processesText = ReadIfMapped(model, processesProperty);
            bool processesUnreadable;
            var processes = ParseProcesses(processesText, out processesUnreadable);

            return new MadeItemProperties
            {
                PartNumber = ReadMapped(ItemField.PartNumber),
                Revision = ReadMapped(ItemField.Revision),
                ArticleName = ReadMapped(ItemField.ArticleName),
                Material = ReadMapped(ItemField.Material),
                Mass = ReadMassGrams(model, fieldMapping, ItemKind.Made),
                BaseSize = ReadMapped(ItemField.BaseSize),
                Note = ReadMapped(ItemField.Note),
                Weblink = ReadMapped(ItemField.Weblink),
                ArticleGroupIds = ParseIntList(ReadIfMapped(model, articleGroupIdsProperty)),
                ArticleGroupNames = ParseLabelList(ReadMapped(ItemField.ArticleGroupNames)),
                SourceItemId = ParseIntOrNull(ReadIfMapped(model, sourceItemIdProperty)),
                SourceItemName = ReadMapped(ItemField.SourceItemName),
                Processes = processes,
                ProcessesUnreadable = processesUnreadable,
                ProcessesStored = !string.IsNullOrWhiteSpace(processesText),
                ProcessNames = ReadMapped(ItemField.ProcessNames),
                CustomerId = ParseIntOrNull(ReadIfMapped(model, customerIdProperty)),
                CustomerName = ReadMapped(ItemField.CustomerName),
                CustomerPartNumber = ReadMapped(ItemField.CustomerPartNumber),
                CustomerRevision = ReadMapped(ItemField.CustomerRevision),
            };
        }

        /// <summary>
        /// Builds the "variables" payload for POST /pat/pdm/validate: the current value of
        /// every custom property the active field mapping maps for <paramref name="kind"/>,
        /// read with the same scope resolution as every other property (Read). Keyed by the
        /// actual custom property name, not the backend field name — the same shape a PDM data
        /// card presents. The server-owned write-back properties (Glados.Ids,
        /// Glados.NewestRevisionSyncState, Glados.NewestRevisionSyncedAt — see
        /// docs/FIELD_OWNERSHIP.md) are skipped via their FieldOwner.Server owner: they are
        /// what PDM ingest writes back afterwards, not something to validate against.
        /// PDM.PartNumber, PDM.Revision and Glados.Kind are always included under their literal
        /// names regardless of what the current mapping happens to resolve them to — same
        /// literal-fallback rationale as BoughtItemProperties.PropertyNames.MassFallbackLegacy —
        /// since the endpoint's identity/kind resolution depends on exactly these three
        /// vault/write-once properties.
        /// </summary>
        public static Dictionary<string, string> BuildPdmValidationVariables(
            ModelDoc2 model,
            FieldMappingService fieldMapping,
            ItemKind kind)
        {
            var variables = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (ItemField field in Enum.GetValues(typeof(ItemField)))
            {
                var entry = fieldMapping.Resolve(kind, field);
                if (entry?.PropertyName == null || entry.Owner == FieldOwner.Server)
                    continue;

                variables[entry.PropertyName] = Read(model, entry.PropertyName);
            }

            variables["PDM.PartNumber"] = Read(model, "PDM.PartNumber");
            variables["PDM.Revision"] = Read(model, "PDM.Revision");
            variables["Glados.Kind"] = Read(model, "Glados.Kind");

            return variables;
        }

        /// <summary>
        /// Writes fieldName into every scope a PDM data card might read from: document level
        /// and every configuration GetConfigurationNames() currently returns (WriteAllScopes)
        /// — see docs/DESIGN.md "Write scopes and timing". Only ever call this (directly, or via one
        /// of the WriteXxx helpers below) for a property this addin itself authors: PDM.* is
        /// refused outright (GuardNotPdmOwned), and the backend's write-back variables
        /// (Glados.Ids, Glados.NewestRevisionSyncState, Glados.NewestRevisionSyncedAt) simply
        /// never reach here — the add-in neither reads nor writes them. Returns false (and
        /// logs via Log) if any scope's underlying SolidWorks API call reports
        /// failure — e.g. the property is locked or linked/derived and can't be set directly
        /// there — but a failure in one scope never skips the rest.
        /// </summary>
        public static bool WriteText(ModelDoc2 model, string fieldName, string value)
        {
            GuardNotPdmOwned(fieldName);
            return WriteAllScopes(model, fieldName, value);
        }

        /// <summary>
        /// The fan-out write every WriteXxx helper in this class ultimately funnels through
        /// (WriteKind is the one exception — a conflicting value there must block just that one
        /// scope rather than the whole write, see WriteKind): puts fieldName into document level
        /// and every configuration (EnumerateScopes), so a configuration added since the last
        /// Save gets the property on this one. Returns true only if every scope's write
        /// succeeded; marks the document dirty once, at the end, if at least one scope was
        /// actually written.
        /// </summary>
        private static bool WriteAllScopes(ModelDoc2 model, string fieldName, string value)
        {
            value = value ?? string.Empty;
            bool ok = true;
            bool wroteAny = false;

            foreach (var scope in EnumerateScopes(model))
            {
                if (WriteToScope(scope.Key, fieldName, value, scope.Value))
                    wroteAny = true;
                else
                    ok = false;
            }

            if (wroteAny)
                MarkDirty(model);

            return ok;
        }

        /// <summary>Sets fieldName in one already-identified scope if it exists there, else adds
        /// it there — the per-scope primitive EnumerateScopes-based callers (WriteAllScopes,
        /// WriteKind, MirrorDocumentValueToConfigurations) share.</summary>
        private static bool WriteToScope(
            ICustomPropertyManager cpm, string fieldName, string value, string scopeDescription)
        {
            return Exists(cpm, fieldName)
                ? SetAndVerify(cpm, fieldName, value, scopeDescription)
                : AddAndVerify(cpm, fieldName, value, scopeDescription);
        }

        /// <summary>Document level, then every configuration — the scope set a fan-out write
        /// hits. Configuration names are read fresh via GetConfigurationNames() on every call
        /// (never cached), so a configuration added since the document was loaded is picked up
        /// by the next Save, exactly as docs/DESIGN.md "Write scopes and timing" describes.</summary>
        private static IEnumerable<KeyValuePair<ICustomPropertyManager, string>> EnumerateScopes(
            ModelDoc2 model)
        {
            yield return new KeyValuePair<ICustomPropertyManager, string>(
                model.Extension.CustomPropertyManager[""], "document level");

            foreach (var configName in (model.GetConfigurationNames() as string[]) ?? new string[0])
            {
                yield return new KeyValuePair<ICustomPropertyManager, string>(
                    model.Extension.CustomPropertyManager[configName],
                    $"configuration '{configName}'");
            }
        }

        /// <summary>
        /// Flags the document as having unsaved changes. The custom-property APIs
        /// (ICustomPropertyManager.Add3/Set2) modify the document without setting its
        /// modified flag — without this, SolidWorks lets the user close the file with no
        /// save prompt and every property just written is silently lost.
        /// </summary>
        private static void MarkDirty(ModelDoc2 model)
        {
            model.SetSaveFlag();
        }

        /// <summary>
        /// Writes a resolved label next to its key. Before the write fan-out, this mattered
        /// because a write only ever landed in one scope and a label could end up on document
        /// level while its key lived on the active configuration (or vice versa); now that
        /// WriteText fans every add-in-written property out to document level *and* every
        /// configuration (WriteAllScopes), a label and its key always land in the same scopes
        /// by construction, so this is a thin alias — kept so call sites keep documenting which
        /// key a label belongs to (docs/DESIGN.md rule 4: "a label is written to the same scope as its
        /// key") rather than losing that pairing to a bare WriteText call.
        /// <paramref name="anchorProperty"/> is unused; the fan-out no longer needs it.
        /// </summary>
        public static bool WriteTextInScopeOf(
            ModelDoc2 model,
            string anchorProperty,
            string fieldName,
            string value)
        {
            return WriteText(model, fieldName, value);
        }

        /// <summary>
        /// Writes the manufacturer reference key and its resolved label together, the label into
        /// the key's own scope. Pass null for <paramref name="manufacturerName"/> to leave the
        /// stored label untouched — that's the "couldn't resolve this id" case, where replacing
        /// a real name with a placeholder would lose the only human-readable trace of it. Pass
        /// an empty string to clear it. Returns true without writing anything if the current
        /// mapping doesn't cover manufacturer_id — unmapped = never written, mirroring ingest's
        /// own rule (docs/FIELD_OWNERSHIP.md section 2).
        /// </summary>
        public static bool WriteManufacturer(
            ModelDoc2 model,
            FieldMappingService fieldMapping,
            int? manufacturerId,
            string manufacturerName)
        {
            var keyProperty = fieldMapping.ResolvePropertyName(ItemKind.Bought, ItemField.ManufacturerId);
            if (keyProperty == null)
                return true;

            bool keyOk = WriteText(model, keyProperty, manufacturerId?.ToString(CultureInfo.InvariantCulture));

            if (manufacturerName == null)
                return keyOk;

            var labelProperty = fieldMapping.ResolvePropertyName(ItemKind.Bought, ItemField.ManufacturerName);
            if (labelProperty == null)
                return keyOk;

            bool labelOk = WriteTextInScopeOf(model, keyProperty, labelProperty, manufacturerName);

            return keyOk && labelOk;
        }

        /// <summary>
        /// Writes the article group reference keys and their resolved labels together. Same null
        /// convention as WriteManufacturer: null labels leave the stored ones untouched, an empty
        /// list clears them. Labels are expected to line up positionally with the ids. Same
        /// unmapped-field no-op as WriteManufacturer if article_group_ids isn't in the current
        /// mapping.
        /// </summary>
        public static bool WriteArticleGroups(
            ModelDoc2 model,
            FieldMappingService fieldMapping,
            IEnumerable<int> articleGroupIds,
            IEnumerable<string> articleGroupNames)
        {
            var keyProperty = fieldMapping.ResolvePropertyName(ItemKind.Bought, ItemField.ArticleGroupIds);
            if (keyProperty == null)
                return true;

            bool keyOk = WriteText(
                model,
                keyProperty,
                string.Join(
                    IdSeparator,
                    (articleGroupIds ?? Enumerable.Empty<int>())
                        .Select(id => id.ToString(CultureInfo.InvariantCulture))));

            if (articleGroupNames == null)
                return keyOk;

            var labelProperty = fieldMapping.ResolvePropertyName(ItemKind.Bought, ItemField.ArticleGroupNames);
            if (labelProperty == null)
                return keyOk;

            bool labelOk = WriteTextInScopeOf(
                model, keyProperty, labelProperty, string.Join(LabelSeparator, articleGroupNames));

            return keyOk && labelOk;
        }

        /// <summary>Made-item analogue of WriteArticleGroups, against ItemKind.Made's current
        /// mapping. Same null/unmapped-field conventions.</summary>
        public static bool WriteMadeItemArticleGroups(
            ModelDoc2 model,
            FieldMappingService fieldMapping,
            IEnumerable<int> articleGroupIds,
            IEnumerable<string> articleGroupNames)
        {
            var keyProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.ArticleGroupIds);
            if (keyProperty == null)
                return true;

            bool keyOk = WriteText(
                model,
                keyProperty,
                string.Join(
                    IdSeparator,
                    (articleGroupIds ?? Enumerable.Empty<int>())
                        .Select(id => id.ToString(CultureInfo.InvariantCulture))));

            if (articleGroupNames == null)
                return keyOk;

            var labelProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.ArticleGroupNames);
            if (labelProperty == null)
                return keyOk;

            bool labelOk = WriteTextInScopeOf(
                model, keyProperty, labelProperty, string.Join(LabelSeparator, articleGroupNames));

            return keyOk && labelOk;
        }

        /// <summary>
        /// Writes the made item's source-item reference key and its composed spec label
        /// together, the label into the key's own scope — same pattern as WriteManufacturer.
        /// Pass null for <paramref name="sourceItemName"/> to leave the stored label untouched
        /// (the id is set but the current lookup snapshot can't resolve it — see
        /// GladosTaskPaneControl.ResolveSourceItemLabel); pass an empty string to clear it,
        /// which is what an explicit Clear does for both the id and the label together. Returns
        /// true without writing anything if the current mapping doesn't cover source_item_id —
        /// unmapped = never written, mirroring ingest's own rule
        /// (docs/FIELD_OWNERSHIP.md section 2). Creating the source item itself, if the user
        /// picked "Create new..." in the picker, already happened against the Glados API before
        /// this is ever called — this only ever writes the resulting id/label to the document,
        /// same as every other WriteXxx helper here.
        /// </summary>
        public static bool WriteSourceItem(
            ModelDoc2 model,
            FieldMappingService fieldMapping,
            int? sourceItemId,
            string sourceItemName)
        {
            var keyProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.SourceItemId);
            if (keyProperty == null)
                return true;

            bool keyOk = WriteText(model, keyProperty, sourceItemId?.ToString(CultureInfo.InvariantCulture));

            if (sourceItemName == null)
                return keyOk;

            var labelProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.SourceItemName);
            if (labelProperty == null)
                return keyOk;

            bool labelOk = WriteTextInScopeOf(model, keyProperty, labelProperty, sourceItemName);

            return keyOk && labelOk;
        }

        /// <summary>
        /// Writes a customer item's customer data: the customer id with its name label in the
        /// id's own scope (same pattern as WriteSourceItem), then the customer's part number
        /// and revision. Pass null for <paramref name="customerName"/> to leave the stored label
        /// untouched (the id is set but the lookup snapshot can't resolve it); pass an empty
        /// string to clear it together with the id. Each field is written only when the
        /// current mapping covers it — unmapped = never written (docs/FIELD_OWNERSHIP.md
        /// section 2). Returns the names of the fields whose write failed.
        /// </summary>
        public static List<string> WriteCustomer(
            ModelDoc2 model,
            FieldMappingService fieldMapping,
            int? customerId,
            string customerName,
            string customerPartNumber,
            string customerRevision)
        {
            var failures = new List<string>();

            var keyProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.CustomerId);
            if (keyProperty != null)
            {
                if (!WriteText(model, keyProperty, customerId?.ToString(CultureInfo.InvariantCulture)))
                    failures.Add("Customer");

                var labelProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.CustomerName);
                if (customerName != null && labelProperty != null
                    && !WriteTextInScopeOf(model, keyProperty, labelProperty, customerName))
                    failures.Add("Customer Name");
            }

            var partNumberProperty =
                fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.CustomerPartNumber);
            if (partNumberProperty != null && !WriteText(model, partNumberProperty, customerPartNumber))
                failures.Add("Customer Part Number");

            var revisionProperty =
                fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.CustomerRevision);
            if (revisionProperty != null && !WriteText(model, revisionProperty, customerRevision))
                failures.Add("Customer Revision");

            return failures;
        }

        /// <summary>Reads Glados.Kind with the same document-level-then-active-configuration
        /// scope resolution as every other mapped property (Read), parsing it into an ItemKind when
        /// it holds one of the two valid wire values ("made"/"bought") — null when the
        /// property is absent, empty, or unrecognized. Tries the bought-item mapping first,
        /// then the made-item one: by default both point at the same "Glados.Kind" property,
        /// and which kind this document actually is is exactly the question being answered
        /// here, so neither can be assumed yet. Null (rather than throwing or guessing) when
        /// "kind" isn't in the current mapping for either — unmapped reads as "not set", the
        /// same as an empty property.</summary>
        public static ItemKind? ReadDocumentKind(ModelDoc2 model, FieldMappingService fieldMapping)
        {
            var propertyName =
                fieldMapping.ResolvePropertyName(ItemKind.Bought, ItemField.Kind)
                ?? fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.Kind);

            if (propertyName == null)
                return null;

            return ItemKindExtensions.TryParseWireValue(Read(model, propertyName), out var kind)
                ? (ItemKind?)kind
                : null;
        }

        /// <summary>
        /// Writes Glados.Kind = <paramref name="kind"/>'s wire value into every scope
        /// (EnumerateScopes: document level, then every configuration) — the same fan-out every
        /// other add-in-written property gets (WriteAllScopes), except write-once is enforced
        /// per scope rather than once for the whole document: a scope that already holds a
        /// *different* valid value refuses just that one scope and logs it, exactly like the
        /// single-scope refusal before the fan-out existed, while every other scope (a fresh
        /// configuration added since the last Save, for instance) still gets written normally —
        /// so a document whose kind was set before this addin fanned out configurations still
        /// gets caught up rather than being stuck refusing forever. Re-writing the same value
        /// that's already there is a harmless no-op. No-op (returns true) when "kind" isn't in
        /// the current mapping for <paramref name="kind"/> — unmapped = never written, mirroring
        /// every other WriteX helper here.
        /// </summary>
        public static bool WriteKind(ModelDoc2 model, FieldMappingService fieldMapping, ItemKind kind)
        {
            var propertyName = fieldMapping.ResolvePropertyName(kind, ItemField.Kind);
            if (propertyName == null)
                return true;

            var requested = kind.ToWireValue();
            bool ok = true;
            bool wroteAny = false;

            foreach (var scope in EnumerateScopes(model))
            {
                if (ItemKindExtensions.TryParseWireValue(TryGet(scope.Key, propertyName), out var existing)
                    && existing != kind)
                {
                    Log.Warning(
                        $"Refusing to change {propertyName} from '{existing.ToWireValue()}' to " +
                        $"'{requested}' in {scope.Value} — kind is immutable once set on a document.");
                    ok = false;
                    continue;
                }

                if (WriteToScope(scope.Key, propertyName, requested, scope.Value))
                    wroteAny = true;
                else
                    ok = false;
            }

            if (wroteAny)
                MarkDirty(model);

            return ok;
        }

        /// <summary>
        /// Sets "Child component display when used as a subassembly" (the configuration's Bill
        /// of Materials options) to Hide in every configuration of a bought assembly: it is one
        /// purchased item, so a parent assembly's BOM lists it as a single row, never its
        /// internals. Not a custom property but a per-configuration document setting — no field
        /// mapping or owner applies; it lives here because this class owns every document write
        /// and MarkDirty. Configuration names are read fresh on every call, like
        /// EnumerateScopes, so a configuration added since the last Save gets it on this one. A
        /// configuration already set to Hide is left untouched, so this never dirties the
        /// document on its own. Returns false if any configuration couldn't be read back as
        /// Hide; a failure in one never skips the rest.
        /// </summary>
        public static bool WriteHideChildComponentsInBom(ModelDoc2 model)
        {
            const int hide = (int)swChildComponentInBOMOption_e.swChildComponent_Hide;
            bool ok = true;
            bool changedAny = false;

            foreach (var configName in (model.GetConfigurationNames() as string[]) ?? new string[0])
            {
                var configuration = model.GetConfigurationByName(configName) as Configuration;
                if (configuration == null)
                {
                    Log.Debug($"ChildComponentDisplayInBOM: configuration '{configName}' not found");
                    ok = false;
                    continue;
                }

                if (configuration.ChildComponentDisplayInBOM == hide)
                    continue;

                configuration.ChildComponentDisplayInBOM = hide;
                bool verified = configuration.ChildComponentDisplayInBOM == hide;
                Log.Debug(
                    $"ChildComponentDisplayInBOM=Hide (configuration '{configName}') → verifiedOk={verified}");

                if (verified)
                    changedAny = true;
                else
                    ok = false;
            }

            if (changedAny)
                MarkDirty(model);

            return ok;
        }

        /// <summary>
        /// Save-time write for Glados.Mass (or whatever the current mapping resolves
        /// ItemField.Mass to, for <paramref name="kind"/>): preserves the template's linked
        /// expression instead of clobbering it with a resolved number — see
        /// WriteLinkedNumericField and docs/DESIGN.md "Linked expressions on Save".
        /// <paramref name="massGrams"/> is whatever the task pane currently shows and is written
        /// only when <paramref name="userChanged"/> says the user edited it since the document
        /// was loaded. The made-item tab has an edit control for mass and detects that against
        /// the loaded baseline (GladosTaskPaneControl.ReadMadeItemFromUi /
        /// SaveMadeItemPropertiesAsync); the bought-item tab still has none, so for
        /// <see cref="ItemKind.Bought"/> <paramref name="userChanged"/> is always false today and
        /// the call only propagates document level into the configurations. No-op (true) when
        /// mass isn't in the current mapping for <paramref name="kind"/> — unmapped = never
        /// written.
        /// </summary>
        public static bool WriteMass(
            ModelDoc2 model, FieldMappingService fieldMapping, ItemKind kind, int? massGrams, bool userChanged)
        {
            var propertyName = fieldMapping.ResolvePropertyName(kind, ItemField.Mass);
            return propertyName == null || WriteLinkedNumericField(model, propertyName, massGrams, userChanged);
        }

        /// <summary>Made-item analogue of WriteMass for Glados.Material. See
        /// WriteLinkedTextField.</summary>
        public static bool WriteMaterial(
            ModelDoc2 model, FieldMappingService fieldMapping, string material, bool userChanged)
        {
            var propertyName = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.Material);
            return propertyName == null || WriteLinkedTextField(model, propertyName, material, userChanged);
        }

        /// <summary>Made-item analogue of WriteMass for Glados.BaseSize. See
        /// WriteLinkedTextField.</summary>
        public static bool WriteBaseSize(
            ModelDoc2 model, FieldMappingService fieldMapping, string baseSize, bool userChanged)
        {
            var propertyName = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.BaseSize);
            return propertyName == null || WriteLinkedTextField(model, propertyName, baseSize, userChanged);
        }

        /// <summary>
        /// The made-item tab's "restore calculated value" action for Mass: undoes whatever
        /// override WriteMass's userChanged path wrote (or a template that never had a link to
        /// begin with) by re-linking the mapped property to SolidWorks' own SW-Mass calculation
        /// — see RestoreCalculatedLink. No-op (true) when mass isn't in the current mapping for
        /// <paramref name="kind"/> — unmapped = never written, mirroring WriteMass. Takes
        /// <paramref name="kind"/> like WriteMass does (mass is mapped for both item kinds) even
        /// though only the made-item tab has a Calc. button today.
        /// </summary>
        public static bool RestoreCalculatedMass(ModelDoc2 model, FieldMappingService fieldMapping, ItemKind kind)
        {
            var propertyName = fieldMapping.ResolvePropertyName(kind, ItemField.Mass);
            return propertyName == null || RestoreCalculatedLink(model, propertyName, SwMassPropertyName);
        }

        /// <summary>Made-item analogue of RestoreCalculatedMass for Glados.Material /
        /// SW-Material. See RestoreCalculatedLink.</summary>
        public static bool RestoreCalculatedMaterial(ModelDoc2 model, FieldMappingService fieldMapping)
        {
            var propertyName = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.Material);
            return propertyName == null || RestoreCalculatedLink(model, propertyName, SwMaterialPropertyName);
        }

        /// <summary>
        /// Re-links <paramref name="propertyName"/> (Mass or Material's mapped custom property)
        /// to SolidWorks' own calculation, discarding whatever plain value currently sits there
        /// — the user's override from WriteLinkedNumericField/WriteLinkedTextField's userChanged
        /// path, or a value that was never linked to begin with. <paramref name="swPropertyName"/>
        /// is the SolidWorks-reserved property this add-in restores against ("SW-Mass" or
        /// "SW-Material" — see SwMassPropertyName/SwMaterialPropertyName), the same two names
        /// TryRewriteSimpleLinkForConfig already knows how to re-scope per configuration.
        ///
        /// Builds the document form of the link, `"&lt;swPropertyName&gt;@&lt;file&gt;"` — the
        /// same quoted-link-string shape MirrorDocumentValueToConfigurations already writes for
        /// the configuration form, proven to be recognized by SolidWorks as a live link when set
        /// through Set2/Add3, not just read back from Get6. `&lt;file&gt;` is
        /// Path.GetFileName(model.GetPathName()), or model.GetTitle() for a document that has
        /// never been saved (GetPathName returns "" in that case): SolidWorks' own GetTitle
        /// returns the document's temporary in-memory name ("Part1", no extension) until the
        /// first Save gives it a real file name, which is exactly the file segment SolidWorks
        /// itself would put into such a document's own links — see the SolidWorks API's GetTitle
        /// documentation ("the document pathname, or, if the document has never been saved, its
        /// default name").
        ///
        /// Writes the document form to document level (WriteToScope, not the WriteAllScopes fan-
        /// out — that would put the *same* literal into every configuration, which is wrong for a
        /// link: each configuration needs its own `@@&lt;config&gt;@` form) and reuses
        /// MirrorDocumentValueToConfigurations to fan the correctly-rewritten per-configuration
        /// form out to every name GetConfigurationNames() currently returns — the same primitive
        /// WriteLinkedTextField/WriteLinkedNumericField already use for the no-override case,
        /// rather than a second copy of that fan-out. propertyName is refused (GuardNotPdmOwned)
        /// like every other write in this class. Marks the document dirty; returns false if
        /// either the document-level write or any configuration's write failed.
        /// </summary>
        public static bool RestoreCalculatedLink(ModelDoc2 model, string propertyName, string swPropertyName)
        {
            GuardNotPdmOwned(propertyName);

            var file = LinkFileName(model);
            var documentLink = "\"" + swPropertyName + "@" + file + "\"";

            var cpmDoc = model.Extension.CustomPropertyManager[""];
            bool docOk = WriteToScope(cpmDoc, propertyName, documentLink, "document level");
            if (docOk)
                MarkDirty(model);

            bool configsOk = MirrorDocumentValueToConfigurations(model, propertyName, documentLink, isLinked: true);

            return docOk && configsOk;
        }

        /// <summary>The "@file" token a SolidWorks-authored link uses: the on-disk file name
        /// (Path.GetFileName(model.GetPathName())), or, for a document that has never been saved
        /// (GetPathName returns ""), model.GetTitle() — see RestoreCalculatedLink's doc comment
        /// for why GetTitle is the right fallback there.</summary>
        private static string LinkFileName(ModelDoc2 model)
        {
            var pathName = model.GetPathName();
            return !string.IsNullOrEmpty(pathName) ? Path.GetFileName(pathName) : model.GetTitle();
        }

        /// <summary>True when <paramref name="propertyName"/>'s document-level raw value (Get6's
        /// ValOut, via TryGetRaw) is a linked expression — the calculated-by-SolidWorks state the
        /// two "restore calculated value" buttons exist to bring back — false when it holds a
        /// plain value (the user's override) or the property doesn't exist at document level at
        /// all. Reuses the same isLinked flag WriteLinkedTextField/WriteLinkedNumericField already
        /// read off Get6 rather than re-deriving "is this a link" from the raw string a second
        /// way. Document level only, matching every other read in this class (ResolveScope):
        /// what a configuration's own copy says is never authoritative on its own.</summary>
        public static bool IsCalculated(ModelDoc2 model, string propertyName)
        {
            string rawValue, resolvedValue;
            bool isLinked;
            return TryGetRaw(
                    model.Extension.CustomPropertyManager[""], propertyName,
                    out rawValue, out resolvedValue, out isLinked)
                && isLinked;
        }

        /// <summary>
        /// Save-time write for a template-linked text field (Material, BaseSize). The add-in
        /// authors these values only on an explicit user override —
        /// <paramref name="userChanged"/>, which the caller decides against the value it loaded
        /// from the document, never against the live resolved value, because a rebuild can
        /// change the latter between load and Save and that must not be mistaken for an edit.
        /// On override the literal goes to document level and every configuration via WriteText.
        /// Otherwise document level is left untouched and whatever it holds is propagated to the
        /// configurations (MirrorDocumentValueToConfigurations): a linked expression (per the
        /// isLinked flag TryGetRaw reads off Get6) rewritten per configuration, a plain value
        /// copied as is. A property missing at document level is not invented. See docs/DESIGN.md
        /// "Linked expressions on Save".
        /// </summary>
        private static bool WriteLinkedTextField(
            ModelDoc2 model, string fieldName, string uiValue, bool userChanged)
        {
            GuardNotPdmOwned(fieldName);

            if (userChanged)
                return WriteText(model, fieldName, uiValue ?? string.Empty);

            string rawValue;
            bool isLinked;
            if (!TryGetRaw(model.Extension.CustomPropertyManager[""], fieldName, out rawValue, out _, out isLinked))
                return true;

            return MirrorDocumentValueToConfigurations(model, fieldName, rawValue, isLinked);
        }

        /// <summary>Numeric analogue of WriteLinkedTextField, for Mass. On override the literal
        /// value is written as a culture-invariant integer string — ParseMassGrams accepts that
        /// back regardless of the running locale.</summary>
        private static bool WriteLinkedNumericField(
            ModelDoc2 model, string fieldName, int? massGrams, bool userChanged)
        {
            GuardNotPdmOwned(fieldName);

            if (userChanged)
                return WriteText(model, fieldName, massGrams?.ToString(CultureInfo.InvariantCulture));

            string rawValue;
            bool isLinked;
            if (!TryGetRaw(model.Extension.CustomPropertyManager[""], fieldName, out rawValue, out _, out isLinked))
                return true;

            return MirrorDocumentValueToConfigurations(model, fieldName, rawValue, isLinked);
        }

        /// <summary>Propagates the document-level raw value into every configuration's scope
        /// only — never document level, which already holds it. <paramref name="isLinked"/> is
        /// the authoritative "is this a link" flag TryGetRaw reads off Get6 at the same time as
        /// rawValue, not a re-derived guess from the string. A linked expression is rewritten per
        /// configuration (TryRewriteSimpleLinkForConfig, single plain links only) so each
        /// configuration resolves its own value; a plain value is copied verbatim. The
        /// no-override half of WriteLinkedTextField/WriteLinkedNumericField.</summary>
        private static bool MirrorDocumentValueToConfigurations(
            ModelDoc2 model, string fieldName, string rawValue, bool isLinked)
        {
            bool ok = true;
            bool wroteAny = false;

            foreach (var configName in (model.GetConfigurationNames() as string[]) ?? new string[0])
            {
                string configValue;
                if (!isLinked)
                {
                    configValue = rawValue;
                }
                else if (!TryRewriteSimpleLinkForConfig(rawValue, configName, out configValue))
                {
                    // A formula, several links, a dimension link — anything but the one
                    // simple link shape — is the template author's, and a blind rewrite
                    // would corrupt it. The configuration copy is left exactly as it is.
                    Log.Debug(
                        $"{fieldName} holds an expression this add-in doesn't rewrite; "
                        + $"configuration '{configName}' left untouched: {rawValue}");
                    continue;
                }

                var cpm = model.Extension.CustomPropertyManager[configName];
                if (WriteToScope(cpm, fieldName, configValue, $"configuration '{configName}'"))
                    wroteAny = true;
                else
                    ok = false;
            }

            if (wroteAny)
                MarkDirty(model);

            return ok;
        }

        /// <summary>Exactly one quoted property link and nothing else: `"Name@File"` (document
        /// form) or `"Name@@Config@File"` (configuration form). Name, config and file contain
        /// no quote and no '@', so a dimension link (`"D1@Sketch1@File"`), a formula, or a
        /// concatenation of several links never matches. These two patterns are the only place
        /// regex still decides anything about a linked value — whether a link Get6 has already
        /// told the caller about (isLinked, from TryGetRaw) has this one rewritable shape, never
        /// whether the value is linked at all.</summary>
        private static readonly Regex SimpleDocumentLink =
            new Regex("^\"(?<name>[^\"@]+)@(?<file>[^\"@]+)\"$", RegexOptions.CultureInvariant);

        private static readonly Regex SimpleConfigurationLink =
            new Regex("^\"(?<name>[^\"@]+)@@(?<config>[^\"@]+)@(?<file>[^\"@]+)\"$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Rewrites the one link shape this add-in understands into the per-configuration form
        /// SolidWorks uses for the same link scoped to one configuration: `"SW-Mass@file"`
        /// becomes `"SW-Mass@@&lt;configName&gt;@file"`, and `"SW-Material@@Default@file"` has
        /// its configuration name replaced. Returns false — and the caller leaves the
        /// configuration copy alone — for every other expression: a bounding-box or dimension
        /// formula, several links concatenated, arithmetic around a link. Those belong to the
        /// template author, and a blind rewrite corrupts them (the first '@' of
        /// `"D1@Sketch1@file"` is not where a configuration goes). See docs/DESIGN.md "Linked
        /// expressions on Save". The return value doubles as the shape check for a value already
        /// known to be linked (MirrorDocumentValueToConfigurations only calls this once
        /// TryGetRaw's isLinked flag is true) — pure string matching against
        /// SimpleDocumentLink/SimpleConfigurationLink above, no SolidWorks types involved, so it
        /// stays unit-testable on its own.
        /// </summary>
        internal static bool TryRewriteSimpleLinkForConfig(
            string rawValue, string configName, out string rewritten)
        {
            var configMatch = SimpleConfigurationLink.Match(rawValue);
            if (configMatch.Success)
            {
                rewritten = "\"" + configMatch.Groups["name"].Value + "@@" + configName + "@"
                    + configMatch.Groups["file"].Value + "\"";
                return true;
            }

            var documentMatch = SimpleDocumentLink.Match(rawValue);
            if (documentMatch.Success)
            {
                rewritten = "\"" + documentMatch.Groups["name"].Value + "@@" + configName + "@"
                    + documentMatch.Groups["file"].Value + "\"";
                return true;
            }

            rewritten = null;
            return false;
        }

        /// <summary>
        /// Normalizes group indexes (NormalizeProcessGroups) and validates the result
        /// (ValidateProcessGrouping), then writes Glados.Processes — single-line JSON, ids
        /// only, position = array order (.playground/made-item-process-grouping-plan.md §3.2)
        /// — and its process-names label (Glados.ProcessNames by default — see
        /// FieldMappingService.ResolvePropertyName) into the same scope. Returns the
        /// normalized steps via <paramref name="normalizedSteps"/> so the caller can adopt them
        /// as its new baseline without recomputing grouping itself; this always happens even
        /// when the writes below are a no-op. A no-op (returns true) when "processes" isn't in
        /// the current mapping, or — once it is — when its label has no resolved property of
        /// its own: unmapped = never written, mirroring WriteArticleGroups. Throws
        /// InvalidOperationException — never silently — if the steps fail
        /// ValidateProcessGrouping; that's a bug in the caller (grouping/step-editor UI), not a
        /// condition the user can fix by retrying the save.
        /// </summary>
        public static bool WriteProcesses(
            ModelDoc2 model,
            FieldMappingService fieldMapping,
            IEnumerable<ProcessStep> steps,
            out List<ProcessStep> normalizedSteps)
        {
            normalizedSteps = NormalizeProcessGroups(steps);
            ValidateProcessGrouping(normalizedSteps);

            var processesProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.Processes);
            if (processesProperty == null)
                return true;

            var json = JsonConvert.SerializeObject(normalizedSteps, GladosApiClient.JsonSettings);
            bool processesOk = WriteText(model, processesProperty, json);

            var labelProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.ProcessNames);
            if (labelProperty == null)
                return processesOk;

            var label = BuildProcessNamesLabel(normalizedSteps);
            bool labelOk = WriteTextInScopeOf(model, processesProperty, labelProperty, label);

            return processesOk && labelOk;
        }

        /// <summary>
        /// Writes <paramref name="kind"/>'s feature-tree template (FieldMappingService.
        /// MadeFeatureTreeTemplate/BoughtFeatureTreeTemplate) verbatim into
        /// feature_tree_representation's mapped property. The template links to other custom
        /// properties with $PRP:"name" and SolidWorks resolves those links itself, so the
        /// feature tree, BOMs and the datacard follow any later change to a linked property,
        /// including a part number or revision PDM assigns, without another Save. A no-op
        /// (returns true) when feature_tree_representation isn't in the current mapping:
        /// unmapped = never written, mirroring every other WriteXxx helper here.
        /// </summary>
        public static bool WriteFeatureTreeRepresentation(
            ModelDoc2 model, FieldMappingService fieldMapping, ItemKind kind)
        {
            var propertyName = fieldMapping.ResolvePropertyName(kind, ItemField.FeatureTreeRepresentation);
            if (propertyName == null)
                return true;

            var template = kind == ItemKind.Made
                ? fieldMapping.MadeFeatureTreeTemplate
                : fieldMapping.BoughtFeatureTreeTemplate;
            return WriteText(model, propertyName, template);
        }

        /// <summary>
        /// C# port of the backend's made_item_rules.normalize_process_groups: recomputes
        /// contiguous, 0-based group indexes for steps already in their final position order
        /// (list order = array order = position), preserving adjacent same-group runs and
        /// splitting any run that was torn apart (e.g. by a reorder or a delete) — a new group
        /// starts whenever a step's current GroupIndex differs from its predecessor's *original*
        /// GroupIndex (not the newly assigned one), exactly like the backend. Returns new
        /// ProcessStep instances in the same order; never mutates <paramref name="ordered"/>.
        /// </summary>
        public static List<ProcessStep> NormalizeProcessGroups(IEnumerable<ProcessStep> ordered)
        {
            var result = new List<ProcessStep>();
            int nextIndex = -1;
            int? prevGroupIndex = null;

            foreach (var step in ordered ?? Enumerable.Empty<ProcessStep>())
            {
                if (prevGroupIndex == null || step.GroupIndex != prevGroupIndex.Value)
                    nextIndex++;

                result.Add(new ProcessStep
                {
                    Role = step.Role,
                    FabricationMethodId = step.FabricationMethodId,
                    InspectionMethodId = step.InspectionMethodId,
                    GroupIndex = nextIndex,
                    EstimatedSetupMinutes = step.EstimatedSetupMinutes,
                    EstimatedMinutesPerPiece = step.EstimatedMinutesPerPiece,
                    Note = step.Note,
                    MethodName = step.MethodName,
                });

                prevGroupIndex = step.GroupIndex;
            }

            return result;
        }

        /// <summary>
        /// C# port of the backend's made_item_rules.assert_valid_step_role plus the
        /// inspection-standalone rule from validate_process_grouping: every step must carry
        /// exactly one method id matching its Role (a FABRICATION step needs
        /// FabricationMethodId and no InspectionMethodId, and vice versa), and an
        /// INSPECTION-role step can never share a group with any other step — a QA checkpoint
        /// is always its own routing position. Intended to run on an already-normalized list
        /// (see NormalizeProcessGroups) right before it's serialized.
        /// </summary>
        public static void ValidateProcessGrouping(IReadOnlyList<ProcessStep> steps)
        {
            if (steps == null)
                return;

            foreach (var step in steps)
            {
                if (step.Role == StepRole.Fabrication)
                {
                    if (step.FabricationMethodId == null)
                        throw new InvalidOperationException(
                            "A fabrication-role process requires a fabrication method id.");
                    if (step.InspectionMethodId != null)
                        throw new InvalidOperationException(
                            "A fabrication-role process must not have an inspection method id.");
                }
                else
                {
                    if (step.FabricationMethodId != null)
                        throw new InvalidOperationException(
                            "An inspection-role process must not have a fabrication method id.");
                    if (step.InspectionMethodId == null)
                        throw new InvalidOperationException(
                            "An inspection-role process requires an inspection method id.");
                }
            }

            foreach (var group in steps.GroupBy(step => step.GroupIndex))
            {
                var members = group.ToList();
                if (members.Count > 1 && members.Any(step => step.Role == StepRole.Inspection))
                    throw new InvalidOperationException(
                        $"Group {group.Key} contains an inspection step and cannot hold more than one process.");
            }
        }

        /// <summary>Reads Glados.Processes tolerantly: an empty/missing property is the normal
        /// "nothing routed yet" state (unreadable stays false), while text that isn't valid
        /// JSON for the §3.2 encoding yields an empty list with unreadable set — never throws,
        /// so a garbled property can't take down the whole made-item load.</summary>
        internal static List<ProcessStep> ParseProcesses(string json, out bool unreadable)
        {
            unreadable = false;

            if (string.IsNullOrWhiteSpace(json))
                return new List<ProcessStep>();

            try
            {
                var steps = JsonConvert.DeserializeObject<List<ProcessStep>>(json, GladosApiClient.JsonSettings);
                return steps ?? new List<ProcessStep>();
            }
            catch (JsonException ex)
            {
                Log.Warning($"Could not parse Glados.Processes '{json}'", ex);
                unreadable = true;
                return new List<ProcessStep>();
            }
        }

        /// <summary>Builds the locale-neutral Glados.ProcessNames label from already-grouped,
        /// position-ordered steps: " → " between groups, " + " within a group — deliberately
        /// not the frontend's localized "und"/"and" (GroupingChain.vue), since a stored file
        /// property must not depend on the author's UI language (plan §3.2). Each step shows
        /// its runtime MethodName, falling back to "#&lt;id&gt;" when unresolved (offline with
        /// an empty lookup cache).</summary>
        internal static string BuildProcessNamesLabel(IEnumerable<ProcessStep> steps)
        {
            var groups = (steps ?? Enumerable.Empty<ProcessStep>())
                .GroupBy(step => step.GroupIndex)
                .OrderBy(group => group.Key)
                .Select(group => string.Join(" + ", group.Select(StepLabel)));

            return string.Join(" → ", groups);
        }

        private static string StepLabel(ProcessStep step)
        {
            if (!string.IsNullOrWhiteSpace(step.MethodName))
                return step.MethodName;

            return step.MethodId.HasValue
                ? "#" + step.MethodId.Value.ToString(CultureInfo.InvariantCulture)
                : "#?";
        }

        /// <summary>PDM.* properties come from the vault or from SolidWorks itself. Writing one
        /// is a coding mistake rather than a runtime condition, so it fails loudly instead of
        /// being reported to the user as a failed save.</summary>
        private static void GuardNotPdmOwned(string fieldName)
        {
            if (BoughtItemProperties.PropertyNames.IsPdmOwned(fieldName))
                throw new ArgumentException(
                    $"'{fieldName}' is PDM-owned and must never be written by this addin.",
                    nameof(fieldName));
        }

        /// <summary>Returns the CustomPropertyManager whose copy of the property should be
        /// read: the first scope holding a non-empty value, document level before the active
        /// configuration; if both copies are empty, whichever exists (document level first);
        /// null if it exists in neither. Document level is canonical — it's what the backend's
        /// PDM ingest reads and what a fan-out write (WriteAllScopes) always puts an add-in
        /// property into — but an *empty* document-level copy must not hide a configuration
        /// value: PDM writes its own variables (PDM.PartNumber, PDM.Revision) into the
        /// configurations when the variable's Configurations mapping is blank, and a document
        /// template or an earlier "@" mapping can leave an empty document-level property next
        /// to them. See docs/DESIGN.md "Write scopes and timing".
        /// </summary>
        private static ICustomPropertyManager ResolveScope(
            ModelDoc2 model,
            string fieldName,
            out string scope)
        {
            var cpmDoc = model.Extension.CustomPropertyManager[""];
            var docValue = TryGet(cpmDoc, fieldName);
            if (!string.IsNullOrWhiteSpace(docValue))
            {
                scope = "document level";
                return cpmDoc;
            }

            // Drawings have no configurations: GetActiveConfiguration() returns null there, and
            // since embedded interop types it as dynamic, dereferencing it would throw a
            // RuntimeBinderException rather than a NullReferenceException.
            var activeConfig = (Configuration)model.GetActiveConfiguration();
            var cpmActive = activeConfig != null
                ? model.Extension.CustomPropertyManager[activeConfig.Name]
                : null;
            var activeValue = cpmActive != null ? TryGet(cpmActive, fieldName) : null;
            if (!string.IsNullOrWhiteSpace(activeValue))
            {
                scope = "active configuration";
                return cpmActive;
            }

            if (docValue != null)
            {
                scope = "document level";
                return cpmDoc;
            }

            if (activeValue != null)
            {
                scope = "active configuration";
                return cpmActive;
            }

            scope = null;
            return null;
        }

        private static bool SetAndVerify(
            ICustomPropertyManager cpm,
            string fieldName,
            string value,
            string scope)
        {
            int setResult = cpm.Set2(fieldName, value);
            bool ok = setResult == (int)swCustomInfoSetResult_e.swCustomInfoSetResult_OK;
            Log.Debug($"Set2 {fieldName}='{value}' ({scope}) → result={setResult}, ok={ok}");
            return ok;
        }

        private static bool AddAndVerify(
            ICustomPropertyManager cpm,
            string fieldName,
            string value,
            string scope)
        {
            // Add3's result code isn't checked against a specific enum here: its exact
            // success sentinel isn't confirmed against this project's installed SolidWorks
            // API version. Logged raw for diagnosis; the write is verified by reading the
            // property straight back instead, comparing the raw value, since a linked value
            // (a $PRP:"name" link, a "SW-Mass@file" link) resolves to different text than
            // was written.
            int addResult = cpm.Add3(
                fieldName,
                (int)swCustomInfoType_e.swCustomInfoText,
                value,
                (int)swCustomPropertyAddOption_e.swCustomPropertyOnlyIfNew);

            string rawValue, resolvedValue;
            bool isLinked;
            bool ok = TryGetRaw(cpm, fieldName, out rawValue, out resolvedValue, out isLinked)
                && rawValue == value;
            Log.Debug(
                $"Add3 {fieldName}='{value}' ({scope}, new) → rawResult={addResult}, verifiedOk={ok}");
            return ok;
        }

        /// <summary>
        /// A cheap snapshot of every property the task pane reads, for noticing a change made
        /// behind its back (a PDM data card, File &gt; Properties, a macro) — compare two
        /// results for equality, the content itself means nothing. Covers every property the
        /// mapping resolves for either kind (Server-owned write-backs excluded, the pane never
        /// reads them) plus the literal PDM.PartNumber, PDM.Revision, Glados.Kind and the
        /// legacy mass name, each in both scopes a read can land on: document level and the
        /// active configuration. Uses the raw value (Get6's ValOut), not the resolved one, so a
        /// linked expression such as "SW-Mass@part.SLDPRT" stays the same across rebuilds and
        /// a recalculation never looks like an outside edit.
        /// </summary>
        public static string ReadPropertyFingerprint(ModelDoc2 model, FieldMappingService fieldMapping)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal)
            {
                "PDM.PartNumber",
                "PDM.Revision",
                "Glados.Kind",
                BoughtItemProperties.PropertyNames.MassFallbackLegacy,
            };

            foreach (ItemKind kind in new[] { ItemKind.Bought, ItemKind.Made })
            {
                foreach (ItemField field in Enum.GetValues(typeof(ItemField)))
                {
                    var entry = fieldMapping.Resolve(kind, field);
                    if (entry?.PropertyName != null && entry.Owner != FieldOwner.Server)
                        names.Add(entry.PropertyName);
                }
            }

            var cpmDoc = model.Extension.CustomPropertyManager[""];
            // Same null guard as ResolveScope: drawings have no configurations.
            var activeConfig = (Configuration)model.GetActiveConfiguration();
            var cpmActive = activeConfig != null
                ? model.Extension.CustomPropertyManager[activeConfig.Name]
                : null;

            var fingerprint = new StringBuilder();
            fingerprint.Append(activeConfig != null ? activeConfig.Name : string.Empty).Append('\u001e');
            foreach (var name in names)
            {
                fingerprint.Append(name).Append('\u001f');
                AppendRaw(fingerprint, cpmDoc, name);
                fingerprint.Append('\u001f');
                if (cpmActive != null)
                    AppendRaw(fingerprint, cpmActive, name);
                fingerprint.Append('\u001e');
            }

            return fingerprint.ToString();
        }

        /// <summary>The raw value, or a marker distinct from any value when the property is
        /// absent — so deleting a property counts as a change, even an empty one.</summary>
        private static void AppendRaw(StringBuilder fingerprint, ICustomPropertyManager cpm, string name)
        {
            string rawValue, resolvedValue;
            bool isLinked;
            if (TryGetRaw(cpm, name, out rawValue, out resolvedValue, out isLinked))
                fingerprint.Append('=').Append(rawValue ?? string.Empty);
            else
                fingerprint.Append('\u0000');
        }

        /// <summary>Prefers the document-level value, falls back to the active configuration's
        /// — see ResolveScope, which this delegates to.</summary>
        public static string Read(ModelDoc2 model, string fieldName)
        {
            string scope;
            var cpm = ResolveScope(model, fieldName, out scope);
            return cpm != null ? TryGet(cpm, fieldName) ?? string.Empty : string.Empty;
        }

        /// <summary>Read order: the mapped property for <paramref name="kind"/> (Glados.Mass by
        /// default — see the mass revert in docs/FIELD_OWNERSHIP.md section 1 row 1), then the
        /// literal Glados.Mass name (MassFallbackLegacy) for documents written under a server
        /// mapping that points mass somewhere else. Neither is ever written from here.</summary>
        private static int? ReadMassGrams(ModelDoc2 model, FieldMappingService fieldMapping, ItemKind kind)
        {
            var text = ReadIfMapped(model, fieldMapping.ResolvePropertyName(kind, ItemField.Mass));

            if (string.IsNullOrWhiteSpace(text))
                text = Read(model, BoughtItemProperties.PropertyNames.MassFallbackLegacy);

            return ParseMassGrams(text);
        }

        /// <summary>Reads propertyName, or returns "" without touching the document when
        /// there's no mapped property to read — an unmapped field renders as absent rather
        /// than attempting a lookup against a null property name.</summary>
        private static string ReadIfMapped(ModelDoc2 model, string propertyName)
        {
            return propertyName != null ? Read(model, propertyName) : string.Empty;
        }

        private static bool Exists(ICustomPropertyManager cpm, string fieldName)
        {
            return TryGet(cpm, fieldName) != null;
        }

        /// <summary>The resolved value (Get6's ResolvedValOut) — what every display/read path
        /// other than the linked-field write helpers wants. Delegates to TryGetRaw.</summary>
        private static string TryGet(ICustomPropertyManager cpm, string fieldName)
        {
            string rawValue, resolvedValue;
            bool isLinked;
            return TryGetRaw(cpm, fieldName, out rawValue, out resolvedValue, out isLinked) ? resolvedValue : null;
        }

        /// <summary>Get6's raw form: <paramref name="rawValue"/> (ValOut — the unresolved
        /// expression text for a linked property, e.g. `"SW-Mass@part.SLDPRT"`, or just the
        /// literal value for a plain one) alongside <paramref name="resolvedValue"/>
        /// (ResolvedValOut, what TryGet/Read normally return) and <paramref name="isLinked"/>,
        /// the authoritative answer to "is this a link at all" — Get6's own LinkToProperty
        /// out-param, true when the value links to another property, OR-ed with WasResolved
        /// having produced a resolved value that differs from the raw one (a link SolidWorks
        /// resolved but didn't flag as LinkToProperty, e.g. a dimension link). Replaces the old
        /// regex-on-the-raw-string heuristic: Get6 already knows the answer, so nothing here
        /// needs to guess it from the text. Only the linked-field write path
        /// (WriteLinkedTextField/WriteLinkedNumericField, via MirrorDocumentValueToConfigurations)
        /// needs rawValue/isLinked — everything that reads for display or comparison elsewhere
        /// keeps using the resolved value via TryGet. Returns false (all three out values
        /// null/false) if the property doesn't exist in cpm at all.</summary>
        private static bool TryGetRaw(
            ICustomPropertyManager cpm, string fieldName,
            out string rawValue, out string resolvedValue, out bool isLinked)
        {
            bool wasResolved, linkToProperty;
            int found = cpm.Get6(
                fieldName, false, out rawValue, out resolvedValue, out wasResolved, out linkToProperty);

            if (found == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent)
            {
                rawValue = null;
                resolvedValue = null;
                isLinked = false;
                return false;
            }

            isLinked = rawValue != null
                && (linkToProperty
                    || (wasResolved && !string.Equals(rawValue, resolvedValue, StringComparison.Ordinal)));
            return true;
        }

        internal static int? ParseIntOrNull(string value)
        {
            int result;
            return int.TryParse(value, out result) ? (int?)result : null;
        }

        /// <summary>Parses a mass value into whole grams. Glados.Mass resolves from a linked
        /// SW-Mass expression and document units are MMGS, so the text is a bare decimal already
        /// in grams — formatted by the local SolidWorks in the running locale, hence "1234,56" on
        /// a de-AT machine. Anything non-numeric returns null rather than being coerced: a unit
        /// showing up there would mean the document units changed, which is worth surfacing.
        /// Public because the made-item task pane's Mass text box (free text, per docs/DESIGN.md
        /// custom property convention) reuses this same tolerant parsing for whatever the user
        /// types, rather than restricting the control to a stricter numeric format.</summary>
        public static int? ParseMassGrams(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            const NumberStyles Styles = NumberStyles.Float | NumberStyles.AllowThousands;
            var text = value.Trim();

            double parsed;
            if (!double.TryParse(text, Styles, CultureInfo.CurrentCulture, out parsed)
                && !double.TryParse(text, Styles, CultureInfo.InvariantCulture, out parsed))
            {
                Log.Debug($"Could not parse mass '{value}'");
                return null;
            }

            double grams = Math.Round(parsed, MidpointRounding.AwayFromZero);
            if (grams < int.MinValue || grams > int.MaxValue)
            {
                Log.Debug($"Mass '{value}' is out of range");
                return null;
            }

            return (int)grams;
        }

        internal static List<int> ParseIntList(string value)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            foreach (var part in value.Split(','))
            {
                int id;
                if (int.TryParse(part.Trim(), out id))
                    result.Add(id);
            }

            return result;
        }

        internal static List<string> ParseLabelList(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            foreach (var part in value.Split(';'))
            {
                var label = part.Trim();
                if (label.Length > 0)
                    result.Add(label);
            }

            return result;
        }
    }
}

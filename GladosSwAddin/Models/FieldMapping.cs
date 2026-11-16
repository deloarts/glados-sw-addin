using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace GladosSwAddin.Models
{
    /// <summary>Which catalogue kind a field-mapping entry belongs to. Made-item entries are
    /// synced and defaulted the same as bought-item ones, even though only the bought-item
    /// read/write path is wired up to them yet — see the NON-GOALS list in
    /// docs/FIELD_OWNERSHIP.md.</summary>
    public enum ItemKind
    {
        Made,
        Bought,
    }

    /// <summary>Who writes the field's value, mirroring the backend's field-ownership
    /// document. Unknown is deliberately first (enum value 0, the fallback the tolerant
    /// StringEnumConverter in GladosApiClient.JsonSettings returns for an owner string it
    /// doesn't recognize) so a future owner category the add-in doesn't know about yet is
    /// never silently mistaken for one of the real ones — see docs/FIELD_OWNERSHIP.md
    /// section 2, "Tolerant parsing".</summary>
    public enum FieldOwner
    {
        Unknown,

        [EnumMember(Value = "pdm")]
        Pdm,

        [EnumMember(Value = "addin")]
        AddIn,

        [EnumMember(Value = "addin_web")]
        AddInWeb,

        [EnumMember(Value = "web")]
        Web,

        [EnumMember(Value = "server")]
        Server,
    }

    /// <summary>The syncable fields across both item kinds, PascalCased from the backend's
    /// snake_case field names (part_number -> PartNumber). This enum exists only for typed
    /// call sites (FieldMappingService.Resolve(kind, field)) — the mapping itself is keyed by
    /// the raw string (see FieldMappingEntry.Field) so a server field this build doesn't have
    /// a member for yet still round-trips instead of being dropped.</summary>
    public enum ItemField
    {
        [EnumMember(Value = "part_number")]
        PartNumber,

        [EnumMember(Value = "revision")]
        Revision,

        [EnumMember(Value = "revision_date")]
        RevisionDate,

        [EnumMember(Value = "revision_note")]
        RevisionNote,

        [EnumMember(Value = "article_name")]
        ArticleName,

        [EnumMember(Value = "material")]
        Material,

        [EnumMember(Value = "mass")]
        Mass,

        [EnumMember(Value = "base_size")]
        BaseSize,

        [EnumMember(Value = "note")]
        Note,

        [EnumMember(Value = "weblink")]
        Weblink,

        [EnumMember(Value = "article_group_ids")]
        ArticleGroupIds,

        [EnumMember(Value = "bought_item_id")]
        BoughtItemId,

        [EnumMember(Value = "source_item_id")]
        SourceItemId,

        [EnumMember(Value = "processes")]
        Processes,

        [EnumMember(Value = "status")]
        Status,

        [EnumMember(Value = "successor_item_id")]
        SuccessorItemId,

        [EnumMember(Value = "approver_id")]
        ApproverId,

        [EnumMember(Value = "files")]
        Files,

        [EnumMember(Value = "data_source")]
        DataSource,

        [EnumMember(Value = "order_code_1")]
        OrderCode1,

        [EnumMember(Value = "order_code_2")]
        OrderCode2,

        [EnumMember(Value = "gtin")]
        Gtin,

        [EnumMember(Value = "ean")]
        Ean,

        [EnumMember(Value = "tariff_number")]
        TariffNumber,

        [EnumMember(Value = "manufacturer_id")]
        ManufacturerId,

        [EnumMember(Value = "kind")]
        Kind,

        /// <summary>The EPLAN Data Portal part a bought item was taken from (bought items
        /// only). Add-in / Web owned: the add-in writes it when the engineer takes a part from
        /// the Data Portal dialog, the web links it from its own lookups, and PDM ingest only
        /// relinks the catalogue item when the file's value changes — docs/FIELD_OWNERSHIP.md
        /// section 4.</summary>
        [EnumMember(Value = "edp_part_id")]
        EdpPartId,

        /// <summary>Label next to article_group_ids (made and bought) — docs/FIELD_OWNERSHIP.md
        /// section 1 row 3. A normal mappable, add-in-owned field now, not a separately
        /// computed derived-label row: its own custom property is whatever the admin's field
        /// mapping resolves it to, the same as any other field.</summary>
        [EnumMember(Value = "article_group_names")]
        ArticleGroupNames,

        /// <summary>Label next to manufacturer_id (bought items only) — see
        /// ArticleGroupNames's doc comment.</summary>
        [EnumMember(Value = "manufacturer_name")]
        ManufacturerName,

        /// <summary>Label next to processes (made items only) — see ArticleGroupNames's doc
        /// comment.</summary>
        [EnumMember(Value = "process_names")]
        ProcessNames,

        /// <summary>Label next to source_item_id (made items only), composed by the add-in at
        /// save time (GladosSwAddin.Helpers.SourceItemLabelComposer) — see ArticleGroupNames's
        /// doc comment.</summary>
        [EnumMember(Value = "source_item_name")]
        SourceItemName,

        /// <summary>Not a label of anything: holds
        /// AdminPdmSettings.made_feature_tree_template/bought_feature_tree_template verbatim,
        /// written by SwCustomPropertyService.WriteFeatureTreeRepresentation at save time, with
        /// SolidWorks resolving its $PRP:"name" links — docs/FIELD_OWNERSHIP.md section 1
        /// row 3.</summary>
        [EnumMember(Value = "feature_tree_representation")]
        FeatureTreeRepresentation,

        /// <summary>The customer a customer item was received from (made items only). Add-in
        /// owned: the Made tab writes it, PDM ingest updates the catalogue item in place —
        /// docs/FIELD_OWNERSHIP.md section 3.2. A made file with a customer is a customer item;
        /// there is no separate kind.</summary>
        [EnumMember(Value = "customer_id")]
        CustomerId,

        /// <summary>The customer's own part number for a customer item (made items only).
        /// Not a key: two customers may use the same number.</summary>
        [EnumMember(Value = "customer_part_number")]
        CustomerPartNumber,

        /// <summary>The customer's own revision for a customer item (made items only),
        /// independent of PDM.Revision.</summary>
        [EnumMember(Value = "customer_revision")]
        CustomerRevision,

        /// <summary>Label next to customer_id (made items only), the customer's name from the
        /// lookup snapshot at save time — see ArticleGroupNames's doc comment.</summary>
        [EnumMember(Value = "customer_name")]
        CustomerName,
    }

    /// <summary>Resolves an enum member's [EnumMember(Value=...)] wire form, or its bare name
    /// if undecorated. Mirrors EnumWireValue in Api/QueryStringBuilder.cs, kept as its own
    /// copy since that one is internal to GladosSwAddin.Api and this needs to be usable from
    /// Services and Forms too.</summary>
    internal static class EnumWireName
    {
        public static string Resolve(Enum value)
        {
            var member = value.GetType().GetField(value.ToString());
            var attribute = member?.GetCustomAttribute<EnumMemberAttribute>();
            return attribute?.Value ?? value.ToString();
        }
    }

    internal static class ItemFieldExtensions
    {
        /// <summary>The raw backend field name for a field, e.g. ItemField.OrderCode1 ->
        /// "order_code_1" — what FieldMappingEntry.Field is actually keyed by.</summary>
        public static string ToWireName(this ItemField field) => EnumWireName.Resolve(field);
    }

    /// <summary>The literal "made"/"bought" text ItemKind is written and read as on a
    /// document — Glados.Kind, add-in-written, write-once — see README's "Kind decides which
    /// catalogue". Deliberately not [EnumMember]-driven like ItemField's wire names: ItemKind
    /// predates the field-mapping sync and its values are compared against comboBoxSource's own
    /// combo items (GladosTaskPaneControl.IsBoughtSource/IsMadeSource) rather than resolved
    /// through EnumWireName.</summary>
    internal static class ItemKindExtensions
    {
        public static string ToWireValue(this ItemKind kind) => kind == ItemKind.Made ? "made" : "bought";

        /// <summary>Parses one of the two wire values back into an ItemKind. False (with kind
        /// left at its default) for anything else, including null/empty — an absent or
        /// unrecognized property value means "not set", not a third kind.</summary>
        public static bool TryParseWireValue(string value, out ItemKind kind)
        {
            if (string.Equals(value, ItemKind.Made.ToWireValue(), StringComparison.Ordinal))
            {
                kind = ItemKind.Made;
                return true;
            }

            if (string.Equals(value, ItemKind.Bought.ToWireValue(), StringComparison.Ordinal))
            {
                kind = ItemKind.Bought;
                return true;
            }

            kind = default(ItemKind);
            return false;
        }
    }

    internal static class FieldOwnerExtensions
    {
        /// <summary>The display form matching docs/FIELD_OWNERSHIP.md's tables exactly (e.g.
        /// "Add-in / Web") for the settings grid's Owner column. Deliberately separate from
        /// the wire form: the backend serves its canonical owner strings ("addin",
        /// "addin_web", ...) — the [EnumMember] values above — which are not fit for
        /// display.</summary>
        public static string ToDisplayString(this FieldOwner owner)
        {
            switch (owner)
            {
                case FieldOwner.Pdm: return "PDM";
                case FieldOwner.AddIn: return "Add-in";
                case FieldOwner.AddInWeb: return "Add-in / Web";
                case FieldOwner.Web: return "Web";
                case FieldOwner.Server: return "Server";
                default: return "Unknown";
            }
        }
    }

    /// <summary>The two feature-tree templates from GET .../settings/field-mapping
    /// (docs/FIELD_OWNERSHIP.md section 1 row 3): the text written verbatim into the
    /// feature-tree property at save time, one per item kind, its $PRP:"name" links resolved
    /// by SolidWorks. Absent on the wire (an older backend) means
    /// "use the compiled-in defaults" — see FieldMapping.DefaultTemplates.</summary>
    public class FeatureTreeTemplates
    {
        public string Made { get; set; }

        public string Bought { get; set; }
    }

    /// <summary>One row of the field mapping: which SolidWorks custom property (if any)
    /// backs a given backend field, and who owns writing it. Field is the raw backend field
    /// name rather than ItemField so an entry for a field this add-in build doesn't recognize
    /// yet still deserializes, persists and displays instead of being dropped — see
    /// docs/FIELD_OWNERSHIP.md section 2, "Tolerant parsing". Doubles as the wire shape for
    /// GET .../settings/field-mapping (see Api/Models/FieldMappingResponse.cs) and as the
    /// shape persisted by FieldMappingService, since both match this one-for-one.</summary>
    public class FieldMappingEntry
    {
        public string Field { get; set; }

        public FieldOwner Owner { get; set; }

        [JsonProperty("custom_property")]
        public string PropertyName { get; set; }

        public bool? UpdateInPlace { get; set; }

        /// <summary>Whether ingest cannot create/revise/claim a row without this field — true
        /// for the locked identity fields (part_number, revision, article_name, plus
        /// order_code_1/manufacturer_id on bought items) and for whatever else the admin has
        /// ticked in *Settings → External Services → SolidWorks PDM*. A plain, non-nullable
        /// bool: the backend always sends it, and an older backend that omits the key entirely
        /// deserializes it as false, the same "not required" reading GladosTaskPaneControl's
        /// required-field markers need. See docs/FIELD_OWNERSHIP.md's "required fields"
        /// paragraph.</summary>
        public bool Required { get; set; }

        /// <summary>The field this one is a label of (docs/FIELD_OWNERSHIP.md section 1 row
        /// 3) — e.g. "manufacturer_id" for the "manufacturer_name" row — null for every other
        /// field, including "feature_tree_representation", which is not a label of
        /// anything.</summary>
        [JsonProperty("label_of")]
        public string LabelOf { get; set; }

        public FieldMappingEntry()
        {
        }

        public FieldMappingEntry(
            string field,
            FieldOwner owner,
            string propertyName = null,
            bool? updateInPlace = null,
            bool required = false,
            string labelOf = null)
        {
            Field = field;
            Owner = owner;
            PropertyName = propertyName;
            UpdateInPlace = updateInPlace;
            Required = required;
            LabelOf = labelOf;
        }
    }

    /// <summary>Wire name of the one pseudo-rule required field this add-in build knows how to
    /// evaluate — see RequiredFieldRuleEvaluator and RequiredFieldRule. Kept as its own constant
    /// class so the string literal exists exactly once.</summary>
    public static class RequiredFieldRuleName
    {
        public const string FabricationStep = "fabrication_step";
    }

    /// <summary>One row of the `rules` map in GET .../settings/field-mapping
    /// (docs/FIELD_OWNERSHIP.md's "required fields" paragraph): a pseudo-rule required field
    /// that is satisfied by some condition on the parsed data rather than one mapped property
    /// being non-empty — today only RequiredFieldRuleName.FabricationStep, made items only. A
    /// rule name this add-in build has no evaluator for is carried through like any other
    /// tolerant-parsing entry and simply never asked about.</summary>
    public class RequiredFieldRule
    {
        public string Name { get; set; }

        public bool Required { get; set; }

        public RequiredFieldRule()
        {
        }

        public RequiredFieldRule(string name, bool required)
        {
            Name = name;
            Required = required;
        }
    }

    /// <summary>Compiled-in defaults — byte-identical to docs/FIELD_OWNERSHIP.md section 3 —
    /// used until a server mapping has been synced, or when the stored one belongs to a
    /// server other than the one currently configured. GladosSwAddin.Services.FieldMappingService
    /// is the only intended consumer; everything else resolves a property name by calling it
    /// rather than reading these tables directly.</summary>
    public static class FieldMapping
    {
        private static readonly IReadOnlyList<FieldMappingEntry> MadeItemDefaults = new List<FieldMappingEntry>
        {
            new FieldMappingEntry("part_number", FieldOwner.Pdm, "PDM.PartNumber", required: true),
            new FieldMappingEntry("revision", FieldOwner.Pdm, "PDM.Revision", required: true),
            new FieldMappingEntry("revision_date", FieldOwner.Pdm, "PDM.RevisionDate", true),
            new FieldMappingEntry("revision_note", FieldOwner.Pdm, "PDM.RevisionNote", true),
            new FieldMappingEntry("article_name", FieldOwner.AddIn, "Glados.ArticleName", false, required: true),
            new FieldMappingEntry("material", FieldOwner.AddIn, "Glados.Material", false),
            new FieldMappingEntry("mass", FieldOwner.AddIn, "Glados.Mass", true),
            new FieldMappingEntry("base_size", FieldOwner.AddIn, "Glados.BaseSize", false),
            new FieldMappingEntry("note", FieldOwner.AddIn, "Glados.Note", true),
            new FieldMappingEntry("weblink", FieldOwner.AddIn, "Glados.Weblink", true),
            new FieldMappingEntry("article_group_ids", FieldOwner.AddIn, "Glados.ArticleGroupIds", true),
            new FieldMappingEntry("bought_item_id", FieldOwner.AddIn, "Glados.BoughtItemId", false),
            new FieldMappingEntry("source_item_id", FieldOwner.AddInWeb, "Glados.SourceItemId", false),
            new FieldMappingEntry("processes", FieldOwner.AddIn, "Glados.Processes", true),
            new FieldMappingEntry("status", FieldOwner.Pdm),
            new FieldMappingEntry("successor_item_id", FieldOwner.Web),
            new FieldMappingEntry("approver_id", FieldOwner.Web),
            new FieldMappingEntry("files", FieldOwner.Web),
            new FieldMappingEntry("data_source", FieldOwner.Server),
            new FieldMappingEntry("kind", FieldOwner.AddIn, "Glados.Kind", false),
            new FieldMappingEntry(
                "article_group_names", FieldOwner.AddIn, "Glados.ArticleGroupNames", labelOf: "article_group_ids"),
            new FieldMappingEntry(
                "process_names", FieldOwner.AddIn, "Glados.ProcessNames", labelOf: "processes"),
            new FieldMappingEntry(
                "source_item_name", FieldOwner.AddIn, "Glados.SourceItemName", labelOf: "source_item_id"),
            new FieldMappingEntry(
                "feature_tree_representation", FieldOwner.AddIn, "Glados.FeatureTreeRepresentation"),
            new FieldMappingEntry("customer_id", FieldOwner.AddIn, "Glados.CustomerId", true),
            new FieldMappingEntry("customer_part_number", FieldOwner.AddIn, "Glados.CustomerPartNumber", true),
            new FieldMappingEntry("customer_revision", FieldOwner.AddIn, "Glados.CustomerRevision", true),
            new FieldMappingEntry(
                "customer_name", FieldOwner.AddIn, "Glados.CustomerName", labelOf: "customer_id"),
        };

        private static readonly IReadOnlyList<FieldMappingEntry> BoughtItemDefaults = new List<FieldMappingEntry>
        {
            new FieldMappingEntry("part_number", FieldOwner.Pdm, "PDM.PartNumber", required: true),
            new FieldMappingEntry("revision", FieldOwner.Pdm, "PDM.Revision", required: true),
            new FieldMappingEntry("article_name", FieldOwner.AddIn, "Glados.ArticleName", required: true),
            new FieldMappingEntry("order_code_1", FieldOwner.AddIn, "Glados.OrderCode1", required: true),
            new FieldMappingEntry("order_code_2", FieldOwner.AddIn, "Glados.OrderCode2"),
            new FieldMappingEntry("gtin", FieldOwner.AddIn, "Glados.Gtin"),
            new FieldMappingEntry("ean", FieldOwner.AddIn, "Glados.Ean"),
            new FieldMappingEntry("tariff_number", FieldOwner.AddIn, "Glados.TariffNumber"),
            new FieldMappingEntry("mass", FieldOwner.AddIn, "Glados.Mass"),
            new FieldMappingEntry("note", FieldOwner.AddIn, "Glados.Note"),
            new FieldMappingEntry("weblink", FieldOwner.AddIn, "Glados.Weblink"),
            new FieldMappingEntry("manufacturer_id", FieldOwner.AddIn, "Glados.ManufacturerId", required: true),
            new FieldMappingEntry("article_group_ids", FieldOwner.AddIn, "Glados.ArticleGroupIds"),
            new FieldMappingEntry("edp_part_id", FieldOwner.AddInWeb, "Glados.EdpPartId"),
            new FieldMappingEntry("status", FieldOwner.Pdm),
            new FieldMappingEntry("successor_item_id", FieldOwner.Web),
            new FieldMappingEntry("approver_id", FieldOwner.Web),
            new FieldMappingEntry("files", FieldOwner.Web),
            new FieldMappingEntry("data_source", FieldOwner.Server),
            new FieldMappingEntry("kind", FieldOwner.AddIn, "Glados.Kind"),
            new FieldMappingEntry(
                "manufacturer_name", FieldOwner.AddIn, "Glados.ManufacturerName", labelOf: "manufacturer_id"),
            new FieldMappingEntry(
                "article_group_names", FieldOwner.AddIn, "Glados.ArticleGroupNames", labelOf: "article_group_ids"),
            new FieldMappingEntry(
                "feature_tree_representation", FieldOwner.AddIn, "Glados.FeatureTreeRepresentation"),
        };

        /// <summary>Compiled-in feature-tree template defaults — byte-identical to the
        /// backend's own AdminPdmSettings.made_feature_tree_template/
        /// bought_feature_tree_template defaults — used the same way Defaults/DefaultRules are,
        /// until a server mapping has synced.</summary>
        public static FeatureTreeTemplates DefaultTemplates { get; } = new FeatureTreeTemplates
        {
            Made = "$PRP:\"PDM.PartNumber\" Rev$PRP:\"PDM.Revision\" ($PRP:\"Glados.ArticleName\")",
            Bought = "$PRP:\"Glados.ArticleName\" - $PRP:\"Glados.OrderCode1\" - $PRP:\"Glados.ManufacturerName\"",
        };

        public static IReadOnlyDictionary<ItemKind, IReadOnlyList<FieldMappingEntry>> Defaults { get; } =
            new Dictionary<ItemKind, IReadOnlyList<FieldMappingEntry>>
            {
                { ItemKind.Made, MadeItemDefaults },
                { ItemKind.Bought, BoughtItemDefaults },
            };

        /// <summary>Compiled-in fallback for the `rules` map, used the same way Defaults is —
        /// until a server mapping has been synced. Deliberately empty for both kinds rather
        /// than mirroring the backend's own fresh-install default (fabrication_step turned on
        /// for made items): whether a pseudo-rule is actually enforced is admin-configurable
        /// state this add-in has no way to know before the first successful sync, unlike the
        /// locked identity fields above, which are true unconditionally. Showing no routing
        /// marker before the first sync is the safe default; showing one that doesn't match
        /// the real setting would not be.</summary>
        public static IReadOnlyDictionary<ItemKind, IReadOnlyList<RequiredFieldRule>> DefaultRules { get; } =
            new Dictionary<ItemKind, IReadOnlyList<RequiredFieldRule>>
            {
                { ItemKind.Made, new List<RequiredFieldRule>() },
                { ItemKind.Bought, new List<RequiredFieldRule>() },
            };
    }
}

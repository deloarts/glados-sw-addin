using System;
using System.Collections.Generic;

namespace GladosSwAddin.Models
{
    /// <summary>
    /// Local (SolidWorks-side) view of a bought item's syncable fields. Field names mirror
    /// GladosSwAddin.Api.Models.BoughtItemResponse; which SolidWorks custom property backs
    /// each one is no longer fixed here — see GladosSwAddin.Services.FieldMappingService and
    /// FieldMapping.Defaults (docs/FIELD_OWNERSHIP.md section 2).
    /// </summary>
    public class BoughtItemProperties
    {
        public string PartNumber { get; set; }

        /// <summary>PDM revision. Not sent to Glados — an empty revision just means the part
        /// hasn't been published in PDM yet.</summary>
        public string Revision { get; set; }

        public string ArticleName { get; set; }

        public string OrderCode1 { get; set; }

        public string OrderCode2 { get; set; }

        public string Gtin { get; set; }

        public string Ean { get; set; }

        public string TariffNumber { get; set; }

        public int? Mass { get; set; }

        public string Note { get; set; }

        public string Weblink { get; set; }

        public int? ManufacturerId { get; set; }

        public List<int> ArticleGroupIds { get; set; } = new List<int>();

        /// <summary>The EPLAN Data Portal part this item was taken from, null when none. A
        /// link, not catalogue data: never part of the divergence comparison, since the web may
        /// legitimately link the catalogue item to another part (docs/FIELD_OWNERSHIP.md
        /// section 4, Add-in / Web).</summary>
        public int? EdpPartId { get; set; }

        /// <summary>Resolved clear text for ManufacturerId, so PDM data cards and BOMs can show
        /// and filter on a name instead of an integer. Denormalized and therefore possibly stale:
        /// display fallback only — never compared against Glados and never used to match an item.
        /// See "Rules for derived properties" in docs/DESIGN.md.</summary>
        public string ManufacturerName { get; set; }

        /// <summary>Resolved clear text for ArticleGroupIds, positionally aligned with it. Same
        /// display-only caveat as ManufacturerName.</summary>
        public List<string> ArticleGroupNames { get; set; } = new List<string>();

        public bool HasPartNumber => !string.IsNullOrWhiteSpace(PartNumber);

        /// <summary>
        /// What's left of the once-per-field property-name constants after the field-mapping
        /// sync: structural rules that apply regardless of which property a field is mapped
        /// to, rather than any one field's name. Per-field names now live only in
        /// GladosSwAddin.Models.FieldMapping.Defaults, resolved through FieldMappingService —
        /// see docs/FIELD_OWNERSHIP.md section 2.
        /// </summary>
        public static class PropertyNames
        {
            private const string PdmPrefix = "PDM.";

            /// <summary>Where mass lived before the 2026-08 migration and lives again as the
            /// mapped default. Kept as a literal fallback — rather than relying only on the
            /// mapped name happening to resolve to this same string — for documents written
            /// under a server mapping that points mass somewhere else. Never written.</summary>
            public const string MassFallbackLegacy = "Glados.Mass";

            /// <summary>PDM.* properties come from the vault or from SolidWorks itself and must
            /// never be written by this addin, regardless of which field a server mapping
            /// happens to point at one — see WriteText's GuardNotPdmOwned.</summary>
            public static bool IsPdmOwned(string propertyName)
            {
                return propertyName != null
                    && propertyName.StartsWith(PdmPrefix, StringComparison.Ordinal);
            }
        }
    }
}

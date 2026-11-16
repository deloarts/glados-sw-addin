using System;
using System.Collections.Generic;

namespace GladosSwAddin.Models
{
    /// <summary>
    /// Local (SolidWorks-side) view of a made item's syncable fields. Field names mirror
    /// GladosSwAddin.Api.Models.MadeItemResponse; which SolidWorks custom property backs
    /// each one is resolved through GladosSwAddin.Services.FieldMappingService and
    /// FieldMapping.Defaults, not fixed here — see docs/FIELD_OWNERSHIP.md section 2. Routing
    /// (Processes) is the one field this addin authors end-to-end: FIELD_OWNERSHIP.md §3.2
    /// makes Glados.Processes add-in-owned, reaching the catalogue only via PDM ingest.
    /// </summary>
    public class MadeItemProperties
    {
        public string PartNumber { get; set; }

        /// <summary>PDM revision. A differing revision from the matched catalogue item usually
        /// means ingest is behind, not that the parts actually differ — see
        /// MadeItemSyncService.GetDivergentFields.</summary>
        public string Revision { get; set; }

        public string ArticleName { get; set; }

        public string Material { get; set; }

        public int? Mass { get; set; }

        public string BaseSize { get; set; }

        public string Note { get; set; }

        public string Weblink { get; set; }

        public List<int> ArticleGroupIds { get; set; } = new List<int>();

        /// <summary>Resolved clear text for ArticleGroupIds, positionally aligned with it.
        /// Denormalized and therefore possibly stale: display fallback only — never compared
        /// against Glados and never used to match an item. See "Rules for derived properties"
        /// in README.md.</summary>
        public List<string> ArticleGroupNames { get; set; } = new List<string>();

        /// <summary>The file's link to a raw-material spec in the source-item catalogue, picked
        /// or created from GladosSwAddin.Forms.SourceItemPickerForm — dual-owned with the web
        /// (docs/FIELD_OWNERSHIP.md row 8): divergence compares this against
        /// GladosSwAddin.Api.Models.MadeItemResponse.PdmSourceItemId (the PDM shadow), never
        /// against SourceItemId itself. See MadeItemSyncService.GetDivergentFields.</summary>
        public int? SourceItemId { get; set; }

        /// <summary>Denormalized spec label for SourceItemId — "&lt;type_name&gt; &lt;dimension&gt;
        /// &lt;material&gt;" composed by GladosSwAddin.Helpers.SourceItemLabelComposer from the
        /// lookup snapshot, re-resolved on every Save (see
        /// GladosTaskPaneControl.ResolveSourceItemLabel) and never blanked when unresolvable —
        /// display fallback only, same rules as ArticleGroupNames above.</summary>
        public string SourceItemName { get; set; }

        /// <summary>The routing: fabrication/inspection steps in position order (array order),
        /// grouped by GroupIndex — see ProcessStep and the Glados.Processes encoding in
        /// .playground/made-item-process-grouping-plan.md §3.2. Empty (not null) whenever
        /// there's nothing to route yet, or when the stored property couldn't be parsed — see
        /// ProcessesUnreadable.</summary>
        public List<ProcessStep> Processes { get; set; } = new List<ProcessStep>();

        /// <summary>True when Glados.Processes held text that wasn't valid JSON for the
        /// encoding above. Processes is then an empty list rather than a guess — a tolerant
        /// read per the plan's "read tolerantly" rule: never crash, never silently overwrite
        /// what's on disk until the user explicitly saves.</summary>
        public bool ProcessesUnreadable { get; set; }

        /// <summary>True when the mapped Glados.Processes property held any non-blank text —
        /// including "[]" or text that couldn't be parsed — i.e. a routing has been stored
        /// before; false when the property is absent, blank or unmapped. Like
        /// ProcessesUnreadable it describes the read, not the value: an explicitly saved empty
        /// routing is stored, a file that never had one is not.</summary>
        public bool ProcessesStored { get; set; }

        /// <summary>Denormalized routing label read back from the resolved process-names label
        /// property (Glados.ProcessNames by default — see
        /// GladosSwAddin.Services.FieldMappingService.ResolvePropertyName), e.g. "Sägen →
        /// Drehen + Fräsen → QS-Prüfung" — display fallback only (see ArticleGroupNames
        /// above), regenerated from Processes on every write by
        /// SwCustomPropertyService.WriteProcesses. Never compared, never used to match.</summary>
        public string ProcessNames { get; set; }

        /// <summary>The customer this part was received from; set only on a customer item,
        /// which is a made item with a customer rather than a kind of its own. Edited on the
        /// Made tab. See docs/FIELD_OWNERSHIP.md section 3.2.</summary>
        public int? CustomerId { get; set; }

        /// <summary>Denormalized customer name for CustomerId, taken from the lookup snapshot
        /// on every Save and never blanked when unresolvable — display fallback only, same
        /// rules as ArticleGroupNames above.</summary>
        public string CustomerName { get; set; }

        /// <summary>The customer's own part number. Required whenever CustomerId is set; the
        /// backend refuses the pair incomplete from the web, but PDM ingest stores whatever
        /// the file holds.</summary>
        public string CustomerPartNumber { get; set; }

        /// <summary>The customer's own revision, independent of Revision.</summary>
        public string CustomerRevision { get; set; }

        public bool HasPartNumber => !string.IsNullOrWhiteSpace(PartNumber);

        /// <summary>
        /// Structural rules that apply regardless of which property a field is mapped to,
        /// rather than any one field's name — mirrors GladosSwAddin.Models.BoughtItemProperties.
        /// PropertyNames. Per-field names live in GladosSwAddin.Models.FieldMapping.Defaults,
        /// resolved through FieldMappingService.
        /// </summary>
        public static class PropertyNames
        {
            private const string PdmPrefix = "PDM.";

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

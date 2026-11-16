using System.Collections.Generic;
using GladosSwAddin.Models;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors GET /pat/settings/field-mapping's response — see
    /// docs/FIELD_OWNERSHIP.md section 1 row 10 and section 2. Reuses
    /// GladosSwAddin.Models.FieldMappingEntry directly for the rows rather than a separate
    /// wire DTO: its shape already matches the JSON one-for-one.</summary>
    public class FieldMappingResponse
    {
        public List<FieldMappingEntry> MadeItems { get; set; }

        public List<FieldMappingEntry> BoughtItems { get; set; }

        /// <summary>The two feature-tree templates (docs/FIELD_OWNERSHIP.md section 1 row 3).
        /// Null on an older backend that doesn't send them yet — treated the same as "use the
        /// compiled-in defaults" by FieldMappingService.MadeFeatureTreeTemplate/
        /// BoughtFeatureTreeTemplate.</summary>
        public FeatureTreeTemplates Templates { get; set; }

        /// <summary>Pseudo-rule required fields per kind ("made"/"bought", matching
        /// ItemKind.ToWireValue()) — docs/FIELD_OWNERSHIP.md's "required fields" paragraph.
        /// Null on an older backend that doesn't send it yet; FieldMappingService.Rules treats
        /// that the same as an empty dictionary.</summary>
        public Dictionary<string, List<RequiredFieldRule>> Rules { get; set; }
    }
}

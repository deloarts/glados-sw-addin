using System.Collections.Generic;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors the response of POST /api/v1/pat/pdm/validate — a read-only dry run
    /// reporting whether the active document's mapped properties would be accepted by PDM
    /// ingest, without touching PDM or the catalogue. Advisory only: nothing in the add-in
    /// depends on this succeeding, and a failed or unreachable check never blocks Save. See
    /// README.md's "Validation before release".</summary>
    public class PdmValidationResponse
    {
        public bool Ok { get; set; }

        /// <summary>Why Ok is false, verbatim from the backend (English or German, as given) —
        /// shown to the user unchanged rather than re-worded or translated again here.</summary>
        public List<string> Reasons { get; set; }

        /// <summary>PDM-owned required fields still missing (field-mapping wire names, e.g.
        /// "part_number"/"revision") — expected to be empty before release since the PDM
        /// workflow assigns them, not the engineer. Unlike Reasons, never affects Ok: a field
        /// listed here is never also listed in Reasons. Empty (never null) on a backend that
        /// doesn't send it yet.</summary>
        public List<string> PendingPdm { get; set; } = new List<string>();

        public string Kind { get; set; }

        public string PartNumber { get; set; }

        public string Revision { get; set; }

        /// <summary>What ingesting this document would do to the catalogue if it arrived from
        /// PDM right now.</summary>
        public PdmValidationOutcome Would { get; set; }

        /// <summary>Whether the request's file_path resolved to a PDM vault identity. Null on a
        /// backend older than this field — PdmValidationApiService.ValidateAsync still sends
        /// FilePath to such a backend, which simply ignores the unknown property; the add-in
        /// treats a null the same as NotApplicable, showing no identity hint either way.</summary>
        public PdmFileIdentity? FileIdentity { get; set; }

        /// <summary>Extra informational lines alongside Reasons — unlike Reasons, present
        /// whether Ok is true or false, and never itself a reason Ok is false. Null on a backend
        /// that doesn't send the field yet; the add-in treats that the same as an empty list.</summary>
        public List<string> Notes { get; set; }
    }
}

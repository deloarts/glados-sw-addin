using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors InspectionMethodResponse in src/modules/catalogue/adapters/inbound/api/schemas/inspection_method_schema.py.</summary>
    public class InspectionMethodResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset? Changed { get; set; }

        public string Name { get; set; }

        public string LabelColor { get; set; }

        public string LabelFontColor { get; set; }

        public string Description { get; set; }

        public int CreatorId { get; set; }

        public PublicUserResponse Creator { get; set; }

        public int? DefaultSetupMinutes { get; set; }

        public double? DefaultMinutesPerPiece { get; set; }

        public bool RequiresEvidence { get; set; }

        public bool Archived { get; set; }

        /// <summary>Place in the hand-kept order Glados' pickers offer these in (see
        /// LookupOrder); 0 from a server that predates it.</summary>
        public int SortOrder { get; set; }
    }
}

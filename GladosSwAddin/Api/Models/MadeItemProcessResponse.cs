using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors MadeItemProcessResponse in
    /// src/modules/catalogue/adapters/inbound/api/schemas/made_item_schema.py. One routing step of a
    /// MadeItemResponse's Processes list — read-only, ids plus resolved display names. Never
    /// compare FabricationMethodName/InspectionMethodName against local data (see
    /// MadeItemSyncService) — only the ids are the source of truth.</summary>
    public class MadeItemProcessResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public DateTime Created { get; set; }

        public string Note { get; set; }

        public int? EstimatedSetupMinutes { get; set; }

        public double? EstimatedMinutesPerPiece { get; set; }

        public int CreatorId { get; set; }

        public StepRole Role { get; set; }

        public int? InspectionMethodId { get; set; }

        public int? FabricationMethodId { get; set; }

        public string FabricationMethodName { get; set; }

        public string InspectionMethodName { get; set; }

        public int MadeItemId { get; set; }

        public int Position { get; set; }

        public int GroupIndex { get; set; }
    }
}

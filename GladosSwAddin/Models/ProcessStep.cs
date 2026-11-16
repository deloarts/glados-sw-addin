using GladosSwAddin.Api.Models;
using Newtonsoft.Json;

namespace GladosSwAddin.Models
{
    /// <summary>
    /// One routing step of a made item's Glados.Processes property — see
    /// GladosSwAddin.Models.MadeItemProperties and the encoding decided in
    /// .playground/made-item-process-grouping-plan.md §3.2: a single-line JSON array, ids
    /// only, position = array order. A FABRICATION-role step carries FabricationMethodId and
    /// leaves InspectionMethodId null; an INSPECTION-role step is the mirror image — never
    /// both, never neither (SwCustomPropertyService.ValidateProcessGrouping enforces this
    /// before every write).
    /// </summary>
    public class ProcessStep
    {
        public StepRole Role { get; set; }

        public int? FabricationMethodId { get; set; }

        public int? InspectionMethodId { get; set; }

        /// <summary>0-based, contiguous within the owning made item, assigned array position by
        /// array position — a new group starts wherever this differs from the previous step's.
        /// Always renormalized by SwCustomPropertyService.NormalizeProcessGroups before a
        /// write, so a caller that only reorders/regroups steps never has to compute this
        /// itself.</summary>
        public int GroupIndex { get; set; }

        public int? EstimatedSetupMinutes { get; set; }

        public double? EstimatedMinutesPerPiece { get; set; }

        public string Note { get; set; }

        /// <summary>Runtime-only display label — resolved from the lookup cache (or the remote
        /// response on Load-from-Glados), never from the property itself (decided 2026-09-02:
        /// the encoding stores ids only, no per-step label). [JsonIgnore] keeps it out of
        /// Glados.Processes; it must never be compared either — see
        /// MadeItemSyncService.GetDivergentFields. Falls back to "#&lt;id&gt;" when
        /// unresolvable (offline with an empty cache).</summary>
        [JsonIgnore]
        public string MethodName { get; set; }

        /// <summary>The id that matters for this step's Role — FabricationMethodId for
        /// FABRICATION, InspectionMethodId for INSPECTION. Null only when the step is
        /// malformed (fails ValidateProcessGrouping).</summary>
        [JsonIgnore]
        public int? MethodId => Role == StepRole.Fabrication ? FabricationMethodId : InspectionMethodId;

        /// <summary>A field-by-field copy, MethodName included, so a working list can be edited
        /// without touching the baseline steps it was taken from.</summary>
        public ProcessStep Clone()
        {
            return new ProcessStep
            {
                Role = Role,
                FabricationMethodId = FabricationMethodId,
                InspectionMethodId = InspectionMethodId,
                GroupIndex = GroupIndex,
                EstimatedSetupMinutes = EstimatedSetupMinutes,
                EstimatedMinutesPerPiece = EstimatedMinutesPerPiece,
                Note = Note,
                MethodName = MethodName,
            };
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Evaluates the pseudo-rule required fields the backend serves under GET
    /// .../settings/field-mapping's `rules` map (docs/FIELD_OWNERSHIP.md's "required fields"
    /// paragraph). Kept as a small static, UI-free helper — no ModelDoc2, no control state — so
    /// "is this rule satisfied" can be answered (and, if a test project is ever added, tested)
    /// without a loaded document or a running task pane. GladosTaskPaneControl's required-field
    /// markers are the only caller today. There is only one evaluator, matching the backend's
    /// own rule catalogue (src.domain.services.pdm_required_fields.REQUIRED_FIELD_RULES): a
    /// rule name this build doesn't recognize is never asked about here, per the tolerant-
    /// parsing convention the rest of the field mapping follows.
    /// </summary>
    public static class RequiredFieldRuleEvaluator
    {
        /// <summary>True once <paramref name="steps"/> has at least one FABRICATION-role step
        /// with a fabrication method assigned — mirrors the backend's
        /// application.services.pdm_required_fields._has_fabrication_step exactly (role
        /// fabrication and a non-null fabrication_method_id). This is what
        /// RequiredFieldRuleName.FabricationStep evaluates.</summary>
        public static bool HasFabricationStep(IEnumerable<ProcessStep> steps)
        {
            return (steps ?? Enumerable.Empty<ProcessStep>())
                .Any(step => step != null
                    && step.Role == StepRole.Fabrication
                    && step.FabricationMethodId.HasValue);
        }
    }
}

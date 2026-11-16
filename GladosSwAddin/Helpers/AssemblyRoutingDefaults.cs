using System.Collections.Generic;
using System.Linq;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;

namespace GladosSwAddin.Helpers
{
    /// <summary>
    /// The routing a made assembly starts from when its file has none: one fabrication step
    /// with the method the backend flags as default for assemblies (default_for_assemblies,
    /// delivered in the lookup snapshot since the add-in can only call the PAT API). Pure so it
    /// can be unit-tested; ShouldSeed decides WHEN to apply it: only to a file that has never
    /// stored a routing, so a routing the user saved empty ("[]") is respected. The task pane
    /// never writes the seeded step — it reaches the file only through the Assembly tab's
    /// Save.
    /// </summary>
    public static class AssemblyRoutingDefaults
    {
        /// <summary>True only for a readable file that has never stored a routing: not null,
        /// nothing stored under the mapped Glados.Processes property (not even "[]"), not
        /// unreadable, and no steps.</summary>
        public static bool ShouldSeed(MadeItemProperties local)
        {
            return local != null
                && !local.ProcessesStored
                && !local.ProcessesUnreadable
                && (local.Processes == null || local.Processes.Count == 0);
        }

        /// <summary>The first method flagged DefaultForAssemblies, or null when there is none
        /// (or <paramref name="methods"/> is null). Archived methods are excluded defensively:
        /// the backend clears the flag on archive, but a cached snapshot could predate
        /// that.</summary>
        public static FabricationMethodResponse FindDefaultMethod(IEnumerable<FabricationMethodResponse> methods)
        {
            return methods?.FirstOrDefault(m => m != null && m.DefaultForAssemblies && !m.Archived);
        }

        /// <summary>A single fabrication step in group 0 for the default method, or null when
        /// FindDefaultMethod finds none.</summary>
        public static ProcessStep CreateDefaultStep(IEnumerable<FabricationMethodResponse> methods)
        {
            var method = FindDefaultMethod(methods);
            if (method == null)
                return null;

            return new ProcessStep
            {
                Role = StepRole.Fabrication,
                FabricationMethodId = method.Id,
                GroupIndex = 0,
                MethodName = method.Name,
            };
        }
    }
}

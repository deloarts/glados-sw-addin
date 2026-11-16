using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Api.Services;
using GladosSwAddin.Models;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Read-only comparison of a local MadeItemProperties against the Glados made-item
    /// catalogue, matched by PDM.PartNumber (newest revision — see
    /// MadeItemsApiService.GetMadeItemByPartNumberAsync). Simpler than BoughtItemSyncService:
    /// made items have no adoption match key, so MadeItemMatchState has no PossibleDuplicate
    /// tier and there is nothing to fall back to on a PartNumber miss. Creating/updating
    /// catalogue rows is owned by PDM ingest and, for routing, by the add-in's own file edit
    /// (WriteProcesses) — this service only ever reads, to show the user whether their
    /// document is in sync.
    /// </summary>
    public class MadeItemSyncService
    {
        private readonly MadeItemsApiService _madeItems;

        public MadeItemSyncService(MadeItemsApiService madeItems)
        {
            _madeItems = madeItems;
        }

        public async Task<MadeItemLookupResult> LookupAsync(MadeItemProperties local)
        {
            if (!local.HasPartNumber)
                return new MadeItemLookupResult { State = MadeItemMatchState.NoPartNumber };

            MadeItemResponse remote;
            try
            {
                remote = await _madeItems.GetMadeItemByPartNumberAsync(local.PartNumber);
            }
            catch (GladosApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new MadeItemLookupResult { State = MadeItemMatchState.NotFound };
            }

            var divergentFields = GetDivergentFields(local, remote);

            return new MadeItemLookupResult
            {
                State = divergentFields.Count == 0
                    ? MadeItemMatchState.FoundInSync
                    : MadeItemMatchState.FoundDiverges,
                RemoteItem = remote,
                DivergentFields = divergentFields,
            };
        }

        /// <summary>Compares reference keys and catalogue text only — same philosophy as
        /// BoughtItemSyncService.GetDivergentFields. Revision is compared first: a mismatch
        /// there usually just means ingest hasn't caught up with this document's PDM revision
        /// yet, not that the two rows actually disagree. Processes compares the ordered
        /// sequence of (role, method id, group index, setup, per-piece, note) — remote
        /// *_method_name labels never participate, the same exclusion this method already
        /// applies to every other resolved-label field.</summary>
        private static List<string> GetDivergentFields(MadeItemProperties local, MadeItemResponse remote)
        {
            var divergent = new List<string>();

            void Compare(string field, object localValue, object remoteValue)
            {
                if (!Equals(localValue, remoteValue))
                    divergent.Add(field);
            }

            Compare(nameof(local.Revision), local.Revision ?? "", remote.Revision ?? "");
            Compare(nameof(local.ArticleName), local.ArticleName ?? "", remote.ArticleName ?? "");
            Compare(nameof(local.Material), local.Material ?? "", remote.Material ?? "");
            Compare(nameof(local.Mass), local.Mass, remote.Mass);
            Compare(nameof(local.BaseSize), local.BaseSize ?? "", remote.BaseSize ?? "");
            Compare(nameof(local.Note), local.Note ?? "", remote.Note ?? "");
            Compare(nameof(local.Weblink), local.Weblink ?? "", remote.Weblink ?? "");

            // source_item_id is dual-owned (docs/FIELD_OWNERSHIP.md row 8): the file and the web
            // can each hold their own value, and CAD only wins once the CAD value itself
            // changes. Comparing against remote.SourceItemId (the web-effective value) would
            // report a false divergence every time the web side edits it without touching the
            // file. Comparing against the PDM shadow instead means "has PDM actually caught up
            // with what this file already claims" — the same question every other divergent
            // field here answers. remote.PdmSourceItemId is null both when PDM hasn't set a
            // shadow yet and on an older backend that doesn't send the field at all; either way
            // that must read as "nothing to compare against" rather than a mismatch, so a
            // document with no source item picked (local.SourceItemId == null) never reports
            // diverged just because the backend is behind.
            Compare(nameof(local.SourceItemId), local.SourceItemId, remote.PdmSourceItemId);

            Compare(nameof(local.CustomerId), local.CustomerId, remote.CustomerId);
            Compare(
                nameof(local.CustomerPartNumber), local.CustomerPartNumber ?? "", remote.CustomerPartNumber ?? "");
            Compare(nameof(local.CustomerRevision), local.CustomerRevision ?? "", remote.CustomerRevision ?? "");

            var localGroups = new HashSet<int>(local.ArticleGroupIds ?? new List<int>());
            var remoteGroups = new HashSet<int>(
                (remote.ArticleGroups ?? new List<ArticleGroupResponse>()).Select(g => g.Id));
            if (!localGroups.SetEquals(remoteGroups))
                divergent.Add(nameof(local.ArticleGroupIds));

            if (!ProcessSequence(local.Processes).SequenceEqual(ProcessSequence(remote.Processes)))
                divergent.Add(nameof(local.Processes));

            return divergent;
        }

        /// <summary>True when two local routings would persist identically: both are group-
        /// normalized first (SwCustomPropertyService.NormalizeProcessGroups, what Save writes)
        /// and then compared by the same per-step signature as the divergence check — so
        /// MethodName, a display label, is ignored and null equals empty.</summary>
        internal static bool SameRouting(IEnumerable<ProcessStep> a, IEnumerable<ProcessStep> b)
        {
            return ProcessSequence(SwCustomPropertyService.NormalizeProcessGroups(a))
                .SequenceEqual(ProcessSequence(SwCustomPropertyService.NormalizeProcessGroups(b)));
        }

        /// <summary>Canonical (role, method id, group index, setup, per-piece, note) signature
        /// for the local side of a Processes comparison, in position order (local steps are
        /// already list-ordered — array order = position).</summary>
        private static List<string> ProcessSequence(IEnumerable<ProcessStep> steps)
        {
            return (steps ?? Enumerable.Empty<ProcessStep>())
                .Select(step => StepSignature(
                    step.Role, step.MethodId, step.GroupIndex,
                    step.EstimatedSetupMinutes, step.EstimatedMinutesPerPiece, step.Note))
                .ToList();
        }

        /// <summary>Same signature for the remote side. Explicitly sorted by Position first —
        /// the API doesn't document response ordering as a guarantee.</summary>
        private static List<string> ProcessSequence(IEnumerable<MadeItemProcessResponse> processes)
        {
            return (processes ?? Enumerable.Empty<MadeItemProcessResponse>())
                .OrderBy(process => process.Position)
                .Select(process => StepSignature(
                    process.Role,
                    process.Role == StepRole.Fabrication ? process.FabricationMethodId : process.InspectionMethodId,
                    process.GroupIndex, process.EstimatedSetupMinutes, process.EstimatedMinutesPerPiece, process.Note))
                .ToList();
        }

        private static string StepSignature(
            StepRole role,
            int? methodId,
            int groupIndex,
            int? estimatedSetupMinutes,
            double? estimatedMinutesPerPiece,
            string note)
        {
            return string.Join(
                "|",
                role.ToString(),
                methodId?.ToString(CultureInfo.InvariantCulture) ?? "",
                groupIndex.ToString(CultureInfo.InvariantCulture),
                estimatedSetupMinutes?.ToString(CultureInfo.InvariantCulture) ?? "",
                estimatedMinutesPerPiece?.ToString(CultureInfo.InvariantCulture) ?? "",
                note ?? "");
        }
    }
}

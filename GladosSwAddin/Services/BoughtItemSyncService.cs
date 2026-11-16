using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Api.Services;
using GladosSwAddin.Models;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Read-only comparison of a local BoughtItemProperties against the Glados bought-item
    /// catalogue, matched by PDM.PartNumber. Creating and updating catalogue items is owned
    /// by the PDM ingest path, outside this addin — see the sync strategy in README.md. This
    /// addin only ever reads, to show the user whether their document is in sync and to flag
    /// likely-duplicate data entry.
    /// </summary>
    public class BoughtItemSyncService
    {
        private readonly BoughtItemsApiService _boughtItems;

        public BoughtItemSyncService(BoughtItemsApiService boughtItems)
        {
            _boughtItems = boughtItems;
        }

        public async Task<BoughtItemLookupResult> LookupAsync(BoughtItemProperties local)
        {
            if (!local.HasPartNumber)
            {
                var unassignedDuplicate = await FindPossibleDuplicateAsync(local);
                return unassignedDuplicate == null
                    ? new BoughtItemLookupResult { State = BoughtItemMatchState.NoPartNumber }
                    : new BoughtItemLookupResult
                    {
                        State = BoughtItemMatchState.PossibleDuplicate,
                        RemoteItem = unassignedDuplicate,
                    };
            }

            BoughtItemResponse remote;
            try
            {
                remote = await _boughtItems.GetBoughtItemByPartNumberAsync(local.PartNumber);
            }
            catch (GladosApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                var duplicate = await FindPossibleDuplicateAsync(local);
                return duplicate == null
                    ? new BoughtItemLookupResult { State = BoughtItemMatchState.NotFound }
                    : new BoughtItemLookupResult
                    {
                        State = BoughtItemMatchState.PossibleDuplicate,
                        RemoteItem = duplicate,
                    };
            }

            var divergentFields = GetDivergentFields(local, remote);

            return new BoughtItemLookupResult
            {
                State = divergentFields.Count == 0
                    ? BoughtItemMatchState.FoundInSync
                    : BoughtItemMatchState.FoundDiverges,
                RemoteItem = remote,
                DivergentFields = divergentFields,
            };
        }

        /// <summary>Looks for exactly one catalogue item sharing this Manufacturer + OrderCode1
        /// that has no PartNumber assigned yet — most likely created via the web before PDM/
        /// SolidWorks assigned this part its number. Returns null if there's no candidate, more
        /// than one (an ambiguous match isn't a confident enough signal to flag), or Manufacturer/
        /// OrderCode1 aren't both filled in yet. Independent of PartNumber — callable (e.g. live,
        /// on focus loss) before this part even has one.</summary>
        public async Task<BoughtItemResponse> FindPossibleDuplicateAsync(BoughtItemProperties local)
        {
            if (local.ManufacturerId == null || string.IsNullOrWhiteSpace(local.OrderCode1))
                return null;

            var page = await _boughtItems.GetBoughtItemsAsync(
                new BoughtItemFilterParams
                {
                    ManufacturerId = local.ManufacturerId,
                    OrderCode1 = local.OrderCode1,
                });

            var candidates = (page.Items ?? new List<BoughtItemResponse>())
                .Where(item => string.IsNullOrWhiteSpace(item.PartNumber))
                .ToList();

            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>Compares reference keys and catalogue text only. The resolved *Name labels
        /// are deliberately excluded: they're a denormalized copy that goes stale the moment
        /// somebody renames a supplier in Glados, and comparing them would mark every affected
        /// document in the vault as diverged over a change that isn't one. Don't add them here.
        /// EdpPartId stays out too: it is a link the web may set differently on purpose, not
        /// catalogue text.</summary>
        private static List<string> GetDivergentFields(BoughtItemProperties local, BoughtItemResponse remote)
        {
            var divergent = new List<string>();

            void Compare(string field, object localValue, object remoteValue)
            {
                if (!Equals(localValue, remoteValue))
                    divergent.Add(field);
            }

            Compare(nameof(local.ArticleName), local.ArticleName ?? "", remote.ArticleName ?? "");
            Compare(nameof(local.OrderCode1), local.OrderCode1 ?? "", remote.OrderCode1 ?? "");
            Compare(nameof(local.OrderCode2), local.OrderCode2 ?? "", remote.OrderCode2 ?? "");
            Compare(nameof(local.Gtin), local.Gtin ?? "", remote.Gtin ?? "");
            Compare(nameof(local.Ean), local.Ean ?? "", remote.Ean ?? "");
            Compare(nameof(local.TariffNumber), local.TariffNumber ?? "", remote.TariffNumber ?? "");
            Compare(nameof(local.Mass), local.Mass, remote.Mass);
            Compare(nameof(local.Note), local.Note ?? "", remote.Note ?? "");
            Compare(nameof(local.Weblink), local.Weblink ?? "", remote.Weblink ?? "");
            Compare(nameof(local.ManufacturerId), local.ManufacturerId, remote.ManufacturerId);

            var localGroups = new HashSet<int>(local.ArticleGroupIds ?? new List<int>());
            var remoteGroups = new HashSet<int>(
                (remote.ArticleGroups ?? new List<ArticleGroupResponse>()).Select(g => g.Id));
            if (!localGroups.SetEquals(remoteGroups))
                divergent.Add(nameof(local.ArticleGroupIds));

            return divergent;
        }
    }
}

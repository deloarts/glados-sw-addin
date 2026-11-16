using System.Collections.Generic;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Models
{
    /// <summary>
    /// Deliberately has no PossibleDuplicate tier, unlike BoughtItemMatchState. A bought item
    /// can be "adopted" sight-unseen because Manufacturer + OrderCode1 is a strong enough key
    /// to guess a likely match before PartNumber exists. A made item has no such match key —
    /// FIELD_OWNERSHIP.md's made-item rules never auto-claim a Glados-numbered catalogue row —
    /// so before PartNumber is assigned there is nothing to look up at all, and after a
    /// PartNumber miss there is nothing else worth guessing at either.
    /// </summary>
    public enum MadeItemMatchState
    {
        /// <summary>No PDM.PartNumber set locally yet — nothing to report.</summary>
        NoPartNumber,

        /// <summary>PartNumber is set but no catalogue item matches it (any revision).</summary>
        NotFound,

        /// <summary>Matches the newest revision of the part-number family and all syncable
        /// fields agree.</summary>
        FoundInSync,

        /// <summary>Matches the newest revision of the part-number family but local values
        /// differ — usually because these document changes (or a routing edit) haven't been
        /// ingested into Glados yet, or the newest catalogue revision is ahead of this
        /// document's PDM revision.</summary>
        FoundDiverges,
    }

    public class MadeItemLookupResult
    {
        public MadeItemMatchState State { get; set; }

        public MadeItemResponse RemoteItem { get; set; }

        public List<string> DivergentFields { get; set; } = new List<string>();
    }
}

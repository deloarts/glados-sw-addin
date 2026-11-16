using System.Collections.Generic;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Models
{
    public enum BoughtItemMatchState
    {
        /// <summary>No PDM.PartNumber set locally, and no unassigned Manufacturer + OrderCode1
        /// match either — nothing to report yet.</summary>
        NoPartNumber,

        /// <summary>PartNumber is set but no catalogue item matches it.</summary>
        NotFound,

        /// <summary>No catalogue item matches PartNumber (or PartNumber isn't set locally yet),
        /// but exactly one item with no PartNumber assigned shares this Manufacturer + OrderCode1
        /// — most likely created via the web before this part existed in PDM, or before PDM/
        /// SolidWorks assigned it a number. RemoteItem holds that candidate.</summary>
        PossibleDuplicate,

        /// <summary>Matches a catalogue item and all syncable fields agree.</summary>
        FoundInSync,

        /// <summary>Matches a catalogue item but local values differ — usually because these
        /// document changes haven't been ingested into Glados yet.</summary>
        FoundDiverges,
    }

    public class BoughtItemLookupResult
    {
        public BoughtItemMatchState State { get; set; }

        public BoughtItemResponse RemoteItem { get; set; }

        public List<string> DivergentFields { get; set; } = new List<string>();
    }
}

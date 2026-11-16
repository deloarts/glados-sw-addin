using System.Collections.Generic;

namespace GladosSwAddin.Api.Filters
{
    /// <summary>Subset of MadeItemFilterParams in
    /// src/modules/catalogue/adapters/inbound/api/schemas/made_item_schema.py — only what the add-in sends.</summary>
    public class MadeItemFilterParams : IFilterParams
    {
        public int? Skip { get; set; }

        public int? Limit { get; set; }

        public string PartNumber { get; set; }

        public string ArticleName { get; set; }

        /// <summary>Picks one revision of the part-number family instead of the newest.</summary>
        public string Revision { get; set; }

        /// <summary>When true, GetMadeItemsAsync returns only the newest revision per
        /// part-number family.</summary>
        public bool? LatestOnly { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToQueryParameters()
        {
            yield return new KeyValuePair<string, object>("skip", Skip);
            yield return new KeyValuePair<string, object>("limit", Limit);
            yield return new KeyValuePair<string, object>("part_number", PartNumber);
            yield return new KeyValuePair<string, object>("article_name", ArticleName);
            yield return new KeyValuePair<string, object>("revision", Revision);
            yield return new KeyValuePair<string, object>("latest_only", LatestOnly);
        }
    }
}

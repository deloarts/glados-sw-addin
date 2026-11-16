using System.Collections.Generic;

namespace GladosSwAddin.Api.Filters
{
    /// <summary>Mirrors SourceItemTypeFilterParams in src/modules/catalogue/adapters/inbound/api/schemas/source_item_type_schema.py.</summary>
    public class SourceItemTypeFilterParams : IFilterParams
    {
        public int? Skip { get; set; }

        public int? Limit { get; set; }

        public string Search { get; set; }

        public int? Id { get; set; }

        public string Name { get; set; }

        public int? CreatorId { get; set; }

        /// <summary>One of: id, name, created, changed.</summary>
        public string OrderBy { get; set; }

        /// <summary>One of: asc, desc.</summary>
        public string OrderDir { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToQueryParameters()
        {
            yield return new KeyValuePair<string, object>("skip", Skip);
            yield return new KeyValuePair<string, object>("limit", Limit);
            yield return new KeyValuePair<string, object>("search", Search);
            yield return new KeyValuePair<string, object>("id", Id);
            yield return new KeyValuePair<string, object>("name", Name);
            yield return new KeyValuePair<string, object>("creator_id", CreatorId);
            yield return new KeyValuePair<string, object>("order_by", OrderBy);
            yield return new KeyValuePair<string, object>("order_dir", OrderDir);
        }
    }
}

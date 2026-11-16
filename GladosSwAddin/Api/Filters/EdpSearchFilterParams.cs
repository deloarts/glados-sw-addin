using System.Collections.Generic;

namespace GladosSwAddin.Api.Filters
{
    /// <summary>The query of GET /pat/bought-items-edp/search in
    /// src/modules/external_data/adapters/inbound/api/v1/pat/endpoints/bought_items_edp.py.</summary>
    public class EdpSearchFilterParams : IFilterParams
    {
        /// <summary>A number or a name, at least two characters.</summary>
        public string Q { get; set; }

        /// <summary>1 to 20; the backend's default of 10 when null.</summary>
        public int? Limit { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToQueryParameters()
        {
            yield return new KeyValuePair<string, object>("q", Q);
            yield return new KeyValuePair<string, object>("limit", Limit);
        }
    }
}

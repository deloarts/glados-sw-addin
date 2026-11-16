using System.Collections.Generic;

namespace GladosSwAddin.Api.Filters
{
    /// <summary>The query of GET /pat/bought-items-edp/match in
    /// src/modules/external_data/adapters/inbound/api/v1/pat/endpoints/bought_items_edp.py.</summary>
    public class EdpMatchFilterParams : IFilterParams
    {
        /// <summary>An order, type or EPLAN part number, matched exactly by the portal.</summary>
        public string Number { get; set; }

        /// <summary>Narrows several hits to the parts of this supplier's Data Portal
        /// code.</summary>
        public int? ManufacturerId { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToQueryParameters()
        {
            yield return new KeyValuePair<string, object>("number", Number);
            yield return new KeyValuePair<string, object>("manufacturer_id", ManufacturerId);
        }
    }
}

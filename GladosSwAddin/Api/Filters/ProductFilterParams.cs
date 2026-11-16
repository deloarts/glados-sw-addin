using System.Collections.Generic;

namespace GladosSwAddin.Api.Filters
{
    /// <summary>Mirrors ProductFilterParams in src/modules/workspace/adapters/inbound/api/schemas/product_schema.py.</summary>
    public class ProductFilterParams : IFilterParams
    {
        public int? Id { get; set; }

        public string ProductNumber { get; set; }

        public int? CustomerId { get; set; }

        public string Description { get; set; }

        public bool? IsActive { get; set; }

        public int? DesignatedUserId { get; set; }

        public int? Skip { get; set; }

        public int? Limit { get; set; }

        public string Search { get; set; }

        /// <summary>One of: id, product_number, created.</summary>
        public string OrderBy { get; set; }

        /// <summary>One of: asc, desc.</summary>
        public string OrderDir { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToQueryParameters()
        {
            yield return new KeyValuePair<string, object>("id", Id);
            yield return new KeyValuePair<string, object>("product_number", ProductNumber);
            yield return new KeyValuePair<string, object>("customer_id", CustomerId);
            yield return new KeyValuePair<string, object>("description", Description);
            yield return new KeyValuePair<string, object>("is_active", IsActive);
            yield return new KeyValuePair<string, object>("designated_user_id", DesignatedUserId);
            yield return new KeyValuePair<string, object>("skip", Skip);
            yield return new KeyValuePair<string, object>("limit", Limit);
            yield return new KeyValuePair<string, object>("search", Search);
            yield return new KeyValuePair<string, object>("order_by", OrderBy);
            yield return new KeyValuePair<string, object>("order_dir", OrderDir);
        }
    }
}

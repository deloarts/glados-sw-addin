using System.Collections.Generic;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Filters
{
    /// <summary>Mirrors ProjectFilterParams in src/modules/workspace/adapters/inbound/api/schemas/project_schema.py.</summary>
    public class ProjectFilterParams : IFilterParams
    {
        public int? Id { get; set; }

        public string ProjectNumber { get; set; }

        public int? CustomerId { get; set; }

        public int? ProductId { get; set; }

        public string Description { get; set; }

        public ProjectStatus? Status { get; set; }

        public int? DesignatedUserId { get; set; }

        public int? Skip { get; set; }

        public int? Limit { get; set; }

        public string Search { get; set; }

        /// <summary>One of: id, project_number, created.</summary>
        public string OrderBy { get; set; }

        /// <summary>One of: asc, desc.</summary>
        public string OrderDir { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToQueryParameters()
        {
            yield return new KeyValuePair<string, object>("id", Id);
            yield return new KeyValuePair<string, object>("project_number", ProjectNumber);
            yield return new KeyValuePair<string, object>("customer_id", CustomerId);
            yield return new KeyValuePair<string, object>("product_id", ProductId);
            yield return new KeyValuePair<string, object>("description", Description);
            yield return new KeyValuePair<string, object>("status", Status);
            yield return new KeyValuePair<string, object>("designated_user_id", DesignatedUserId);
            yield return new KeyValuePair<string, object>("skip", Skip);
            yield return new KeyValuePair<string, object>("limit", Limit);
            yield return new KeyValuePair<string, object>("search", Search);
            yield return new KeyValuePair<string, object>("order_by", OrderBy);
            yield return new KeyValuePair<string, object>("order_dir", OrderDir);
        }
    }
}

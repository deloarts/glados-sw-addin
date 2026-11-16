using System.Collections.Generic;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors src/modules/platform/adapters/inbound/api/schemas/pagination_schema.py.</summary>
    public class PageResponse<T>
    {
        public List<T> Items { get; set; }

        public int Total { get; set; }

        public int? Limit { get; set; }

        public int Skip { get; set; }
    }
}

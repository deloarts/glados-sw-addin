using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api
{
    /// <summary>
    /// The add-in never treats a single capped list request as "all rows" — this loops a
    /// skip/limit-paged endpoint, collecting every page's Items, until skip plus what's been
    /// fetched so far reaches the reported Total (or a page comes back empty, defensively, in
    /// case Total is ever wrong). GET /pat/lookups itself doesn't need this — it's unpaged by
    /// design (docs/FIELD_OWNERSHIP.md row 11) — but any other skip/limit-paged "fetch the
    /// whole list" call goes through here instead of a single Limit-capped request.
    /// </summary>
    internal static class PagingHelper
    {
        private const int DefaultPageSize = 200;

        public static async Task<List<T>> FetchAllAsync<T>(
            Func<int, int, Task<PageResponse<T>>> fetchPage,
            int pageSize = DefaultPageSize)
        {
            if (fetchPage == null)
                throw new ArgumentNullException(nameof(fetchPage));

            var results = new List<T>();
            var skip = 0;

            while (true)
            {
                var page = await fetchPage(skip, pageSize).ConfigureAwait(false);
                var items = page?.Items ?? new List<T>();
                results.AddRange(items);

                if (items.Count == 0)
                    break;

                skip += items.Count;

                if (skip >= (page?.Total ?? skip))
                    break;
            }

            return results;
        }
    }
}

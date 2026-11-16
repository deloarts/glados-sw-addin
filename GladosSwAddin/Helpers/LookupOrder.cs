using System;
using System.Collections.Generic;
using System.Linq;

namespace GladosSwAddin.Helpers
{
    /// <summary>
    /// The order the pickers offer hand-ordered lookups in: article groups, source item types and
    /// the fabrication and inspection methods carry a sort_order in Glados, set by moving or
    /// dragging a row in the web app so the entries used most come first, and the add-in follows
    /// it the same way the web pickers do. The lookup snapshot arrives in id order, so the order is
    /// applied here from the field rather than taken from the payload's sequence. Ties go by name,
    /// which also keeps the alphabetical order for a server that predates sort_order (or a
    /// snapshot cached before it), where every row carries 0. Suppliers and customers have no
    /// hand-kept order and stay alphabetical.
    /// </summary>
    public static class LookupOrder
    {
        /// <summary><paramref name="rows"/> by sort order, then by name through
        /// <paramref name="nameComparer"/>.</summary>
        public static IOrderedEnumerable<T> HandKept<T>(
            IEnumerable<T> rows,
            Func<T, int> sortOrderOf,
            Func<T, string> nameOf,
            IComparer<string> nameComparer)
        {
            return (rows ?? Enumerable.Empty<T>())
                .OrderBy(sortOrderOf)
                .ThenBy(nameOf, nameComparer);
        }

        /// <summary>A lookup from a row id to its sort order, for ordering rows that only
        /// reference a hand-ordered lookup (a source item by its type). An id that isn't among
        /// <paramref name="rows"/> sorts after every known one.</summary>
        public static Func<int, int> SortOrderById<T>(
            IEnumerable<T> rows,
            Func<T, int> idOf,
            Func<T, int> sortOrderOf)
        {
            var byId = new Dictionary<int, int>();
            foreach (var row in rows ?? Enumerable.Empty<T>())
                byId[idOf(row)] = sortOrderOf(row);

            return id => byId.TryGetValue(id, out var order) ? order : int.MaxValue;
        }
    }
}

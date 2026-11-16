using System;
using System.Collections.Generic;
using System.Linq;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Helpers
{
    /// <summary>
    /// LookupOrder puts hand-ordered lookups in their Glados order; these cases pin that the sort
    /// order wins over the name, that ties and a server without sort_order fall back to the
    /// alphabetical order, and that a row referencing an unknown id sorts last.
    /// </summary>
    [TestClass]
    public class LookupOrderTests
    {
        private static ArticleGroupResponse Group(int id, string name, int sortOrder)
        {
            return new ArticleGroupResponse { Id = id, Name = name, SortOrder = sortOrder };
        }

        private static List<int> HandKeptIds(IEnumerable<ArticleGroupResponse> groups)
        {
            return LookupOrder
                .HandKept(groups, g => g.SortOrder, g => g.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Id)
                .ToList();
        }

        [TestMethod]
        public void HandKept_OrdersBySortOrderBeforeName()
        {
            var groups = new[] { Group(1, "Alpha", 2), Group(2, "Kappa", 0), Group(3, "Zeta", 1) };

            CollectionAssert.AreEqual(new List<int> { 2, 3, 1 }, HandKeptIds(groups));
        }

        [TestMethod]
        public void HandKept_BreaksTiesByName()
        {
            var groups = new[] { Group(1, "Zeta", 0), Group(2, "alpha", 1), Group(3, "Kappa", 0) };

            CollectionAssert.AreEqual(new List<int> { 3, 1, 2 }, HandKeptIds(groups));
        }

        [TestMethod]
        public void HandKept_WithoutSortOrderIsAlphabetical()
        {
            // A server that predates sort_order, or a snapshot cached before it: every row is 0.
            var groups = new[] { Group(1, "Zeta", 0), Group(2, "Alpha", 0), Group(3, "Kappa", 0) };

            CollectionAssert.AreEqual(new List<int> { 2, 3, 1 }, HandKeptIds(groups));
        }

        [TestMethod]
        public void HandKept_NullRowsIsEmpty()
        {
            Assert.AreEqual(0, HandKeptIds(null).Count);
        }

        [TestMethod]
        public void SortOrderById_ResolvesKnownIdsAndPutsUnknownLast()
        {
            var types = new[]
            {
                new SourceItemTypeResponse { Id = 10, SortOrder = 1 },
                new SourceItemTypeResponse { Id = 20, SortOrder = 0 },
            };

            var order = LookupOrder.SortOrderById(types, t => t.Id, t => t.SortOrder);

            Assert.AreEqual(1, order(10));
            Assert.AreEqual(0, order(20));
            Assert.AreEqual(int.MaxValue, order(99));
        }
    }
}

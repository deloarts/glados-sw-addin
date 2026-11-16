using System;
using System.Collections.Generic;
using System.Linq;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Guesses a new customer item's customer from where its file lies: received models are
    /// usually filed under a folder named after the customer, so a folder of the document's path
    /// whose name equals a customer's name suggests that customer. The guess only pre-selects
    /// the Made tab's customer picker on Create Customer Item, so it stays strict and returns
    /// nothing rather than a doubtful match. Kept static and UI-free so it is testable without a
    /// loaded document.
    /// </summary>
    public static class CustomerFolderMatcher
    {
        /// <summary>The customer whose name equals a folder name of <paramref name="filePath"/>,
        /// compared without case and with runs of whitespace collapsed. The folder closest to
        /// the file wins, since a customer folder can sit inside a broader one. Archived
        /// customers never match, because a new item should not start out on one, and a folder
        /// name that two customers share yields null instead of an arbitrary pick. Null as well
        /// for a never saved document, whose path is empty.</summary>
        public static CustomerLookupResponse Match(
            string filePath, IEnumerable<CustomerLookupResponse> customers)
        {
            if (string.IsNullOrWhiteSpace(filePath) || customers == null)
                return null;

            var byName = customers
                .Where(c => c != null && !c.Archived && !string.IsNullOrWhiteSpace(c.Name))
                .GroupBy(c => Normalize(c.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var segments = filePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            // The last segment is the file name itself, so the walk starts one above it.
            for (var i = segments.Length - 2; i >= 0; i--)
            {
                if (byName.TryGetValue(Normalize(segments[i]), out var matches))
                    return matches.Count == 1 ? matches[0] : null;
            }

            return null;
        }

        private static string Normalize(string value)
        {
            return string.Join(
                " ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}

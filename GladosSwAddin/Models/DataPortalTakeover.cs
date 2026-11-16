using System;
using System.Collections.Generic;
using System.Linq;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Models
{
    /// <summary>The bought-tab fields a Data Portal part can fill, in the order the dialog
    /// lists them.</summary>
    public enum DataPortalField
    {
        ArticleName,
        OrderCode1,
        OrderCode2,
        Ean,
        Weblink,
        Manufacturer,
        ArticleGroups,
    }

    /// <summary>One line of the Data Portal dialog's comparison: what the tab holds now, what
    /// the part would put there, and whether taking it would change anything.</summary>
    public class DataPortalFieldRow
    {
        public DataPortalField Field { get; set; }

        public string Current { get; set; }

        public string Portal { get; set; }

        /// <summary>The part has a value for this field that differs from the current one, so
        /// taking it changes something.</summary>
        public bool Takeable { get; set; }

        /// <summary>Ticked when the dialog opens: only fields that are still empty. A value the
        /// engineer already typed is shown, never replaced unless they tick it.</summary>
        public bool TakeByDefault { get; set; }
    }

    /// <summary>Compares the bought tab's current values with the values the backend drafted
    /// from a Data Portal part (EdpItemDraftResponse) and applies the fields the engineer
    /// chooses. Pure: the dialog renders Compare's rows, and the task pane shows Apply's result
    /// as unsaved edits, so nothing reaches the document before Save. Article groups are added
    /// to the current ones rather than replacing them, since the draft only carries the
    /// portal's default groups. The part's id is taken with any choice, even none: taking a
    /// part links the document to it.</summary>
    public static class DataPortalTakeover
    {
        /// <param name="manufacturerName">Resolves a supplier id to its name (null when
        /// unknown), for display only.</param>
        /// <param name="articleGroupNames">Resolves article group ids to one display text.</param>
        public static IReadOnlyList<DataPortalFieldRow> Compare(
            BoughtItemProperties current,
            EdpItemDraftResponse draft,
            Func<int?, string> manufacturerName,
            Func<IEnumerable<int>, string> articleGroupNames)
        {
            current = current ?? new BoughtItemProperties();
            var rows = new List<DataPortalFieldRow>
            {
                TextRow(DataPortalField.ArticleName, current.ArticleName, draft.ArticleName),
                TextRow(DataPortalField.OrderCode1, current.OrderCode1, draft.OrderCode1),
                TextRow(DataPortalField.OrderCode2, current.OrderCode2, draft.OrderCode2),
                TextRow(DataPortalField.Ean, current.Ean, draft.Ean),
                TextRow(DataPortalField.Weblink, current.Weblink, draft.Weblink),
            };

            var manufacturerTakeable =
                draft.ManufacturerId.HasValue && draft.ManufacturerId != current.ManufacturerId;
            rows.Add(new DataPortalFieldRow
            {
                Field = DataPortalField.Manufacturer,
                Current = current.ManufacturerId.HasValue
                    ? manufacturerName(current.ManufacturerId) ?? current.ManufacturerName
                    : null,
                Portal = draft.ManufacturerId.HasValue
                    ? manufacturerName(draft.ManufacturerId)
                    : null,
                Takeable = manufacturerTakeable,
                TakeByDefault = manufacturerTakeable && !current.ManufacturerId.HasValue,
            });

            var currentGroups = current.ArticleGroupIds ?? new List<int>();
            var draftGroups = draft.ArticleGroupIds ?? new List<int>();
            var groupsTakeable = draftGroups.Except(currentGroups).Any();
            rows.Add(new DataPortalFieldRow
            {
                Field = DataPortalField.ArticleGroups,
                Current = currentGroups.Count > 0 ? articleGroupNames(currentGroups) : null,
                Portal = draftGroups.Count > 0 ? articleGroupNames(draftGroups) : null,
                Takeable = groupsTakeable,
                TakeByDefault = groupsTakeable && currentGroups.Count == 0,
            });

            return rows;
        }

        /// <summary>A copy of current with the chosen fields taken from the draft and the part
        /// linked. A chosen field the draft has no value for keeps its current value. A label
        /// whose key changed comes back empty (ManufacturerName null, ArticleGroupNames empty)
        /// for the caller to resolve from its lookup lists; the task pane re-resolves labels on
        /// every Save anyway.</summary>
        public static BoughtItemProperties Apply(
            BoughtItemProperties current, EdpItemDraftResponse draft, IEnumerable<DataPortalField> chosen)
        {
            current = current ?? new BoughtItemProperties();
            var take = new HashSet<DataPortalField>(chosen ?? Enumerable.Empty<DataPortalField>());

            string Pick(DataPortalField field, string currentValue, string portalValue) =>
                take.Contains(field) && !IsBlank(portalValue) ? portalValue : currentValue;

            var groups = new List<int>(current.ArticleGroupIds ?? new List<int>());
            var addedGroups = take.Contains(DataPortalField.ArticleGroups)
                ? (draft.ArticleGroupIds ?? new List<int>()).Where(id => !groups.Contains(id)).ToList()
                : new List<int>();
            groups.AddRange(addedGroups);

            var manufacturerId = take.Contains(DataPortalField.Manufacturer) && draft.ManufacturerId.HasValue
                ? draft.ManufacturerId
                : current.ManufacturerId;

            return new BoughtItemProperties
            {
                PartNumber = current.PartNumber,
                Revision = current.Revision,
                ArticleName = Pick(DataPortalField.ArticleName, current.ArticleName, draft.ArticleName),
                OrderCode1 = Pick(DataPortalField.OrderCode1, current.OrderCode1, draft.OrderCode1),
                OrderCode2 = Pick(DataPortalField.OrderCode2, current.OrderCode2, draft.OrderCode2),
                Gtin = current.Gtin,
                Ean = Pick(DataPortalField.Ean, current.Ean, draft.Ean),
                TariffNumber = current.TariffNumber,
                Mass = current.Mass,
                Note = current.Note,
                Weblink = Pick(DataPortalField.Weblink, current.Weblink, draft.Weblink),
                ManufacturerId = manufacturerId,
                ManufacturerName = manufacturerId == current.ManufacturerId ? current.ManufacturerName : null,
                ArticleGroupIds = groups,
                // Positionally aligned with the ids, so it only survives while they are unchanged.
                ArticleGroupNames = addedGroups.Count == 0
                    ? new List<string>(current.ArticleGroupNames ?? new List<string>())
                    : new List<string>(),
                EdpPartId = draft.EdpPartId,
            };
        }

        private static DataPortalFieldRow TextRow(DataPortalField field, string current, string portal)
        {
            var takeable = !IsBlank(portal) && !SameText(current, portal);
            return new DataPortalFieldRow
            {
                Field = field,
                Current = current,
                Portal = portal,
                Takeable = takeable,
                TakeByDefault = takeable && IsBlank(current),
            };
        }

        private static bool IsBlank(string value) => string.IsNullOrWhiteSpace(value);

        private static bool SameText(string a, string b) =>
            string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.Ordinal);
    }
}

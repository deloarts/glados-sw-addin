using System.Collections.Generic;
using System.Linq;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Models
{
    /// <summary>
    /// Pins DataPortalTakeover: which fields the Data Portal dialog offers and ticks by default,
    /// and what taking them does to the bought tab's values, the article groups being added
    /// rather than replaced and the part being linked whatever is chosen.
    /// </summary>
    [TestClass]
    public class DataPortalTakeoverTests
    {
        private static EdpItemDraftResponse Draft() => new EdpItemDraftResponse
        {
            EdpPartId = 4711,
            ArticleName = "Zylinder DSNU-25",
            OrderCode1 = "19210",
            OrderCode2 = "DSNU-25-50-P-A",
            Ean = "4052568123456",
            Weblink = "https://example.test/19210",
            ManufacturerCode = "FES",
            ManufacturerId = 7,
            ArticleGroupIds = new List<int> { 3 },
        };

        private static IReadOnlyList<DataPortalFieldRow> Compare(BoughtItemProperties current, EdpItemDraftResponse draft) =>
            DataPortalTakeover.Compare(
                current,
                draft,
                id => id.HasValue ? "Supplier " + id.Value : null,
                ids => string.Join("; ", ids));

        private static DataPortalFieldRow Row(IEnumerable<DataPortalFieldRow> rows, DataPortalField field) =>
            rows.Single(r => r.Field == field);

        [TestMethod]
        public void Compare_ListsEveryFieldInDialogOrder()
        {
            var rows = Compare(new BoughtItemProperties(), Draft());

            CollectionAssert.AreEqual(
                new[]
                {
                    DataPortalField.ArticleName,
                    DataPortalField.OrderCode1,
                    DataPortalField.OrderCode2,
                    DataPortalField.Ean,
                    DataPortalField.Weblink,
                    DataPortalField.Manufacturer,
                    DataPortalField.ArticleGroups,
                },
                rows.Select(r => r.Field).ToArray());
        }

        [TestMethod]
        public void Compare_TicksEveryFieldOfAnEmptyTab()
        {
            var rows = Compare(new BoughtItemProperties(), Draft());

            Assert.IsTrue(rows.All(r => r.Takeable && r.TakeByDefault));
            Assert.AreEqual("Supplier 7", Row(rows, DataPortalField.Manufacturer).Portal);
        }

        [TestMethod]
        public void Compare_OffersButDoesNotTickAValueTheEngineerAlreadyTyped()
        {
            var rows = Compare(new BoughtItemProperties { ArticleName = "My cylinder" }, Draft());

            var row = Row(rows, DataPortalField.ArticleName);
            Assert.IsTrue(row.Takeable);
            Assert.IsFalse(row.TakeByDefault);
            Assert.AreEqual("My cylinder", row.Current);
        }

        [TestMethod]
        public void Compare_ASameValueIgnoringOuterWhitespaceIsNothingToTake()
        {
            var rows = Compare(new BoughtItemProperties { OrderCode1 = " 19210 " }, Draft());

            Assert.IsFalse(Row(rows, DataPortalField.OrderCode1).Takeable);
        }

        [TestMethod]
        public void Compare_AFieldThePartLeavesEmptyIsNothingToTake()
        {
            var draft = Draft();
            draft.Ean = null;

            var rows = Compare(new BoughtItemProperties(), draft);

            Assert.IsFalse(Row(rows, DataPortalField.Ean).Takeable);
            Assert.IsFalse(Row(rows, DataPortalField.Ean).TakeByDefault);
        }

        [TestMethod]
        public void Compare_AnUnresolvedManufacturerCanNotBeTaken()
        {
            var draft = Draft();
            draft.ManufacturerId = null;

            var row = Row(Compare(new BoughtItemProperties(), draft), DataPortalField.Manufacturer);

            Assert.IsFalse(row.Takeable);
            Assert.IsNull(row.Portal);
        }

        [TestMethod]
        public void Compare_ArticleGroupsAlreadyCheckedAreNothingToTake()
        {
            var rows = Compare(new BoughtItemProperties { ArticleGroupIds = new List<int> { 3, 5 } }, Draft());

            Assert.IsFalse(Row(rows, DataPortalField.ArticleGroups).Takeable);
        }

        [TestMethod]
        public void Compare_MissingArticleGroupsAreOfferedButNotTickedOnAGroupedTab()
        {
            var rows = Compare(new BoughtItemProperties { ArticleGroupIds = new List<int> { 5 } }, Draft());

            var row = Row(rows, DataPortalField.ArticleGroups);
            Assert.IsTrue(row.Takeable);
            Assert.IsFalse(row.TakeByDefault);
        }

        [TestMethod]
        public void Apply_TakesOnlyTheChosenFieldsAndKeepsTheRest()
        {
            var current = new BoughtItemProperties
            {
                PartNumber = "CAD-1",
                ArticleName = "Mine",
                Gtin = "123",
                TariffNumber = "8412",
                Mass = 250,
                Note = "keep",
            };

            var result = DataPortalTakeover.Apply(
                current, Draft(), new[] { DataPortalField.OrderCode1, DataPortalField.Ean });

            Assert.AreEqual("Mine", result.ArticleName);
            Assert.AreEqual("19210", result.OrderCode1);
            Assert.AreEqual("4052568123456", result.Ean);
            Assert.IsNull(result.OrderCode2);
            Assert.AreEqual("CAD-1", result.PartNumber);
            Assert.AreEqual("123", result.Gtin);
            Assert.AreEqual("8412", result.TariffNumber);
            Assert.AreEqual(250, result.Mass);
            Assert.AreEqual("keep", result.Note);
        }

        [TestMethod]
        public void Apply_LinksThePartEvenWithNothingChosen()
        {
            var result = DataPortalTakeover.Apply(new BoughtItemProperties(), Draft(), new DataPortalField[0]);

            Assert.AreEqual(4711, result.EdpPartId);
        }

        [TestMethod]
        public void Apply_AddsArticleGroupsAndClearsTheLabelsThatNoLongerAlign()
        {
            var current = new BoughtItemProperties
            {
                ArticleGroupIds = new List<int> { 5 },
                ArticleGroupNames = new List<string> { "Valves" },
            };

            var result = DataPortalTakeover.Apply(current, Draft(), new[] { DataPortalField.ArticleGroups });

            CollectionAssert.AreEqual(new[] { 5, 3 }, result.ArticleGroupIds.ToArray());
            Assert.AreEqual(0, result.ArticleGroupNames.Count);
        }

        [TestMethod]
        public void Apply_KeepsTheArticleGroupLabelsWhenTheGroupsStayTheSame()
        {
            var current = new BoughtItemProperties
            {
                ArticleGroupIds = new List<int> { 3 },
                ArticleGroupNames = new List<string> { "Cylinders" },
            };

            var result = DataPortalTakeover.Apply(current, Draft(), new[] { DataPortalField.ArticleGroups });

            CollectionAssert.AreEqual(new[] { 3 }, result.ArticleGroupIds.ToArray());
            CollectionAssert.AreEqual(new[] { "Cylinders" }, result.ArticleGroupNames.ToArray());
        }

        [TestMethod]
        public void Apply_ANewManufacturerDropsTheOldLabel()
        {
            var current = new BoughtItemProperties { ManufacturerId = 2, ManufacturerName = "Other" };

            var result = DataPortalTakeover.Apply(current, Draft(), new[] { DataPortalField.Manufacturer });

            Assert.AreEqual(7, result.ManufacturerId);
            Assert.IsNull(result.ManufacturerName);
        }

        [TestMethod]
        public void Apply_AChosenFieldThePartLeavesEmptyKeepsTheCurrentValue()
        {
            var draft = Draft();
            draft.Weblink = " ";
            draft.ManufacturerId = null;
            var current = new BoughtItemProperties { Weblink = "https://mine.test", ManufacturerId = 2 };

            var result = DataPortalTakeover.Apply(
                current, draft, new[] { DataPortalField.Weblink, DataPortalField.Manufacturer });

            Assert.AreEqual("https://mine.test", result.Weblink);
            Assert.AreEqual(2, result.ManufacturerId);
        }
    }
}

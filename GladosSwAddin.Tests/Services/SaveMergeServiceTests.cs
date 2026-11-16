using System.Collections.Generic;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;
using GladosSwAddin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Services
{
    /// <summary>
    /// Covers SaveMergeService, the per-field decision a Save makes between the pane's edits
    /// and the file as it is at Save time, so a value changed outside the add-in (a PDM data
    /// card) is kept unless the user edited the same field.
    /// </summary>
    [TestClass]
    public class SaveMergeServiceTests
    {
        private static BoughtItemProperties Bought(string note = "A", string orderCode1 = "OC")
        {
            return new BoughtItemProperties
            {
                ArticleName = "Bolt",
                OrderCode1 = orderCode1,
                Note = note,
                ManufacturerId = 1,
                ManufacturerName = "ACME",
                ArticleGroupIds = new List<int> { 1, 2 },
                ArticleGroupNames = new List<string> { "One", "Two" },
            };
        }

        private static MadeItemProperties Made(params ProcessStep[] steps)
        {
            return new MadeItemProperties
            {
                ArticleName = "Bracket",
                Note = "A",
                Material = "S235",
                SourceItemId = 5,
                SourceItemName = "Block",
                Processes = new List<ProcessStep>(steps),
            };
        }

        private static ProcessStep Step(int methodId, int groupIndex = 0)
        {
            return new ProcessStep
            {
                Role = StepRole.Fabrication,
                FabricationMethodId = methodId,
                GroupIndex = groupIndex,
            };
        }

        [TestMethod]
        public void UntouchedField_ChangedInFile_TakesTheFileValue()
        {
            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(
                Bought(), Bought(orderCode1: "OC-new"), Bought(note: "B"), out conflicts);

            Assert.AreEqual("B", merged.Note);
            Assert.AreEqual("OC-new", merged.OrderCode1);
            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void EditedField_UnchangedInFile_KeepsThePaneValue()
        {
            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(Bought(), Bought(note: "C"), Bought(), out conflicts);

            Assert.AreEqual("C", merged.Note);
            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void BothSidesChangedToTheSameValue_IsNoConflict()
        {
            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(Bought(), Bought(note: "B"), Bought(note: "B"), out conflicts);

            Assert.AreEqual("B", merged.Note);
            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void BothSidesChangedDifferently_IsAConflict_ThatKeepsThePaneValueUntilResolved()
        {
            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(Bought(), Bought(note: "C"), Bought(note: "B"), out conflicts);

            CollectionAssert.AreEqual(new[] { ItemField.Note }, conflicts);
            Assert.AreEqual("C", merged.Note);
        }

        [TestMethod]
        public void TakeFromFile_ReplacesOnlyTheNamedFields()
        {
            var file = Bought(note: "B", orderCode1: "OC-file");
            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(
                Bought(), Bought(note: "C", orderCode1: "OC-mine"), file, out conflicts);

            SaveMergeService.TakeFromFile(merged, file, new[] { ItemField.Note });

            Assert.AreEqual("B", merged.Note);
            Assert.AreEqual("OC-mine", merged.OrderCode1);
        }

        [TestMethod]
        public void NullAndEmpty_AreTheSameValue()
        {
            var baseline = Bought(note: null);
            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(baseline, Bought(note: "C"), Bought(note: ""), out conflicts);

            Assert.AreEqual("C", merged.Note);
            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void ArticleGroups_CompareAsASet()
        {
            var file = Bought();
            file.ArticleGroupIds = new List<int> { 2, 1 };
            var pane = Bought();
            pane.ArticleGroupIds = new List<int> { 3 };

            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(Bought(), pane, file, out conflicts);

            CollectionAssert.AreEqual(new List<int> { 3 }, merged.ArticleGroupIds);
            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void ChangedKey_IsTakenTogetherWithItsLabel()
        {
            var file = Bought();
            file.ManufacturerId = 2;
            file.ManufacturerName = "Contoso";

            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(Bought(), Bought(), file, out conflicts);

            Assert.AreEqual(2, merged.ManufacturerId);
            Assert.AreEqual("Contoso", merged.ManufacturerName);
        }

        [TestMethod]
        public void Labels_AreNeverCompared()
        {
            var file = Bought();
            file.ManufacturerName = "Renamed on the card";
            var pane = Bought();
            pane.ManufacturerName = "ACME Ltd";

            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeBought(Bought(), pane, file, out conflicts);

            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual("ACME Ltd", merged.ManufacturerName);
        }

        [TestMethod]
        public void Merge_DoesNotMutateItsInputs()
        {
            var pane = Made(Step(1));
            var file = Made(Step(2));
            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeMade(Made(Step(1)), pane, file, out conflicts);

            merged.Processes[0].FabricationMethodId = 9;

            Assert.AreEqual(1, pane.Processes[0].FabricationMethodId);
            Assert.AreEqual(2, file.Processes[0].FabricationMethodId);
        }

        [TestMethod]
        public void Routing_UntouchedInPane_TakesTheFileRouting()
        {
            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeMade(Made(Step(1)), Made(Step(1)), Made(Step(1), Step(2, 1)), out conflicts);

            Assert.AreEqual(2, merged.Processes.Count);
            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void Routing_EditedOnBothSides_IsAConflict()
        {
            List<ItemField> conflicts;
            SaveMergeService.MergeMade(Made(Step(1)), Made(Step(3)), Made(Step(2)), out conflicts);

            CollectionAssert.AreEqual(new[] { ItemField.Processes }, conflicts);
        }

        [TestMethod]
        public void Routing_ThatBecameUnreadable_IsKeptAsUnreadable()
        {
            var file = Made();
            file.ProcessesUnreadable = true;

            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeMade(Made(), Made(), file, out conflicts);

            Assert.IsTrue(merged.ProcessesUnreadable);
            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void Routing_UnreadableSinceLoad_IsNotAChange()
        {
            var baseline = Made();
            baseline.ProcessesUnreadable = true;
            var file = Made();
            file.ProcessesUnreadable = true;

            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeMade(baseline, Made(), file, out conflicts);

            Assert.IsFalse(merged.ProcessesUnreadable);
            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void LinkedFields_AreLeftToTheirOwnSavePath()
        {
            var file = Made();
            file.Material = "1.4301";

            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeMade(Made(), Made(), file, out conflicts);

            Assert.AreEqual("S235", merged.Material);
        }

        [TestMethod]
        public void Assembly_MergesOnlyTheFieldsItEdits()
        {
            var file = Made();
            file.SourceItemId = 6;
            file.Note = "B";

            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeAssembly(Made(), Made(), file, out conflicts);

            Assert.AreEqual(5, merged.SourceItemId);
            Assert.AreEqual("B", merged.Note);
        }

        [TestMethod]
        public void Customer_PartNumberChangedInFile_IsKept()
        {
            var file = Made();
            file.CustomerPartNumber = "K-1";

            List<ItemField> conflicts;
            var merged = SaveMergeService.MergeMade(Made(), Made(), file, out conflicts);

            Assert.AreEqual("K-1", merged.CustomerPartNumber);
        }

        [TestMethod]
        public void HasEdits_FalseForAnUntouchedPane()
        {
            Assert.IsFalse(SaveMergeService.HasEdits(Bought(), Bought()));
            Assert.IsFalse(SaveMergeService.HasEdits(Made(Step(1)), Made(Step(1))));
        }

        [TestMethod]
        public void HasEdits_IgnoresLabelsAndArticleGroupOrder()
        {
            var pane = Bought();
            pane.ManufacturerName = "Other label";
            pane.ArticleGroupIds = new List<int> { 2, 1 };

            Assert.IsFalse(SaveMergeService.HasEdits(Bought(), pane));
        }

        [TestMethod]
        public void HasEdits_SeesATextEdit()
        {
            Assert.IsTrue(SaveMergeService.HasEdits(Bought(), Bought(note: "C")));
        }

        [TestMethod]
        public void HasEdits_SeesARoutingEdit()
        {
            Assert.IsTrue(SaveMergeService.HasEdits(Made(Step(1)), Made(Step(1), Step(2, 1))));
        }

        [TestMethod]
        public void HasEdits_SeesTheLinkedFieldsTheMergeLeavesOut()
        {
            var material = Made();
            material.Material = "1.4301";
            var mass = Made();
            mass.Mass = 1200;

            Assert.IsTrue(SaveMergeService.HasEdits(Made(), material));
            Assert.IsTrue(SaveMergeService.HasEdits(Made(), mass));
        }
    }
}

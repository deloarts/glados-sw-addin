using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;
using GladosSwAddin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Services
{
    /// <summary>
    /// Covers SwCustomPropertyService's pure helpers: process-grouping normalization and
    /// validation, mass parsing, the Glados.Processes JSON codec, the linked-expression rewrite
    /// regexes, and the small string/number parsers behind the Read*/Write* methods — everything
    /// reachable without a live ModelDoc2. TryRewriteSimpleLinkForConfig, ParseProcesses,
    /// ParseIntOrNull, ParseIntList, ParseLabelList and BuildProcessNamesLabel are internal
    /// purely so these tests can reach them; their behaviour is unchanged.
    /// </summary>
    [TestClass]
    public class SwCustomPropertyServiceTests
    {
        private static ProcessStep Fabrication(int groupIndex, int methodId, string methodName = null)
        {
            return new ProcessStep
            {
                Role = StepRole.Fabrication,
                FabricationMethodId = methodId,
                GroupIndex = groupIndex,
                MethodName = methodName,
            };
        }

        private static ProcessStep Inspection(int groupIndex, int methodId, string methodName = null)
        {
            return new ProcessStep
            {
                Role = StepRole.Inspection,
                InspectionMethodId = methodId,
                GroupIndex = groupIndex,
                MethodName = methodName,
            };
        }

        [TestMethod]
        public void NormalizeProcessGroups_KeepsAContiguousRunUnderOneIndex()
        {
            var steps = new List<ProcessStep>
            {
                Fabrication(5, 1),
                Fabrication(5, 2),
                Inspection(9, 3),
            };

            var normalized = SwCustomPropertyService.NormalizeProcessGroups(steps);

            Assert.AreEqual(3, normalized.Count);
            Assert.AreEqual(0, normalized[0].GroupIndex);
            Assert.AreEqual(0, normalized[1].GroupIndex);
            Assert.AreEqual(1, normalized[2].GroupIndex);
        }

        [TestMethod]
        public void NormalizeProcessGroups_SplitsARunThatWasTornApart()
        {
            // Original group 0 reappears after group 1 was inserted between its two members —
            // the second "0" must start a new group rather than merging back into the first.
            var steps = new List<ProcessStep>
            {
                Fabrication(0, 1),
                Fabrication(1, 2),
                Fabrication(0, 3),
            };

            var normalized = SwCustomPropertyService.NormalizeProcessGroups(steps);

            Assert.AreSequenceEqual(new[] { 0, 1, 2 }, normalized.ConvertAll(s => s.GroupIndex));
        }

        [TestMethod]
        public void NormalizeProcessGroups_NeverMutatesTheInputList()
        {
            var original = new List<ProcessStep> { Fabrication(7, 1) };

            SwCustomPropertyService.NormalizeProcessGroups(original);

            Assert.AreEqual(7, original[0].GroupIndex);
        }

        [TestMethod]
        public void NormalizeProcessGroups_PreservesEveryOtherFieldOfEachStep()
        {
            var step = new ProcessStep
            {
                Role = StepRole.Fabrication,
                FabricationMethodId = 4,
                GroupIndex = 0,
                EstimatedSetupMinutes = 15,
                EstimatedMinutesPerPiece = 2.5,
                Note = "deburr",
                MethodName = "Turning",
            };

            var normalized = SwCustomPropertyService.NormalizeProcessGroups(new[] { step });

            var result = normalized[0];
            Assert.AreEqual(StepRole.Fabrication, result.Role);
            Assert.AreEqual(4, result.FabricationMethodId);
            Assert.AreEqual(15, result.EstimatedSetupMinutes);
            Assert.AreEqual(2.5, result.EstimatedMinutesPerPiece);
            Assert.AreEqual("deburr", result.Note);
            Assert.AreEqual("Turning", result.MethodName);
        }

        [TestMethod]
        public void NormalizeProcessGroups_NullInputYieldsAnEmptyList()
        {
            Assert.AreEqual(0, SwCustomPropertyService.NormalizeProcessGroups(null).Count);
        }

        [TestMethod]
        public void ValidateProcessGrouping_AcceptsAWellFormedFabricationStep()
        {
            SwCustomPropertyService.ValidateProcessGrouping(new List<ProcessStep> { Fabrication(0, 1) });
        }

        [TestMethod]
        public void ValidateProcessGrouping_AcceptsAWellFormedInspectionStep()
        {
            SwCustomPropertyService.ValidateProcessGrouping(new List<ProcessStep> { Inspection(0, 1) });
        }

        [TestMethod]
        public void ValidateProcessGrouping_NullListIsANoOp()
        {
            SwCustomPropertyService.ValidateProcessGrouping(null);
        }

        [TestMethod]
        public void ValidateProcessGrouping_TwoFabricationStepsMayShareAGroup()
        {
            SwCustomPropertyService.ValidateProcessGrouping(new List<ProcessStep> { Fabrication(0, 1), Fabrication(0, 2) });
        }

        [TestMethod]
        public void ValidateProcessGrouping_FabricationStepWithoutAFabricationMethodIdThrows()
        {
            var step = new ProcessStep { Role = StepRole.Fabrication, GroupIndex = 0 };
            Assert.ThrowsExactly<InvalidOperationException>(
                () => SwCustomPropertyService.ValidateProcessGrouping(new List<ProcessStep> { step }));
        }

        [TestMethod]
        public void ValidateProcessGrouping_FabricationStepWithAnInspectionMethodIdThrows()
        {
            var step = new ProcessStep
            {
                Role = StepRole.Fabrication,
                FabricationMethodId = 1,
                InspectionMethodId = 2,
                GroupIndex = 0,
            };
            Assert.ThrowsExactly<InvalidOperationException>(
                () => SwCustomPropertyService.ValidateProcessGrouping(new List<ProcessStep> { step }));
        }

        [TestMethod]
        public void ValidateProcessGrouping_InspectionStepWithoutAnInspectionMethodIdThrows()
        {
            var step = new ProcessStep { Role = StepRole.Inspection, GroupIndex = 0 };
            Assert.ThrowsExactly<InvalidOperationException>(
                () => SwCustomPropertyService.ValidateProcessGrouping(new List<ProcessStep> { step }));
        }

        [TestMethod]
        public void ValidateProcessGrouping_InspectionStepWithAFabricationMethodIdThrows()
        {
            var step = new ProcessStep
            {
                Role = StepRole.Inspection,
                InspectionMethodId = 1,
                FabricationMethodId = 2,
                GroupIndex = 0,
            };
            Assert.ThrowsExactly<InvalidOperationException>(
                () => SwCustomPropertyService.ValidateProcessGrouping(new List<ProcessStep> { step }));
        }

        [TestMethod]
        public void ValidateProcessGrouping_AnInspectionStepCanNeverShareAGroup()
        {
            var steps = new List<ProcessStep> { Fabrication(0, 1), Inspection(0, 2) };
            Assert.ThrowsExactly<InvalidOperationException>(() => SwCustomPropertyService.ValidateProcessGrouping(steps));
        }

        [TestMethod]
        public void ParseMassGrams_RoundsAwayFromZeroToTheNearestGram()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.AreEqual(1235, SwCustomPropertyService.ParseMassGrams("1234.56"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void ParseMassGrams_AcceptsTheCurrentCultureDecimalSeparator()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-AT");
                Assert.AreEqual(1235, SwCustomPropertyService.ParseMassGrams("1234,56"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void ParseMassGrams_FallsBackToInvariantWhenTheCurrentCultureCannotParseIt()
        {
            // Under de-AT, "." is a thousands separator, so "100.0" fails the de-AT attempt and
            // must fall through to the invariant-culture attempt instead of returning null.
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-AT");
                Assert.AreEqual(100, SwCustomPropertyService.ParseMassGrams("100.0"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void ParseMassGrams_OutOfRangeValueReturnsNull()
        {
            Assert.IsNull(SwCustomPropertyService.ParseMassGrams("99999999999"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("abc")]
        public void ParseMassGrams_NullBlankOrNonNumericReturnsNull(string value)
        {
            Assert.IsNull(SwCustomPropertyService.ParseMassGrams(value));
        }

        [TestMethod]
        public void ParseMassGrams_TrimsSurroundingWhitespace()
        {
            Assert.AreEqual(100, SwCustomPropertyService.ParseMassGrams("  100  "));
        }

        [TestMethod]
        public void TryRewriteSimpleLinkForConfig_RewritesADocumentFormLink()
        {
            string rewritten;
            var ok = SwCustomPropertyService.TryRewriteSimpleLinkForConfig(
                "\"SW-Mass@part.SLDPRT\"", "Config1", out rewritten);

            Assert.IsTrue(ok);
            Assert.AreEqual("\"SW-Mass@@Config1@part.SLDPRT\"", rewritten);
        }

        [TestMethod]
        public void TryRewriteSimpleLinkForConfig_ReplacesTheConfigNameInAConfigurationFormLink()
        {
            string rewritten;
            var ok = SwCustomPropertyService.TryRewriteSimpleLinkForConfig(
                "\"SW-Material@@Default@part.SLDPRT\"", "Config1", out rewritten);

            Assert.IsTrue(ok);
            Assert.AreEqual("\"SW-Material@@Config1@part.SLDPRT\"", rewritten);
        }

        [TestMethod]
        public void TryRewriteSimpleLinkForConfig_LeavesADimensionLinkAlone()
        {
            string rewritten;
            var ok = SwCustomPropertyService.TryRewriteSimpleLinkForConfig(
                "\"D1@Sketch1@part.SLDPRT\"", "Config1", out rewritten);

            Assert.IsFalse(ok);
            Assert.IsNull(rewritten);
        }

        [TestMethod]
        public void TryRewriteSimpleLinkForConfig_LeavesAPlainValueAlone()
        {
            string rewritten;
            var ok = SwCustomPropertyService.TryRewriteSimpleLinkForConfig("not a link", "Config1", out rewritten);

            Assert.IsFalse(ok);
            Assert.IsNull(rewritten);
        }

        [TestMethod]
        public void BuildProcessNamesLabel_JoinsGroupsWithArrowAndStepsWithinAGroupWithPlus()
        {
            var steps = new List<ProcessStep>
            {
                Fabrication(0, 1, "Saw"),
                Fabrication(1, 2, "Turn"),
                Fabrication(1, 3, "Mill"),
                Inspection(2, 4, "Inspect"),
            };

            var label = SwCustomPropertyService.BuildProcessNamesLabel(steps);

            Assert.AreEqual("Saw → Turn + Mill → Inspect", label);
        }

        [TestMethod]
        public void BuildProcessNamesLabel_OrdersGroupsByGroupIndexRegardlessOfListOrder()
        {
            var steps = new List<ProcessStep> { Fabrication(1, 2, "B"), Fabrication(0, 1, "A") };

            Assert.AreEqual("A → B", SwCustomPropertyService.BuildProcessNamesLabel(steps));
        }

        [TestMethod]
        public void BuildProcessNamesLabel_FallsBackToHashMethodIdWhenTheNameIsUnresolved()
        {
            var step = Fabrication(0, 7);

            Assert.AreEqual("#7", SwCustomPropertyService.BuildProcessNamesLabel(new[] { step }));
        }

        [TestMethod]
        public void BuildProcessNamesLabel_EmptyOrNullInputYieldsAnEmptyString()
        {
            Assert.AreEqual(string.Empty, SwCustomPropertyService.BuildProcessNamesLabel(new List<ProcessStep>()));
            Assert.AreEqual(string.Empty, SwCustomPropertyService.BuildProcessNamesLabel(null));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("   ")]
        public void ParseProcesses_BlankOrMissingJsonIsTheNormalNothingRoutedYetState(string json)
        {
            bool unreadable;
            var steps = SwCustomPropertyService.ParseProcesses(json, out unreadable);

            Assert.AreEqual(0, steps.Count);
            Assert.IsFalse(unreadable);
        }

        [TestMethod]
        public void ParseProcesses_ParsesAWellFormedStepArrayInArrayOrder()
        {
            const string json =
                "[{\"role\":\"fabrication\",\"fabrication_method_id\":5,\"group_index\":0}," +
                "{\"role\":\"inspection\",\"inspection_method_id\":9,\"group_index\":1}]";

            bool unreadable;
            var steps = SwCustomPropertyService.ParseProcesses(json, out unreadable);

            Assert.IsFalse(unreadable);
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepRole.Fabrication, steps[0].Role);
            Assert.AreEqual(5, steps[0].FabricationMethodId);
            Assert.AreEqual(StepRole.Inspection, steps[1].Role);
            Assert.AreEqual(9, steps[1].InspectionMethodId);
        }

        [TestMethod]
        public void ParseProcesses_GarbledJsonYieldsAnEmptyListMarkedUnreadableRatherThanThrowing()
        {
            bool unreadable;
            var steps = SwCustomPropertyService.ParseProcesses("{not valid json", out unreadable);

            Assert.AreEqual(0, steps.Count);
            Assert.IsTrue(unreadable);
        }

        [TestMethod]
        [DataRow("123", 123)]
        [DataRow(" 123 ", 123)]
        [DataRow("-5", -5)]
        public void ParseIntOrNull_ParsesAValidInteger(string value, int expected)
        {
            Assert.AreEqual(expected, SwCustomPropertyService.ParseIntOrNull(value));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("abc")]
        [DataRow("12.5")]
        public void ParseIntOrNull_ReturnsNullForAnythingUnparseable(string value)
        {
            Assert.IsNull(SwCustomPropertyService.ParseIntOrNull(value));
        }

        [TestMethod]
        public void ParseIntList_SplitsTrimsAndSkipsUnparseableEntries()
        {
            Assert.AreSequenceEqual(new List<int> { 1, 2, 3 }, SwCustomPropertyService.ParseIntList("1, 2 ,3"));
            Assert.AreSequenceEqual(new List<int> { 1, 3 }, SwCustomPropertyService.ParseIntList("1,abc,3"));
            Assert.AreSequenceEqual(new List<int> { 1, 2 }, SwCustomPropertyService.ParseIntList("1,2,"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void ParseIntList_BlankOrMissingYieldsAnEmptyList(string value)
        {
            Assert.AreEqual(0, SwCustomPropertyService.ParseIntList(value).Count);
        }

        [TestMethod]
        public void ParseLabelList_SplitsTrimsAndSkipsEmptyEntries()
        {
            Assert.AreSequenceEqual(new List<string> { "a", "b", "c" }, SwCustomPropertyService.ParseLabelList("a; b ;c"));
            Assert.AreSequenceEqual(new List<string> { "a", "c" }, SwCustomPropertyService.ParseLabelList("a;;c"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void ParseLabelList_BlankOrMissingYieldsAnEmptyList(string value)
        {
            Assert.AreEqual(0, SwCustomPropertyService.ParseLabelList(value).Count);
        }
    }
}

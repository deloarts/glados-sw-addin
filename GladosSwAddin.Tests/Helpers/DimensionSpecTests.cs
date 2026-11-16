using System.Globalization;
using System.Threading;
using GladosSwAddin.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Helpers
{
    /// <summary>
    /// DimensionSpec mirrors the backend's dimension_spec.py, which is the authoritative
    /// canonicalisation; these cases pin the mirror to that behaviour (including that only a
    /// Block's numbers are reordered during canonicalisation).
    /// </summary>
    [TestClass]
    public class DimensionSpecTests
    {
        [TestMethod]
        public void ComposeBlock_SortsAllThreeNumbersDescending()
        {
            Assert.AreEqual("200 × 100 × 20", DimensionSpec.ComposeBlock(100, 20, 200));
        }

        [TestMethod]
        public void ComposeProfile_SortsOnlyWidthAndHeight()
        {
            Assert.AreEqual("60 × 40 × 6000 × 3", DimensionSpec.ComposeProfile(40, 60, 6000, 3));
        }

        [TestMethod]
        public void ComposeTurnedPartAndTube_NeverReorder()
        {
            Assert.AreEqual("Ø20 × 100", DimensionSpec.ComposeTurnedPart(20, 100));
            Assert.AreEqual("Ø20 × 100 × 2.5", DimensionSpec.ComposeTube(20, 100, 2.5));
        }

        [TestMethod]
        public void FormatNumber_IsInvariantWithoutTrailingZeros()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-AT");
                Assert.AreEqual("12.5", DimensionSpec.FormatNumber(12.5));
                Assert.AreEqual("100", DimensionSpec.FormatNumber(100.0));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void TryParseNumber_AcceptsCurrentCultureAndInvariantDecimals()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-AT");
                double value;
                Assert.IsTrue(DimensionSpec.TryParseNumber(" 12,5 ", out value));
                Assert.AreEqual(12.5, value);
                Assert.IsFalse(DimensionSpec.TryParseNumber("   ", out value));
                Assert.IsFalse(DimensionSpec.TryParseNumber(null, out value));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestMethod]
        [DataRow("100 x 200 x 20", "200 × 100 × 20")]
        [DataRow("100*200*20.0", "200 × 100 × 20")]
        [DataRow("ø20.50 x 100", "Ø20.5 × 100")]
        [DataRow("Ø20 × 100 × 2", "Ø20 × 100 × 2")]
        [DataRow("40 x 60 x 6000 x 3", "40 × 60 × 6000 × 3")]
        [DataRow("  some   custom\ttext ", "some custom text")]
        public void Canonicalize_MatchesBackendMirror(string input, string expected)
        {
            Assert.AreEqual(expected, DimensionSpec.Canonicalize(input));
        }

        [TestMethod]
        public void Canonicalize_NullAndBlankBecomeNull()
        {
            Assert.IsNull(DimensionSpec.Canonicalize(null));
            Assert.IsNull(DimensionSpec.Canonicalize("   "));
        }

        [TestMethod]
        public void AreEquivalent_TreatsBlockPermutationsAsDuplicates()
        {
            Assert.IsTrue(DimensionSpec.AreEquivalent("100 x 200 x 20", "20 × 200 × 100"));
            Assert.IsFalse(DimensionSpec.AreEquivalent("Ø20 × 100", "Ø100 × 20"));
        }

        [TestMethod]
        public void TryParse_RecognisesStructuredShapesOnly()
        {
            DimensionShape shape;
            double[] numbers;

            Assert.IsTrue(DimensionSpec.TryParse("Ø20 × 100 × 2", out shape, out numbers));
            Assert.AreEqual(DimensionShape.Tube, shape);
            CollectionAssert.AreEqual(new[] { 20.0, 100.0, 2.0 }, numbers);

            Assert.IsFalse(DimensionSpec.TryParse("M8 thread", out shape, out numbers));
            Assert.IsNull(numbers);
        }
    }
}

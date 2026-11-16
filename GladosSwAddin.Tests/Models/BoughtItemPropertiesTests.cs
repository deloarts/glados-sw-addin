using GladosSwAddin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Models
{
    /// <summary>
    /// Pins BoughtItemProperties.PropertyNames.IsPdmOwned — see MadeItemPropertiesTests for why
    /// this is a separate test class from the made-item one despite the identical-looking logic.
    /// MassFallbackLegacy is a compile-time constant, so a test asserting its value would just
    /// compare the same inlined literal against itself and is intentionally left out.
    /// </summary>
    [TestClass]
    public class BoughtItemPropertiesTests
    {
        [TestMethod]
        [DataRow("PDM.PartNumber", true)]
        [DataRow("PDM.Revision", true)]
        [DataRow("PDM.", true)]
        [DataRow("Glados.ArticleName", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        [DataRow("pdm.PartNumber", false)]
        public void IsPdmOwned_MatchesOnlyTheExactCaseSensitivePrefix(string propertyName, bool expected)
        {
            Assert.AreEqual(expected, BoughtItemProperties.PropertyNames.IsPdmOwned(propertyName));
        }
    }
}

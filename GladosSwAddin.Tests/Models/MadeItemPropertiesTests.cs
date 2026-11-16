using GladosSwAddin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Models
{
    /// <summary>
    /// Pins MadeItemProperties.PropertyNames.IsPdmOwned — the guard
    /// SwCustomPropertyService.GuardNotPdmOwned relies on to refuse writing a PDM.* property for
    /// a made item. Kept as its own test class, separate from BoughtItemPropertiesTests, because
    /// the two IsPdmOwned implementations are independent copies (see CLAUDE.md's "never let a
    /// made-tab change regress the bought tab" rule) and could drift from each other silently.
    /// </summary>
    [TestClass]
    public class MadeItemPropertiesTests
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
            Assert.AreEqual(expected, MadeItemProperties.PropertyNames.IsPdmOwned(propertyName));
        }
    }
}

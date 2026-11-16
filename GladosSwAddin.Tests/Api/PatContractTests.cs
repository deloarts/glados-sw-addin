using System.Globalization;
using System.Threading;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Api
{
    /// <summary>
    /// Pins PatContract's comparison of the backend's pat_contract with the supported one and
    /// the warning decision built on it: only a readable, different contract warns, so a missing
    /// or malformed answer never shows a false warning.
    /// </summary>
    [TestClass]
    public class PatContractTests
    {
        [TestMethod]
        public void Check_SameContractIsSupported()
        {
            Assert.AreEqual(PatContractStatus.Supported, PatContract.Check(PatContract.Supported));
        }

        [TestMethod]
        public void Check_ComparesNumbersNotText()
        {
            Assert.AreEqual(PatContractStatus.Supported, PatContract.Check(" 00.1 "));
        }

        [TestMethod]
        [DataRow("0.2")]
        [DataRow("1.0")]
        [DataRow("0.10")]
        [DataRow("1.1")]
        public void Check_OtherMinorOrMajorIsUnsupported(string serverContract)
        {
            Assert.AreEqual(PatContractStatus.Unsupported, PatContract.Check(serverContract));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("0")]
        [DataRow("0.1.0")]
        [DataRow("v0.1")]
        [DataRow("0.-1")]
        [DataRow("abc")]
        public void Check_UnreadableContractIsUnknown(string serverContract)
        {
            Assert.AreEqual(PatContractStatus.Unknown, PatContract.Check(serverContract));
        }

        [TestMethod]
        public void Check_MissingContractIsUnknown()
        {
            Assert.AreEqual(PatContractStatus.Unknown, PatContract.Check(null));
        }

        [TestMethod]
        public void WarningFor_SupportedContractGivesNoWarning()
        {
            var response = new PatVersionResponse { Version = "0.1.7", PatContract = PatContract.Supported };

            Assert.IsNull(PatContract.WarningFor(response, "0.1.0"));
        }

        [TestMethod]
        public void WarningFor_MissingResponseGivesNoWarning()
        {
            Assert.IsNull(PatContract.WarningFor(null, "0.1.0"));
        }

        [TestMethod]
        public void WarningFor_UnreadableContractGivesNoWarning()
        {
            var response = new PatVersionResponse { Version = "0.2.0", PatContract = null };

            Assert.IsNull(PatContract.WarningFor(response, "0.1.0"));
        }

        [TestMethod]
        public void WarningFor_OtherContractNamesBothVersionsAndBothContracts()
        {
            var previous = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            try
            {
                var response = new PatVersionResponse { Version = "0.2.3", PatContract = "0.2" };

                var warning = PatContract.WarningFor(response, "0.1.0");

                Assert.IsNotNull(warning);
                StringAssert.Contains(warning, "GLADOS 0.2.3");
                StringAssert.Contains(warning, "API contract 0.2,");
                StringAssert.Contains(warning, "this add-in (0.1.0)");
                StringAssert.Contains(warning, "supports API contract " + PatContract.Supported + ".");
                StringAssert.Contains(warning, "Update the add-in.");
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = previous;
            }
        }

        [TestMethod]
        public void WarningFor_MissingVersionsAreShownAsQuestionMarks()
        {
            var previous = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            try
            {
                var response = new PatVersionResponse { Version = null, PatContract = "1.0" };

                var warning = PatContract.WarningFor(response, string.Empty);

                StringAssert.Contains(warning, "GLADOS ? serves");
                StringAssert.Contains(warning, "this add-in (?)");
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = previous;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using GladosSwAddin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Services
{
    /// <summary>
    /// Covers ImportedComponentsService's pure helpers: turning a document title into a file
    /// and component name, keeping work-folder file names unique, reading a component name back
    /// to the part a rename sets, the work-folder membership test, and the age rule for purging
    /// old work folders — everything reachable without a live SolidWorks session.
    /// </summary>
    [TestClass]
    public class ImportedComponentsServiceTests
    {
        [TestMethod]
        public void ToComponentName_KeepsAPlainTitle()
        {
            Assert.AreEqual(
                "170922 DFM-25-20-P-A-KF---(Z)",
                ImportedComponentsService.ToComponentName("170922 DFM-25-20-P-A-KF---(Z)"));
        }

        [TestMethod]
        public void ToComponentName_DropsASolidWorksExtensionOnly()
        {
            Assert.AreEqual("8210498 ZBH-9", ImportedComponentsService.ToComponentName("8210498 ZBH-9.SLDPRT"));
            Assert.AreEqual("wrapper", ImportedComponentsService.ToComponentName("wrapper.sldasm"));
            Assert.AreEqual("170922_DFM.stp", ImportedComponentsService.ToComponentName("170922_DFM.stp"));
        }

        [TestMethod]
        public void ToComponentName_DropsTheVirtualParentSuffix()
        {
            Assert.AreEqual(
                "8210498 ZBH-9_1",
                ImportedComponentsService.ToComponentName("8210498 ZBH-9_1^170927_DFM-25-80-P-A-KF_I-26-00063"));
            Assert.AreEqual("Component", ImportedComponentsService.ToComponentName("^parent"));
        }

        [TestMethod]
        public void ToComponentName_ReplacesCharactersWindowsOrSolidWorksReject()
        {
            Assert.AreEqual("a_b_c_d", ImportedComponentsService.ToComponentName("a/b:c@d"));
        }

        [TestMethod]
        public void ToComponentName_FallsBackForAnEmptyTitle()
        {
            Assert.AreEqual("Component", ImportedComponentsService.ToComponentName(null));
            Assert.AreEqual("Component", ImportedComponentsService.ToComponentName("  .SLDPRT"));
        }

        [TestMethod]
        public void UniqueFileName_NumbersOnlyWhileTaken()
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pin.SLDPRT", "pin (2).SLDPRT" };

            Assert.AreEqual("pin (3).SLDPRT", ImportedComponentsService.UniqueFileName("pin", ".SLDPRT", taken.Contains));
            Assert.AreEqual("rod.SLDPRT", ImportedComponentsService.UniqueFileName("rod", ".SLDPRT", taken.Contains));
        }

        [TestMethod]
        public void ComponentBaseName_StripsInstanceAndVirtualSuffix()
        {
            Assert.AreEqual("8210498 ZBH-9", ImportedComponentsService.ComponentBaseName("8210498 ZBH-9-3"));
            Assert.AreEqual(
                "Copy of 8210498 ZBH-9",
                ImportedComponentsService.ComponentBaseName("Copy of 8210498 ZBH-9^I-26-00057-1"));
            Assert.AreEqual("8210498 ZBH-9", ImportedComponentsService.ComponentBaseName("8210498 ZBH-9^I-26-00057-2"));
        }

        [TestMethod]
        public void ComponentBaseName_HandlesBlank()
        {
            Assert.AreEqual(string.Empty, ImportedComponentsService.ComponentBaseName(null));
        }

        [TestMethod]
        public void IsInFolder_MatchesBelowTheFolderOnly()
        {
            const string folder = @"C:\Temp\GladosSwAddin\ImportedComponents\abc";

            Assert.IsTrue(ImportedComponentsService.IsInFolder(@"c:\temp\gladosswaddin\importedcomponents\ABC\pin.SLDPRT", folder));
            Assert.IsTrue(ImportedComponentsService.IsInFolder("C:/Temp/GladosSwAddin/ImportedComponents/abc/x.SLDPRT", folder + @"\"));
            Assert.IsFalse(ImportedComponentsService.IsInFolder(@"C:\Temp\GladosSwAddin\ImportedComponents\abcd\pin.SLDPRT", folder));
            Assert.IsFalse(ImportedComponentsService.IsInFolder(string.Empty, folder));
            Assert.IsFalse(ImportedComponentsService.IsInFolder(@"C:\x.SLDPRT", null));
        }

        [TestMethod]
        public void IsStaleWorkFolder_AfterOneDay()
        {
            var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

            Assert.IsFalse(ImportedComponentsService.IsStaleWorkFolder(now.AddHours(-23), now));
            Assert.IsTrue(ImportedComponentsService.IsStaleWorkFolder(now.AddHours(-25), now));
        }
    }
}

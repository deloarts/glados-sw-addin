using System.Linq;
using GladosSwAddin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Services
{
    /// <summary>
    /// Covers DownloadsReferenceService's pure helpers: reading the full paths out of
    /// GetDependencies2's name/path pairs, and deciding whether a path lies in the current
    /// user's (possibly redirected) Downloads folder or another profile's — everything
    /// reachable without a live ModelDoc2 or the Windows shell.
    /// </summary>
    [TestClass]
    public class DownloadsReferenceServiceTests
    {
        private const string Downloads = @"C:\Users\alice\Downloads";
        private const string Profiles = @"C:\Users";

        [TestMethod]
        public void ParseDependencyPaths_TakesTheOddEntries()
        {
            var paths = DownloadsReferenceService.ParseDependencyPaths(new[]
            {
                "bracket.STEP", @"C:\Users\alice\Downloads\bracket.STEP",
                "base.SLDPRT", @"\\vault\parts\base.SLDPRT",
            });

            CollectionAssert.AreEqual(
                new[] { @"C:\Users\alice\Downloads\bracket.STEP", @"\\vault\parts\base.SLDPRT" },
                paths.ToArray());
        }

        [TestMethod]
        public void ParseDependencyPaths_DropsBlanksAndCaseInsensitiveRepeats()
        {
            var paths = DownloadsReferenceService.ParseDependencyPaths(new[]
            {
                "a.step", @"C:\x\a.step",
                "b.step", "",
                "a.STEP", @"C:\X\A.STEP",
            });

            CollectionAssert.AreEqual(new[] { @"C:\x\a.step" }, paths.ToArray());
        }

        [TestMethod]
        public void ParseDependencyPaths_NullOrWrongShape_YieldsNone()
        {
            Assert.IsEmpty(DownloadsReferenceService.ParseDependencyPaths(null));
            Assert.IsEmpty(DownloadsReferenceService.ParseDependencyPaths(42));
            Assert.IsEmpty(DownloadsReferenceService.ParseDependencyPaths(new[] { "only-a-name.step" }));
        }

        [TestMethod]
        [DataRow(@"C:\Users\alice\Downloads\bracket.STEP")]
        [DataRow(@"c:\users\ALICE\downloads\sub\bracket.step")]
        [DataRow("C:/Users/alice/Downloads/bracket.step")]
        public void IsInDownloadsFolder_CurrentUsersDownloads(string path)
        {
            Assert.IsTrue(DownloadsReferenceService.IsInDownloadsFolder(path, Downloads, Profiles));
        }

        [TestMethod]
        public void IsInDownloadsFolder_RedirectedDownloads()
        {
            Assert.IsTrue(DownloadsReferenceService.IsInDownloadsFolder(
                @"D:\Dl\bracket.step", @"D:\Dl\", Profiles));
        }

        [TestMethod]
        public void IsInDownloadsFolder_AnotherProfilesDownloads()
        {
            Assert.IsTrue(DownloadsReferenceService.IsInDownloadsFolder(
                @"C:\Users\bob\Downloads\bracket.step", Downloads, Profiles));
        }

        [TestMethod]
        [DataRow(@"C:\Users\alice\Documents\bracket.step")]
        [DataRow(@"C:\Users\alice\Downloads")]
        [DataRow(@"C:\Users\alice\DownloadsOld\bracket.step")]
        [DataRow(@"C:\Users\Downloads\bracket.step")]
        [DataRow(@"C:\Users\bob\Projects\Downloads\bracket.step")]
        [DataRow(@"\\vault\Downloads\bracket.step")]
        [DataRow("")]
        [DataRow(null)]
        public void IsInDownloadsFolder_Elsewhere(string path)
        {
            Assert.IsFalse(DownloadsReferenceService.IsInDownloadsFolder(path, Downloads, Profiles));
        }

        [TestMethod]
        public void IsInDownloadsFolder_UnresolvedFolders_SkipThatRule()
        {
            Assert.IsTrue(DownloadsReferenceService.IsInDownloadsFolder(
                @"C:\Users\bob\Downloads\a.step", null, Profiles));
            Assert.IsTrue(DownloadsReferenceService.IsInDownloadsFolder(
                @"D:\Dl\a.step", @"D:\Dl", null));
            Assert.IsFalse(DownloadsReferenceService.IsInDownloadsFolder(
                @"C:\Users\bob\Downloads\a.step", null, null));
        }
    }
}

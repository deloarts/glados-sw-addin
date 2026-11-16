using System;
using System.IO;
using System.Linq;
using GladosSwAddin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Services
{
    /// <summary>
    /// Log's file sink, run against a private temp folder: what reaches the file (Info and up,
    /// with the exception), what must not (Debug, which may carry property values), and the
    /// retention of the newest daily files.
    /// </summary>
    [TestClass]
    public class LogTests
    {
        private string _directory;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(TestAssemblySetup.LogDirectory, "log-tests-" + Guid.NewGuid().ToString("N"));
            Log.DirectoryOverride = _directory;
        }

        [TestCleanup]
        public void Cleanup()
        {
            Log.DirectoryOverride = TestAssemblySetup.LogDirectory;
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private string ReadTodaysLog()
        {
            var files = Directory.GetFiles(_directory, "glados-*.log");
            Assert.HasCount(1, files);
            return File.ReadAllText(files[0]);
        }

        [TestMethod]
        public void InfoAndAboveReachTheFileWithTheirException()
        {
            Log.Info("info line");
            Log.Warning("warning line", new InvalidOperationException("boom"));
            Log.Error("error line");

            var text = ReadTodaysLog();
            StringAssert.Contains(text, "INFO    info line");
            StringAssert.Contains(text, "WARNING warning line");
            StringAssert.Contains(text, "System.InvalidOperationException: boom");
            StringAssert.Contains(text, "ERROR   error line");
        }

        [TestMethod]
        public void DebugNeverReachesTheFile()
        {
            Log.Debug("debug secret-ish value");
            Log.Info("marker");

            var text = ReadTodaysLog();
            Assert.DoesNotContain("secret-ish", text);
        }

        [TestMethod]
        public void KeepsOnlyTheNewestDailyFiles()
        {
            Directory.CreateDirectory(_directory);
            for (var day = 1; day <= 20; day++)
                File.WriteAllText(Path.Combine(_directory, string.Format("glados-202001{0:00}.log", day)), "old");

            Log.Info("today");

            var remaining = Directory.GetFiles(_directory, "glados-*.log")
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            Assert.HasCount(Log.RetainedDays, remaining);
            Assert.AreEqual("glados-20200108.log", remaining[0]);
        }
    }
}

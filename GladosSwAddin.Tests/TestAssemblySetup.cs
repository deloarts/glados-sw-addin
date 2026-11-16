using System;
using System.IO;
using GladosSwAddin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests
{
    /// <summary>
    /// Assembly-wide setup: code under test logs (e.g. a malformed Glados.Processes value is a
    /// logged warning), and without a redirect those lines would land in the developer's real
    /// %LOCALAPPDATA%\GladosSwAddin\logs. Every test run gets its own temp log folder instead.
    /// </summary>
    [TestClass]
    public static class TestAssemblySetup
    {
        public static string LogDirectory { get; private set; }

        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            LogDirectory = Path.Combine(Path.GetTempPath(), "GladosSwAddin.Tests", Guid.NewGuid().ToString("N"));
            Log.DirectoryOverride = LogDirectory;
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            Log.DirectoryOverride = null;
            if (Directory.Exists(LogDirectory))
                Directory.Delete(LogDirectory, true);
        }
    }
}

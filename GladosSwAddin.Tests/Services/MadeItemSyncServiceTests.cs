using System.Collections.Generic;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;
using GladosSwAddin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Services
{
    /// <summary>
    /// MadeItemSyncService.SameRouting is the pure comparison behind the Assembly tab's
    /// unsaved-routing marker: it must agree with what Save would persist (group-normalized,
    /// compared by the divergence signature) and ignore the MethodName display label.
    /// </summary>
    [TestClass]
    public class MadeItemSyncServiceTests
    {
        private static ProcessStep Fab(int methodId, int group, string methodName = null, string note = null)
        {
            return new ProcessStep
            {
                Role = StepRole.Fabrication,
                FabricationMethodId = methodId,
                GroupIndex = group,
                MethodName = methodName,
                Note = note,
            };
        }

        [TestMethod]
        public void SameRouting_EqualRoutingsMatch()
        {
            var a = new List<ProcessStep> { Fab(1, 0, note: "x"), Fab(2, 1) };
            var b = new List<ProcessStep> { Fab(1, 0, note: "x"), Fab(2, 1) };

            Assert.IsTrue(MadeItemSyncService.SameRouting(a, b));
        }

        [TestMethod]
        public void SameRouting_DifferentMethodDiffers()
        {
            var a = new List<ProcessStep> { Fab(1, 0) };
            var b = new List<ProcessStep> { Fab(2, 0) };

            Assert.IsFalse(MadeItemSyncService.SameRouting(a, b));
        }

        [TestMethod]
        public void SameRouting_GroupIndexesCompareAfterNormalisation()
        {
            var a = new List<ProcessStep> { Fab(1, 0), Fab(2, 2) };
            var b = new List<ProcessStep> { Fab(1, 0), Fab(2, 1) };

            Assert.IsTrue(MadeItemSyncService.SameRouting(a, b));
        }

        [TestMethod]
        public void SameRouting_DifferentGroupingDiffers()
        {
            var a = new List<ProcessStep> { Fab(1, 0), Fab(2, 0) };
            var b = new List<ProcessStep> { Fab(1, 0), Fab(2, 1) };

            Assert.IsFalse(MadeItemSyncService.SameRouting(a, b));
        }

        [TestMethod]
        public void SameRouting_IgnoresMethodName()
        {
            var a = new List<ProcessStep> { Fab(1, 0, methodName: "Sawing") };
            var b = new List<ProcessStep> { Fab(1, 0, methodName: "Saegen") };

            Assert.IsTrue(MadeItemSyncService.SameRouting(a, b));
        }

        [TestMethod]
        public void SameRouting_NullEqualsEmpty()
        {
            Assert.IsTrue(MadeItemSyncService.SameRouting(null, new List<ProcessStep>()));
            Assert.IsTrue(MadeItemSyncService.SameRouting(null, null));
            Assert.IsFalse(MadeItemSyncService.SameRouting(new List<ProcessStep> { Fab(1, 0) }, null));
        }
    }
}

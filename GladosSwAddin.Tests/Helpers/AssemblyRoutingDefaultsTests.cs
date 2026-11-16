using System.Collections.Generic;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Helpers;
using GladosSwAddin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Helpers
{
    /// <summary>
    /// AssemblyRoutingDefaults picks the backend-flagged default assembly method out of the
    /// lookup snapshot; these cases pin that an archived or unflagged method is never picked
    /// and that the seeded step is a plain fabrication step in group 0.
    /// </summary>
    [TestClass]
    public class AssemblyRoutingDefaultsTests
    {
        private static FabricationMethodResponse Method(
            int id, string name, bool isDefault = false, bool archived = false)
        {
            return new FabricationMethodResponse
            {
                Id = id,
                Name = name,
                DefaultForAssemblies = isDefault,
                Archived = archived,
            };
        }

        [TestMethod]
        public void FindDefaultMethod_ReturnsFlaggedNonArchivedMethod()
        {
            var methods = new List<FabricationMethodResponse>
            {
                Method(1, "Turning"),
                Method(2, "Assembly", isDefault: true),
            };

            Assert.AreEqual(2, AssemblyRoutingDefaults.FindDefaultMethod(methods).Id);
        }

        [TestMethod]
        public void FindDefaultMethod_ReturnsNullWhenNoneFlagged()
        {
            var methods = new List<FabricationMethodResponse> { Method(1, "Turning"), Method(2, "Milling") };

            Assert.IsNull(AssemblyRoutingDefaults.FindDefaultMethod(methods));
        }

        [TestMethod]
        public void FindDefaultMethod_IgnoresArchivedFlaggedMethod()
        {
            var methods = new List<FabricationMethodResponse> { Method(2, "Assembly", isDefault: true, archived: true) };

            Assert.IsNull(AssemblyRoutingDefaults.FindDefaultMethod(methods));
        }

        [TestMethod]
        public void FindDefaultMethod_ReturnsNullForNullInput()
        {
            Assert.IsNull(AssemblyRoutingDefaults.FindDefaultMethod(null));
        }

        [TestMethod]
        public void CreateDefaultStep_BuildsFabricationStepInGroupZero()
        {
            var methods = new List<FabricationMethodResponse> { Method(7, "Assembly", isDefault: true) };

            var step = AssemblyRoutingDefaults.CreateDefaultStep(methods);

            Assert.IsNotNull(step);
            Assert.AreEqual(StepRole.Fabrication, step.Role);
            Assert.AreEqual(7, step.FabricationMethodId);
            Assert.IsNull(step.InspectionMethodId);
            Assert.AreEqual(0, step.GroupIndex);
            Assert.AreEqual("Assembly", step.MethodName);
        }

        [TestMethod]
        public void ShouldSeed_TrueForAFileThatNeverStoredARouting()
        {
            Assert.IsTrue(AssemblyRoutingDefaults.ShouldSeed(new MadeItemProperties()));
        }

        [TestMethod]
        public void ShouldSeed_FalseForAnExplicitlySavedEmptyRouting()
        {
            var local = new MadeItemProperties { ProcessesStored = true };

            Assert.IsFalse(AssemblyRoutingDefaults.ShouldSeed(local));
        }

        [TestMethod]
        public void ShouldSeed_FalseForAnUnreadableRouting()
        {
            var local = new MadeItemProperties { ProcessesUnreadable = true, ProcessesStored = true };

            Assert.IsFalse(AssemblyRoutingDefaults.ShouldSeed(local));
        }

        [TestMethod]
        public void ShouldSeed_FalseWhenTheRoutingHasSteps()
        {
            var local = new MadeItemProperties
            {
                Processes = new List<ProcessStep> { new ProcessStep { FabricationMethodId = 1 } },
            };

            Assert.IsFalse(AssemblyRoutingDefaults.ShouldSeed(local));
        }

        [TestMethod]
        public void ShouldSeed_FalseForNull()
        {
            Assert.IsFalse(AssemblyRoutingDefaults.ShouldSeed(null));
        }

        [TestMethod]
        public void CreateDefaultStep_ReturnsNullWhenNoDefault()
        {
            var methods = new List<FabricationMethodResponse> { Method(1, "Turning") };

            Assert.IsNull(AssemblyRoutingDefaults.CreateDefaultStep(methods));
        }
    }
}

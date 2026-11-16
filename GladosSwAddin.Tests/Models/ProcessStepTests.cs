using GladosSwAddin.Api;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GladosSwAddin.Tests.Models
{
    /// <summary>
    /// Pins ProcessStep's Glados.Processes wire shape (ids and position only, snake_case) —
    /// MethodName is a runtime-only display label ([JsonIgnore]) and must never round-trip onto
    /// the property, per ProcessStep's own doc comment.
    /// </summary>
    [TestClass]
    public class ProcessStepTests
    {
        [TestMethod]
        public void Serialize_WritesIdsOnlyInSnakeCaseAndNeverTheMethodName()
        {
            var step = new ProcessStep
            {
                Role = StepRole.Fabrication,
                FabricationMethodId = 5,
                InspectionMethodId = null,
                GroupIndex = 2,
                EstimatedSetupMinutes = 10,
                EstimatedMinutesPerPiece = 1.5,
                Note = "clamp carefully",
                MethodName = "Turning",
            };

            var json = JsonConvert.SerializeObject(step, GladosApiClient.JsonSettings);
            var obj = JObject.Parse(json);

            Assert.AreEqual("fabrication", (string)obj["role"]);
            Assert.AreEqual(5, (int)obj["fabrication_method_id"]);
            Assert.AreEqual(2, (int)obj["group_index"]);
            Assert.AreEqual(10, (int)obj["estimated_setup_minutes"]);
            Assert.AreEqual(1.5, (double)obj["estimated_minutes_per_piece"]);
            Assert.AreEqual("clamp carefully", (string)obj["note"]);

            Assert.IsNull(obj["inspection_method_id"]); // null -> NullValueHandling.Ignore
            Assert.IsNull(obj["method_name"]);
            Assert.IsNull(obj["MethodName"]);
            Assert.IsNull(obj["method_id"]);
        }

        [TestMethod]
        public void Deserialize_LeavesMethodNameNullAndDerivesMethodIdFromRole()
        {
            const string json = "{\"role\":\"inspection\",\"inspection_method_id\":9,\"group_index\":0}";

            var step = JsonConvert.DeserializeObject<ProcessStep>(json, GladosApiClient.JsonSettings);

            Assert.AreEqual(StepRole.Inspection, step.Role);
            Assert.AreEqual(9, step.InspectionMethodId);
            Assert.IsNull(step.FabricationMethodId);
            Assert.IsNull(step.MethodName);
            Assert.AreEqual(9, step.MethodId);
        }
    }
}

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using GladosSwAddin.Api;
using GladosSwAddin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GladosSwAddin.Tests.Api
{
    /// <summary>
    /// Pins GladosApiClient.JsonSettings' wire conventions (snake_case naming, nulls ignored,
    /// tolerant enum parsing) and ExtractErrorDetail's two FastAPI error-body shapes — a domain
    /// error's {detail, code} and a 422 validation error's {detail: [...]} — see
    /// GladosApiClient's own XML-doc for both. ExtractErrorDetail and its ErrorDetail return
    /// type are internal purely so these tests can reach them; their behaviour is unchanged.
    /// </summary>
    [TestClass]
    public class GladosApiClientTests
    {
        private class SamplePayload
        {
            public string PartNumber { get; set; }

            public string Revision { get; set; }
        }

        [TestMethod]
        public void JsonSettings_SerializesPropertyNamesAsSnakeCase()
        {
            var json = JsonConvert.SerializeObject(new SamplePayload { PartNumber = "PN-1" }, GladosApiClient.JsonSettings);

            var obj = JObject.Parse(json);
            Assert.AreEqual("PN-1", (string)obj["part_number"]);
        }

        [TestMethod]
        public void JsonSettings_OmitsNullProperties()
        {
            var json = JsonConvert.SerializeObject(
                new SamplePayload { PartNumber = "PN-1", Revision = null }, GladosApiClient.JsonSettings);

            var obj = JObject.Parse(json);
            Assert.IsNull(obj["revision"]);
        }

        [TestMethod]
        public void JsonSettings_DeserializesAKnownEnumMemberValue()
        {
            var owner = JsonConvert.DeserializeObject<FieldOwner>("\"pdm\"", GladosApiClient.JsonSettings);

            Assert.AreEqual(FieldOwner.Pdm, owner);
        }

        [TestMethod]
        public void JsonSettings_UnknownEnumValueFallsBackToMemberZeroInsteadOfThrowing()
        {
            var owner = JsonConvert.DeserializeObject<FieldOwner>("\"some_future_owner\"", GladosApiClient.JsonSettings);

            // FieldOwner.Unknown is deliberately member 0 — see FieldOwner's own doc comment.
            Assert.AreEqual(FieldOwner.Unknown, owner);
        }

        [TestMethod]
        public void JsonSettings_UnknownEnumValueOnANullableFieldBecomesNullInsteadOfMemberZero()
        {
            var owner = JsonConvert.DeserializeObject<FieldOwner?>("\"some_future_owner\"", GladosApiClient.JsonSettings);

            Assert.IsNull(owner);
        }

        [TestMethod]
        public void ExtractErrorDetail_ParsesADomainErrorBody()
        {
            var detail = GladosApiClient.ExtractErrorDetail(
                "{\"detail\":\"Bought item not found.\",\"code\":\"BoughtItemNotFoundError\"}",
                HttpStatusCode.NotFound);

            Assert.AreEqual("Bought item not found.", detail.Message);
            Assert.AreEqual("BoughtItemNotFoundError", detail.Code);
        }

        [TestMethod]
        public void ExtractErrorDetail_FormatsA422ValidationErrorListDroppingTheBodyOrQueryLocPrefix()
        {
            const string body =
                "{\"detail\":[" +
                "{\"loc\":[\"body\",\"items\",0,\"quantity\"],\"msg\":\"ensure this value is greater than 0\"}," +
                "{\"loc\":[\"query\",\"page\"],\"msg\":\"field required\"}]}";

            // net48's HttpStatusCode has no UnprocessableEntity member (added in later .NET
            // versions); 422 is FastAPI's actual validation-error status regardless.
            var detail = GladosApiClient.ExtractErrorDetail(body, (HttpStatusCode)422);

            var expected = "items.0.quantity: ensure this value is greater than 0"
                + Environment.NewLine + "page: field required";
            Assert.AreEqual(expected, detail.Message);
            Assert.IsNull(detail.Code);
        }

        [TestMethod]
        public void ExtractErrorDetail_FallsBackToRawBodyForNonJsonText()
        {
            const string body = "<html>Bad Gateway</html>";

            var detail = GladosApiClient.ExtractErrorDetail(body, HttpStatusCode.BadGateway);

            Assert.AreEqual(body, detail.Message);
            Assert.IsNull(detail.Code);
        }

        [TestMethod]
        public void ExtractErrorDetail_FallsBackToAGenericMessageForAnEmptyBody()
        {
            var detail = GladosApiClient.ExtractErrorDetail(string.Empty, HttpStatusCode.InternalServerError);

            Assert.AreEqual("Request failed with status 500 (InternalServerError).", detail.Message);
            Assert.IsNull(detail.Code);
        }

        [TestMethod]
        public void IsLostAnswer_ResendsAfterAFailedConnectionOrATimeout()
        {
            Assert.IsTrue(GladosApiClient.IsLostAnswer(new HttpRequestException("connection reset")));
            Assert.IsTrue(GladosApiClient.IsLostAnswer(new TaskCanceledException()));
        }

        [TestMethod]
        public void IsLostAnswer_ResendsAfterAGatewayOrALostRace()
        {
            Assert.IsTrue(GladosApiClient.IsLostAnswer(
                new GladosApiException(HttpStatusCode.BadGateway, "bad gateway", (string)null)));
            Assert.IsTrue(GladosApiClient.IsLostAnswer(
                new GladosApiException(HttpStatusCode.ServiceUnavailable, "lost race", "DatabaseConflictError")));
            Assert.IsTrue(GladosApiClient.IsLostAnswer(
                new GladosApiException(HttpStatusCode.GatewayTimeout, "timeout", (string)null)));
        }

        [TestMethod]
        public void IsLostAnswer_DoesNotResendDuringMaintenanceOrAfterARefusal()
        {
            Assert.IsFalse(GladosApiClient.IsLostAnswer(
                new GladosApiException(HttpStatusCode.ServiceUnavailable, "maintenance", "MaintenanceModeError")));
            Assert.IsFalse(GladosApiClient.IsLostAnswer(
                new GladosApiException(HttpStatusCode.Conflict, "taken", "ConstraintViolationError")));
            Assert.IsFalse(GladosApiClient.IsLostAnswer(new InvalidOperationException()));
        }

        [TestMethod]
        public void ExtractErrorDetail_FallsBackToTheRawBodyWhenDetailIsUnusable()
        {
            // Documented edge case, not a bug (see ExtractErrorDetail's own doc comment): a
            // "code" without a usable "detail" is dropped along with everything else, and the
            // whole raw body becomes the message instead.
            const string body = "{\"code\":\"SomeError\"}";

            var detail = GladosApiClient.ExtractErrorDetail(body, HttpStatusCode.BadRequest);

            Assert.AreEqual(body, detail.Message);
            Assert.IsNull(detail.Code);
        }
    }
}

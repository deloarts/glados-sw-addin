using System.Net;
using GladosSwAddin.Api;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GladosSwAddin.Tests.Api
{
    /// <summary>
    /// Pins which error answers count as a rejected token: only 401 and 403 tell the user to
    /// fix the token, every other status, such as a 503 during maintenance, is a server error.
    /// </summary>
    [TestClass]
    public class GladosApiExceptionTests
    {
        [TestMethod]
        [DataRow(HttpStatusCode.Unauthorized)]
        [DataRow(HttpStatusCode.Forbidden)]
        public void IsTokenRejection_TrueFor401And403(HttpStatusCode statusCode)
        {
            Assert.IsTrue(new GladosApiException(statusCode, "rejected").IsTokenRejection);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        [DataRow(HttpStatusCode.InternalServerError)]
        [DataRow(HttpStatusCode.BadGateway)]
        [DataRow(HttpStatusCode.NotFound)]
        [DataRow((HttpStatusCode)429)]
        public void IsTokenRejection_FalseForEveryOtherErrorAnswer(HttpStatusCode statusCode)
        {
            Assert.IsFalse(new GladosApiException(statusCode, "error").IsTokenRejection);
        }
    }
}

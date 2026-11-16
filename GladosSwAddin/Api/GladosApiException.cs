using System;
using System.Net;

namespace GladosSwAddin.Api
{
    public class GladosApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// The backend domain exception's class name (e.g. "BoughtItemNotFoundError"), echoed
        /// back as the response's "code" field. Null when the server didn't return one — e.g.
        /// FastAPI 422 validation errors, or a non-JSON/unexpected body. Prefer switching on
        /// this over matching against <see cref="Exception.Message"/>: the message is free-form
        /// prose meant for display and can be reworded or localized without notice, while Code
        /// is a stable, machine-readable discriminator.
        /// </summary>
        public string Code { get; }

        /// <summary>True when the server refused the token itself (401 or 403), as opposed to
        /// any other error answer such as a 503 during maintenance, so the caller can tell
        /// the user to fix the token only when the token is the problem.</summary>
        public bool IsTokenRejection =>
            StatusCode == HttpStatusCode.Unauthorized || StatusCode == HttpStatusCode.Forbidden;

        public GladosApiException(HttpStatusCode statusCode, string detail)
            : this(statusCode, detail, (string)null)
        {
        }

        /// <summary>Overload that also carries the backend's machine-readable error code.</summary>
        public GladosApiException(HttpStatusCode statusCode, string detail, string code)
            : base(detail)
        {
            StatusCode = statusCode;
            Code = code;
        }

        public GladosApiException(HttpStatusCode statusCode, string detail, Exception innerException)
            : base(detail, innerException)
        {
            StatusCode = statusCode;
        }
    }
}

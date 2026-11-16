using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace GladosSwAddin.Api
{
    /// <summary>
    /// Low-level HTTP client for the Glados backend's PAT/auth API surface.
    /// Resource-specific services (see Api/Services) build on top of this.
    /// </summary>
    public class GladosApiClient : IDisposable
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private const string IdempotencyKeyHeaderName = "Idempotency-Key";
        private static readonly TimeSpan IdempotentResendDelay = TimeSpan.FromMilliseconds(400);

        private readonly HttpClient _httpClient;

        internal static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            },
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { new TolerantStringEnumConverter() },
        };

        public GladosApiClient(string serverUrl, string personalAccessToken)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("Server URL must be configured.", nameof(serverUrl));

            var baseUri = new Uri(serverUrl.Trim().TrimEnd('/') + "/");

            // TLS 1.2 is pinned on this client's own handler rather than inherited from
            // ServicePointManager.SecurityProtocol: that one is process-wide, and inside
            // SLDWORKS.exe any other add-in can narrow it (e.g. to TLS 1.0), which a server that
            // refuses TLS 1.0/1.1 answers with "Could not create SSL/TLS secure channel". TLS 1.3
            // is left out because Windows 10's Schannel doesn't support it.
            _httpClient = new HttpClient(new HttpClientHandler { SslProtocols = SslProtocols.Tls12 })
            {
                BaseAddress = baseUri,
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(personalAccessToken))
            {
                _httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, personalAccessToken);
            }
        }

        public async Task<T> GetAsync<T>(string relativeUrl)
        {
            using (var response = await _httpClient.GetAsync(relativeUrl).ConfigureAwait(false))
            {
                return await ReadResponseAsync<T>(response).ConfigureAwait(false);
            }
        }

        public Task<T> GetAsync<T>(string relativeUrl, IFilterParams filter)
        {
            var query = filter != null
                ? QueryStringBuilder.Build(filter.ToQueryParameters())
                : string.Empty;

            return GetAsync<T>(relativeUrl + query);
        }

        /// <summary>GET that can carry an If-None-Match request header and surfaces both a 304
        /// response and the ETag response header. Additive alongside the plain GetAsync
        /// overloads above rather than folding conditional-GET support into them, since
        /// LookupsApiService's GET /pat/lookups revalidation (docs/FIELD_OWNERSHIP.md row 11)
        /// is the only caller that needs it. 304 isn't a success status code, so it's handled
        /// before falling into ReadResponseAsync's error path.</summary>
        public async Task<ConditionalGetResult<T>> GetConditionalAsync<T>(string relativeUrl, string ifNoneMatch)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl))
            {
                if (!string.IsNullOrEmpty(ifNoneMatch))
                    request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);

                using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    var etag = response.Headers.ETag?.ToString();

                    if (response.StatusCode == HttpStatusCode.NotModified)
                        return ConditionalGetResult<T>.NotModifiedResult(etag);

                    var value = await ReadResponseAsync<T>(response).ConfigureAwait(false);
                    return ConditionalGetResult<T>.Modified(value, etag);
                }
            }
        }

        public async Task<T> PostAsync<T>(string relativeUrl, object body)
        {
            using (var content = CreateJsonContent(body))
            using (var response = await _httpClient.PostAsync(relativeUrl, content).ConfigureAwait(false))
            {
                return await ReadResponseAsync<T>(response).ConfigureAwait(false);
            }
        }

        /// <summary>POST for a request whose repetition would create something twice, such as
        /// a new catalogue item: it carries a fresh Idempotency-Key, one per call, which stands
        /// for one user action. When the answer is lost on the way (no response, a timeout, or a
        /// gateway or retryable 503 answer, see IsLostAnswer) the request is sent once more with
        /// the same key, so the backend answers from the first request instead of creating a
        /// second item and drawing a second part number. Mirrors the web frontend's
        /// sendIdempotent (frontend/src/core/api/idempotency.ts).</summary>
        public async Task<T> PostIdempotentAsync<T>(string relativeUrl, object body)
        {
            var key = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
            try
            {
                return await SendIdempotentAsync<T>(relativeUrl, body, key).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsLostAnswer(ex))
            {
                await Task.Delay(IdempotentResendDelay).ConfigureAwait(false);
                return await SendIdempotentAsync<T>(relativeUrl, body, key).ConfigureAwait(false);
            }
        }

        private async Task<T> SendIdempotentAsync<T>(string relativeUrl, object body, string key)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl))
            using (var content = CreateJsonContent(body))
            {
                request.Content = content;
                request.Headers.TryAddWithoutValidation(IdempotencyKeyHeaderName, key);
                using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    return await ReadResponseAsync<T>(response).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Whether a failed request may have been booked without its answer
        /// arriving, so sending it again with the same Idempotency-Key is safe and needed: the
        /// connection failed, the request timed out (HttpClient reports that as a
        /// TaskCanceledException), or a gateway or a lost database race answered 502, 503 or
        /// 504. Maintenance mode also answers 503, but books nothing and goes on for longer
        /// than one resend, so it is not a lost answer.</summary>
        internal static bool IsLostAnswer(Exception ex)
        {
            if (ex is HttpRequestException || ex is TaskCanceledException)
                return true;

            var apiError = ex as GladosApiException;
            if (apiError == null || string.Equals(apiError.Code, "MaintenanceModeError", StringComparison.Ordinal))
                return false;

            return apiError.StatusCode == HttpStatusCode.BadGateway
                || apiError.StatusCode == HttpStatusCode.ServiceUnavailable
                || apiError.StatusCode == HttpStatusCode.GatewayTimeout;
        }

        public async Task<T> PutAsync<T>(string relativeUrl, object body)
        {
            using (var content = CreateJsonContent(body))
            using (var response = await _httpClient.PutAsync(relativeUrl, content).ConfigureAwait(false))
            {
                return await ReadResponseAsync<T>(response).ConfigureAwait(false);
            }
        }

        /// <summary>Fetches a raw (non-JSON) binary response, e.g. an image.</summary>
        public async Task<byte[]> GetBytesAsync(string relativeUrl)
        {
            using (var response = await _httpClient.GetAsync(relativeUrl).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var error = ExtractErrorDetail(raw, response.StatusCode);
                    throw new GladosApiException(response.StatusCode, error.Message, error.Code);
                }

                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        private static StringContent CreateJsonContent(object body)
        {
            var json = JsonConvert.SerializeObject(body, JsonSettings);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response)
        {
            var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = ExtractErrorDetail(raw, response.StatusCode);
                throw new GladosApiException(response.StatusCode, error.Message, error.Code);
            }

            if (string.IsNullOrWhiteSpace(raw))
                return default(T);

            try
            {
                return JsonConvert.DeserializeObject<T>(raw, JsonSettings);
            }
            catch (JsonException ex)
            {
                throw new GladosApiException(response.StatusCode, "Failed to parse server response.", ex);
            }
        }

        /// <summary>
        /// Parses an error response body into a display message and, when present, the
        /// backend's machine-readable error code. Handles two shapes the backend produces:
        /// domain errors, whose body is <c>{"detail": "&lt;message&gt;", "code": "&lt;ExceptionClassName&gt;"}</c>
        /// (see exception_handlers.py), and FastAPI's own 422 validation errors, whose body is
        /// <c>{"detail": [{"loc": [...], "msg": "...", "type": "..."}, ...]}</c> — a plain array,
        /// since that path isn't overridden by the backend and carries no "code". Anything else
        /// (a non-JSON body, or JSON without a usable "detail") falls back to the raw body text.
        /// </summary>
        internal static ErrorDetail ExtractErrorDetail(string raw, HttpStatusCode statusCode)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new ErrorDetail($"Request failed with status {(int)statusCode} ({statusCode}).", null);

            try
            {
                var root = JObject.Parse(raw);
                var code = AsString(root["code"]);
                var detailToken = root["detail"];

                if (detailToken != null)
                {
                    if (detailToken.Type == JTokenType.String)
                    {
                        var message = detailToken.Value<string>();
                        if (!string.IsNullOrWhiteSpace(message))
                            return new ErrorDetail(message, code);
                    }
                    else if (detailToken.Type == JTokenType.Array)
                    {
                        var message = FormatValidationErrors((JArray)detailToken);
                        if (!string.IsNullOrWhiteSpace(message))
                            return new ErrorDetail(message, code);
                    }
                }
            }
            catch (JsonException)
            {
                // Body wasn't a JSON object at all — fall through and surface the raw body instead.
            }

            return new ErrorDetail(raw, null);
        }

        /// <summary>
        /// Reads a JToken as a plain string, or null if it's missing or isn't a JSON string
        /// (e.g. a nested object) — avoids JToken's explicit string cast throwing on those.
        /// </summary>
        private static string AsString(JToken token)
        {
            return token != null && token.Type == JTokenType.String ? token.Value<string>() : null;
        }

        /// <summary>
        /// Formats FastAPI's 422 validation error list into one readable line per error, e.g.
        /// "items.0.quantity: ensure this value is greater than 0". The leading "body"/"query"
        /// token in "loc" (which just names the request part the field came from) is dropped
        /// since it adds noise without adding information for the user.
        /// </summary>
        private static string FormatValidationErrors(JArray errors)
        {
            var lines = new List<string>();

            foreach (var token in errors)
            {
                var error = token as JObject;
                if (error == null)
                    continue;

                var msg = AsString(error["msg"]) ?? string.Empty;
                var locArray = error["loc"] as JArray;
                var locParts = locArray != null
                    ? locArray.Select(l => l.ToString()).ToList()
                    : new List<string>();

                if (locParts.Count > 0 && (locParts[0] == "body" || locParts[0] == "query"))
                    locParts.RemoveAt(0);

                var loc = string.Join(".", locParts);
                lines.Add(string.IsNullOrEmpty(loc) ? msg : loc + ": " + msg);
            }

            return string.Join(Environment.NewLine, lines);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>Result of <see cref="ExtractErrorDetail"/>: a display message plus the optional backend error code.</summary>
        internal struct ErrorDetail
        {
            public string Message { get; }
            public string Code { get; }

            public ErrorDetail(string message, string code)
            {
                Message = message;
                Code = code;
            }
        }

        /// <summary>
        /// A <see cref="StringEnumConverter"/> that tolerates enum members it doesn't know
        /// about yet. The backend can add new enum values ahead of an add-in release; the
        /// stock converter throws <see cref="JsonSerializationException"/> on an unrecognized
        /// string and takes the whole response deserialization down with it. Falling back to
        /// the type's default value (or null for a nullable enum) keeps the rest of the
        /// response usable instead of failing the call over one unknown field. Write behaviour
        /// (honoring [EnumMember] values) is inherited unchanged from the base converter.
        /// </summary>
        private class TolerantStringEnumConverter : StringEnumConverter
        {
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                try
                {
                    return base.ReadJson(reader, objectType, existingValue, serializer);
                }
                catch (Exception ex) when (ex is JsonSerializationException || ex is ArgumentException)
                {
                    var underlyingType = Nullable.GetUnderlyingType(objectType);
                    if (underlyingType != null)
                        return null;

                    return Activator.CreateInstance(objectType);
                }
            }
        }
    }
}

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps POST /api/v1/pat/pdm/validate — a read-only dry run that reports whether
    /// the active document's currently mapped custom-property values would be accepted by PDM
    /// ingest, without touching PDM or the catalogue. See README.md's "Validation before
    /// release".</summary>
    public class PdmValidationApiService
    {
        private readonly GladosApiClient _client;

        public PdmValidationApiService(GladosApiClient client)
        {
            _client = client;
        }

        /// <summary>Validates <paramref name="variables"/> (the document's mapped custom
        /// properties, keyed by property name) against the catalogue for <paramref name="kind"/>
        /// (null mirrors an unset Source dropdown). <paramref name="filePath"/> is the document's
        /// full local path (ModelDoc2.GetPathName()), null for a document that has never been
        /// saved — it lets the backend try to resolve a PDM vault identity for the file, reported
        /// back as PdmValidationResponse.FileIdentity; an older backend that doesn't accept the
        /// field simply ignores it. A 409 — the PDM integration is disabled in Glados — is
        /// reported as <see cref="PdmValidationCallResult.IntegrationDisabled"/> rather than an
        /// exception: it is an expected, common outcome for a server that hasn't turned PDM sync
        /// on yet, not a transport failure. Every other non-2xx response (401/403 included)
        /// surfaces as the usual GladosApiException, and a network failure as
        /// HttpRequestException — both propagate to the caller unchanged.</summary>
        public async Task<PdmValidationCallResult> ValidateAsync(
            ItemKind? kind,
            string fileName,
            string filePath,
            IDictionary<string, string> variables)
        {
            var body = new
            {
                Kind = kind?.ToWireValue(),
                FileName = fileName,
                FilePath = filePath,
                Variables = variables ?? new Dictionary<string, string>(),
            };

            try
            {
                var response = await _client
                    .PostAsync<PdmValidationResponse>(ApiRoutes.Pat + ApiRoutes.PdmValidate, body)
                    .ConfigureAwait(false);

                return PdmValidationCallResult.Succeeded(response);
            }
            catch (GladosApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                return PdmValidationCallResult.IntegrationDisabled();
            }
        }
    }

    /// <summary>Distinguishes PdmValidationApiService.ValidateAsync's one expected non-success
    /// outcome (409, PDM integration disabled) from an actual response.</summary>
    public enum PdmValidationCallStatus
    {
        Succeeded,
        IntegrationDisabled,
    }

    /// <summary>Result of PdmValidationApiService.ValidateAsync. Response is non-null only when
    /// Status is Succeeded.</summary>
    public class PdmValidationCallResult
    {
        public PdmValidationCallStatus Status { get; }

        public PdmValidationResponse Response { get; }

        private PdmValidationCallResult(PdmValidationCallStatus status, PdmValidationResponse response)
        {
            Status = status;
            Response = response;
        }

        public static PdmValidationCallResult Succeeded(PdmValidationResponse response) =>
            new PdmValidationCallResult(PdmValidationCallStatus.Succeeded, response);

        public static PdmValidationCallResult IntegrationDisabled() =>
            new PdmValidationCallResult(PdmValidationCallStatus.IntegrationDisabled, null);
    }
}

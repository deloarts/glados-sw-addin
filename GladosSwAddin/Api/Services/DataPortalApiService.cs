using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps the read-only EPLAN Data Portal routes of the PAT surface
    /// (glados/docs/EPLAN_DATA_PORTAL_INTEGRATION.md section 5). Nothing here writes to
    /// Glados: what the engineer takes from a part goes into the document, and PDM ingest
    /// brings it to the catalogue. Match, search and picture need use:data_portal on the
    /// token (403 otherwise) and answer EdpNotConfiguredError while the portal is off; status
    /// works for any token.</summary>
    public class DataPortalApiService
    {
        private const string Route = ApiRoutes.Pat + "bought-items-edp/";

        private readonly GladosApiClient _client;

        public DataPortalApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<EdpAvailabilityResponse> GetStatusAsync()
        {
            return _client.GetAsync<EdpAvailabilityResponse>(Route + "status");
        }

        public Task<EdpSearchResponse> MatchAsync(string number, int? manufacturerId)
        {
            return _client.GetAsync<EdpSearchResponse>(
                Route + "match",
                new EdpMatchFilterParams { Number = number, ManufacturerId = manufacturerId });
        }

        public Task<EdpSearchResponse> SearchAsync(string query, int? limit = null)
        {
            return _client.GetAsync<EdpSearchResponse>(
                Route + "search", new EdpSearchFilterParams { Q = query, Limit = limit });
        }

        /// <summary>The part's product picture (PNG), passed through Glados.</summary>
        public Task<byte[]> GetPictureAsync(int partId)
        {
            return _client.GetBytesAsync(Route + $"parts/{partId}/picture");
        }
    }
}

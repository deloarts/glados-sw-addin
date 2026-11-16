using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps the PAT source-items surface (docs/FIELD_OWNERSHIP.md row 11): list (same
    /// filters as the web list plus IncludeArchived), get by id, and create (same request
    /// schema and permission rule as the web create). Client-only for now — no task-pane UI
    /// calls this yet; GET /pat/lookups is what feeds the offline source item cache.</summary>
    public class SourceItemsApiService
    {
        private readonly GladosApiClient _client;

        public SourceItemsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<SourceItemResponse>> GetSourceItemsAsync(SourceItemFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<SourceItemResponse>>(ApiRoutes.Pat + "source-items/", filter);
        }

        public Task<SourceItemResponse> GetSourceItemAsync(int itemId)
        {
            return _client.GetAsync<SourceItemResponse>(ApiRoutes.Pat + $"source-items/{itemId}");
        }

        /// <summary>Creates the source item once per call: the request carries an
        /// Idempotency-Key, so a resend after a lost answer gets the item the first request
        /// created back instead of a second one.</summary>
        public Task<SourceItemResponse> CreateSourceItemAsync(SourceItemCreateRequest request)
        {
            return _client.PostIdempotentAsync<SourceItemResponse>(ApiRoutes.Pat + "source-items/", request);
        }
    }
}

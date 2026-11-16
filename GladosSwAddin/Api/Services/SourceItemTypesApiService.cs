using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/catalogue/adapters/inbound/api/v1/pat/endpoints/source_item_types.py.</summary>
    public class SourceItemTypesApiService
    {
        private readonly GladosApiClient _client;

        public SourceItemTypesApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<SourceItemTypeResponse>> GetSourceItemTypesAsync(
            SourceItemTypeFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<SourceItemTypeResponse>>(
                ApiRoutes.Pat + "source-item-types/", filter);
        }

        public Task<SourceItemTypeResponse> GetSourceItemTypeAsync(int sourceItemTypeId)
        {
            return _client.GetAsync<SourceItemTypeResponse>(
                ApiRoutes.Pat + $"source-item-types/{sourceItemTypeId}");
        }
    }
}

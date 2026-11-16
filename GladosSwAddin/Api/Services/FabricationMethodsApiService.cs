using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/catalogue/adapters/inbound/api/v1/pat/endpoints/fabrication_methods.py.</summary>
    public class FabricationMethodsApiService
    {
        private readonly GladosApiClient _client;

        public FabricationMethodsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<FabricationMethodResponse>> GetFabricationMethodsAsync(
            FabricationMethodFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<FabricationMethodResponse>>(
                ApiRoutes.Pat + "fabrication-methods/", filter);
        }

        public Task<FabricationMethodResponse> GetFabricationMethodAsync(int fabricationMethodId)
        {
            return _client.GetAsync<FabricationMethodResponse>(
                ApiRoutes.Pat + $"fabrication-methods/{fabricationMethodId}");
        }
    }
}

using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/catalogue/adapters/inbound/api/v1/pat/endpoints/inspection_methods.py.</summary>
    public class InspectionMethodsApiService
    {
        private readonly GladosApiClient _client;

        public InspectionMethodsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<InspectionMethodResponse>> GetInspectionMethodsAsync(
            InspectionMethodFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<InspectionMethodResponse>>(
                ApiRoutes.Pat + "inspection-methods/", filter);
        }

        public Task<InspectionMethodResponse> GetInspectionMethodAsync(int inspectionMethodId)
        {
            return _client.GetAsync<InspectionMethodResponse>(
                ApiRoutes.Pat + $"inspection-methods/{inspectionMethodId}");
        }
    }
}

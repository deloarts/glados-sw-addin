using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/catalogue/adapters/inbound/api/v1/pat/endpoints/suppliers.py.</summary>
    public class SuppliersApiService
    {
        private readonly GladosApiClient _client;

        public SuppliersApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<SupplierResponse>> GetSuppliersAsync(SupplierFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<SupplierResponse>>(ApiRoutes.Pat + "suppliers/", filter);
        }

        public Task<SupplierResponse> GetSupplierAsync(int supplierId)
        {
            return _client.GetAsync<SupplierResponse>(ApiRoutes.Pat + $"suppliers/{supplierId}");
        }
    }
}

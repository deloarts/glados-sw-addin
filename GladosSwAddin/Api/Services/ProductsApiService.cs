using System;
using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/workspace/adapters/inbound/api/v1/pat/endpoints/products.py.</summary>
    public class ProductsApiService
    {
        private readonly GladosApiClient _client;

        public ProductsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<ProductResponse>> GetProductsAsync(ProductFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<ProductResponse>>(ApiRoutes.Pat + "products/", filter);
        }

        public Task<ProductResponse> GetProductAsync(int productId)
        {
            return _client.GetAsync<ProductResponse>(ApiRoutes.Pat + $"products/{productId}");
        }

        public Task<ProductResponse> GetProductByNumberAsync(string productNumber)
        {
            return _client.GetAsync<ProductResponse>(
                ApiRoutes.Pat + $"products/number/{Uri.EscapeDataString(productNumber)}");
        }
    }
}

using System;
using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/catalogue/adapters/inbound/api/v1/pat/endpoints/bought_items.py.</summary>
    public class BoughtItemsApiService
    {
        private readonly GladosApiClient _client;

        public BoughtItemsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<BoughtItemResponse>> GetBoughtItemsAsync(BoughtItemFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<BoughtItemResponse>>(ApiRoutes.Pat + "bought-items/", filter);
        }

        public Task<BoughtItemResponse> GetBoughtItemAsync(int itemId)
        {
            return _client.GetAsync<BoughtItemResponse>(ApiRoutes.Pat + $"bought-items/{itemId}");
        }

        /// <summary>Throws GladosApiException with StatusCode NotFound if no catalogue item matches.</summary>
        public Task<BoughtItemResponse> GetBoughtItemByPartNumberAsync(string partNumber)
        {
            return _client.GetAsync<BoughtItemResponse>(
                ApiRoutes.Pat + $"bought-items/partnumber/{Uri.EscapeDataString(partNumber)}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/catalogue/adapters/inbound/api/v1/pat/endpoints/made_items.py — read-only
    /// (no PAT write/regroup endpoint; the add-in edits Glados.Processes in the file instead,
    /// see docs/FIELD_OWNERSHIP.md).</summary>
    public class MadeItemsApiService
    {
        private readonly GladosApiClient _client;

        public MadeItemsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<MadeItemResponse>> GetMadeItemsAsync(MadeItemFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<MadeItemResponse>>(ApiRoutes.Pat + "made-items/", filter);
        }

        public Task<MadeItemResponse> GetMadeItemAsync(int itemId)
        {
            return _client.GetAsync<MadeItemResponse>(ApiRoutes.Pat + $"made-items/{itemId}");
        }

        /// <summary>Returns the newest revision of the part-number family, or an exact revision
        /// when <paramref name="revision"/> is given. Throws GladosApiException with StatusCode
        /// NotFound if no catalogue item matches.</summary>
        public Task<MadeItemResponse> GetMadeItemByPartNumberAsync(string partNumber, string revision = null)
        {
            var query = QueryStringBuilder.Build(
                new[] { new KeyValuePair<string, object>("revision", revision) });

            return _client.GetAsync<MadeItemResponse>(
                ApiRoutes.Pat + $"made-items/partnumber/{Uri.EscapeDataString(partNumber)}" + query);
        }
    }
}

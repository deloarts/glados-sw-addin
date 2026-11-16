using System.Threading.Tasks;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>
    /// Wraps GET /pat/lookups (docs/FIELD_OWNERSHIP.md row 11) — the add-in's single bootstrap
    /// call for the Manufacturer/Article Group/Fabrication Method/Inspection Method/Source Item
    /// Type/Source Item lookup lists, replacing five capped list requests. Supports ETag
    /// revalidation: pass the previously stored ETag and a 304 comes back as
    /// ConditionalGetResult&lt;LookupsResponse&gt;.NotModified, with no body to parse.
    /// LookupService is the only caller.
    /// </summary>
    public class LookupsApiService
    {
        private readonly GladosApiClient _client;

        public LookupsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<ConditionalGetResult<LookupsResponse>> GetLookupsAsync(string ifNoneMatch = null)
        {
            return _client.GetConditionalAsync<LookupsResponse>(ApiRoutes.Pat + "lookups/", ifNoneMatch);
        }
    }
}

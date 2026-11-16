using System.Threading.Tasks;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>
    /// Wraps GET /pat/settings/field-mapping (docs/FIELD_OWNERSHIP.md section 1 row 10). A 404
    /// still degrades gracefully rather than failing — an add-in build talking to a backend
    /// that predates this endpoint, or any other server-side hiccup, must not block a
    /// connection. FieldMappingService is the only caller and already treats that (and any
    /// other error) as "keep the stored mapping or compiled-in defaults".
    /// </summary>
    public class FieldMappingApiService
    {
        private readonly GladosApiClient _client;

        public FieldMappingApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<FieldMappingResponse> GetFieldMappingAsync()
        {
            return _client.GetAsync<FieldMappingResponse>(ApiRoutes.Pat + "settings/field-mapping");
        }
    }
}

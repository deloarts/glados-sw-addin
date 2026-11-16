using System.Threading.Tasks;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/platform/adapters/inbound/api/v1/pat/endpoints/version.py. Any
    /// valid token may call it. The task pane asks once per successful connection and only ever
    /// warns on the answer (see PatContract), so a failure here never blocks anything.</summary>
    public class VersionApiService
    {
        private readonly GladosApiClient _client;

        public VersionApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PatVersionResponse> GetVersionAsync()
        {
            return _client.GetAsync<PatVersionResponse>(ApiRoutes.Pat + "version");
        }
    }
}

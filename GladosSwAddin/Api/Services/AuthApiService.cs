using System.Threading.Tasks;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/platform/adapters/inbound/api/v1/auth/endpoints/pat_login.py.</summary>
    public class AuthApiService
    {
        private readonly GladosApiClient _client;

        public AuthApiService(GladosApiClient client)
        {
            _client = client;
        }

        /// <summary>Validates the configured PAT and returns the user it belongs to.</summary>
        public Task<UserResponse> TestPersonalAccessTokenAsync()
        {
            return _client.PostAsync<UserResponse>(ApiRoutes.Auth + "login/test-personal-access-token", null);
        }
    }
}

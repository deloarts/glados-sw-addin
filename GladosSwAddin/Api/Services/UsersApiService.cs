using System.Threading.Tasks;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/platform/adapters/inbound/api/v1/pat/endpoints/users.py.</summary>
    public class UsersApiService
    {
        private readonly GladosApiClient _client;

        public UsersApiService(GladosApiClient client)
        {
            _client = client;
        }

        /// <summary>Returns the user the configured PAT belongs to.</summary>
        public Task<UserResponse> GetMeAsync()
        {
            return _client.GetAsync<UserResponse>(ApiRoutes.Pat + "users/me");
        }

        /// <summary>Returns the avatar image bytes for the user the configured PAT belongs to.</summary>
        public Task<byte[]> GetMyAvatarAsync()
        {
            return _client.GetBytesAsync(ApiRoutes.Pat + "users/me/avatar");
        }
    }
}

using System;
using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/workspace/adapters/inbound/api/v1/pat/endpoints/projects.py.</summary>
    public class ProjectsApiService
    {
        private readonly GladosApiClient _client;

        public ProjectsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<ProjectResponse>> GetProjectsAsync(ProjectFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<ProjectResponse>>(ApiRoutes.Pat + "projects/", filter);
        }

        public Task<ProjectResponse> GetProjectAsync(int projectId)
        {
            return _client.GetAsync<ProjectResponse>(ApiRoutes.Pat + $"projects/{projectId}");
        }

        public Task<ProjectResponse> GetProjectByNumberAsync(string projectNumber)
        {
            return _client.GetAsync<ProjectResponse>(
                ApiRoutes.Pat + $"projects/number/{Uri.EscapeDataString(projectNumber)}");
        }
    }
}

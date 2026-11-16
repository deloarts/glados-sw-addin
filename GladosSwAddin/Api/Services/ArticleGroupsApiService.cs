using System.Threading.Tasks;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Services
{
    /// <summary>Wraps src/modules/catalogue/adapters/inbound/api/v1/pat/endpoints/article_groups.py.</summary>
    public class ArticleGroupsApiService
    {
        private readonly GladosApiClient _client;

        public ArticleGroupsApiService(GladosApiClient client)
        {
            _client = client;
        }

        public Task<PageResponse<ArticleGroupResponse>> GetArticleGroupsAsync(ArticleGroupFilterParams filter = null)
        {
            return _client.GetAsync<PageResponse<ArticleGroupResponse>>(ApiRoutes.Pat + "article-groups/", filter);
        }

        public Task<ArticleGroupResponse> GetArticleGroupAsync(int articleGroupId)
        {
            return _client.GetAsync<ArticleGroupResponse>(ApiRoutes.Pat + $"article-groups/{articleGroupId}");
        }
    }
}

using System;
using GladosSwAddin.Api.Services;

namespace GladosSwAddin.Api
{
    /// <summary>
    /// Facade over the Glados backend's PAT/auth API surface. One instance per
    /// server URL / PAT combination — see ApplicationServices.Api for the
    /// long-lived instance used by the rest of the addin.
    /// </summary>
    public class GladosApi : IDisposable
    {
        private readonly GladosApiClient _client;

        public AuthApiService Auth { get; }

        public UsersApiService Users { get; }

        public ProductsApiService Products { get; }

        public ProjectsApiService Projects { get; }

        public SuppliersApiService Suppliers { get; }

        public ArticleGroupsApiService ArticleGroups { get; }

        public FabricationMethodsApiService FabricationMethods { get; }

        public InspectionMethodsApiService InspectionMethods { get; }

        public SourceItemTypesApiService SourceItemTypes { get; }

        public SourceItemsApiService SourceItems { get; }

        public LookupsApiService Lookups { get; }

        public BoughtItemsApiService BoughtItems { get; }

        public MadeItemsApiService MadeItems { get; }

        public FieldMappingApiService FieldMapping { get; }

        public PdmValidationApiService PdmValidation { get; }

        public DataPortalApiService DataPortal { get; }

        public VersionApiService Version { get; }

        public GladosApi(string serverUrl, string personalAccessToken)
        {
            _client = new GladosApiClient(serverUrl, personalAccessToken);

            Auth = new AuthApiService(_client);
            Users = new UsersApiService(_client);
            Products = new ProductsApiService(_client);
            Projects = new ProjectsApiService(_client);
            Suppliers = new SuppliersApiService(_client);
            ArticleGroups = new ArticleGroupsApiService(_client);
            FabricationMethods = new FabricationMethodsApiService(_client);
            InspectionMethods = new InspectionMethodsApiService(_client);
            SourceItemTypes = new SourceItemTypesApiService(_client);
            SourceItems = new SourceItemsApiService(_client);
            Lookups = new LookupsApiService(_client);
            BoughtItems = new BoughtItemsApiService(_client);
            MadeItems = new MadeItemsApiService(_client);
            FieldMapping = new FieldMappingApiService(_client);
            PdmValidation = new PdmValidationApiService(_client);
            DataPortal = new DataPortalApiService(_client);
            Version = new VersionApiService(_client);
        }

        public void Dispose()
        {
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

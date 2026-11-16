using System;
using System.Collections.Generic;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors the response body of GET /pat/lookups (docs/FIELD_OWNERSHIP.md row 11):
    /// the add-in's single bootstrap call for the Manufacturer/Article Group/Fabrication Method/
    /// Inspection Method/Source Item Type/Source Item/Customer lookup lists, replacing five capped list
    /// requests. Unpaged — every row, including archived, ordered by id. LookupsApiService reads
    /// this via GladosApiClient.GetConditionalAsync, which also surfaces the ETag response
    /// header this body doesn't carry itself.</summary>
    public class LookupsResponse
    {
        public DateTimeOffset ServerTime { get; set; }

        public List<SupplierResponse> Suppliers { get; set; } = new List<SupplierResponse>();

        public List<ArticleGroupResponse> ArticleGroups { get; set; } = new List<ArticleGroupResponse>();

        public List<FabricationMethodResponse> FabricationMethods { get; set; } =
            new List<FabricationMethodResponse>();

        public List<InspectionMethodResponse> InspectionMethods { get; set; } =
            new List<InspectionMethodResponse>();

        public List<SourceItemTypeResponse> SourceItemTypes { get; set; } =
            new List<SourceItemTypeResponse>();

        public List<SourceItemResponse> SourceItems { get; set; } = new List<SourceItemResponse>();

        public List<CustomerLookupResponse> Customers { get; set; } = new List<CustomerLookupResponse>();
    }
}

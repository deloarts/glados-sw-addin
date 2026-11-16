using System;
using System.Collections.Generic;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors SupplierResponse in src/modules/catalogue/adapters/inbound/api/schemas/supplier_schema.py.</summary>
    public class SupplierResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset? Changed { get; set; }

        public bool Archived { get; set; }

        public string Name { get; set; }

        public string LegalName { get; set; }

        public string VatId { get; set; }

        public SupplierLevel Level { get; set; }

        public List<int> CapabilityIds { get; set; }

        public List<FabricationMethodResponse> Capabilities { get; set; }

        public List<int> ArticleGroupIds { get; set; }

        public List<ArticleGroupResponse> ArticleGroups { get; set; }

        public string Street { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        public string PostalCode { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string Weblink { get; set; }

        public string OrderEmail { get; set; }

        public string OrderLanguage { get; set; }

        public string OrderTemplate { get; set; }

        public string RequestEmail { get; set; }

        public string RequestLanguage { get; set; }

        public string RequestTemplate { get; set; }

        public int CreatorId { get; set; }

        public PublicUserResponse Creator { get; set; }
    }
}

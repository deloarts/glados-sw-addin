using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors CustomerResponse in src/modules/catalogue/adapters/inbound/api/schemas/customer_schema.py.</summary>
    public class CustomerResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public DateTime Created { get; set; }

        public DateTime? Changed { get; set; }

        public bool Archived { get; set; }

        public string Name { get; set; }

        public string LegalName { get; set; }

        public string VatId { get; set; }

        public string Level { get; set; }

        public string Street { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        public string PostalCode { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public string Weblink { get; set; }

        public int CreatorId { get; set; }

        public PublicUserResponse Creator { get; set; }
    }
}

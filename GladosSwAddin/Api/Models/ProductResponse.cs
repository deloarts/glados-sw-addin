using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors ProductResponse in src/modules/workspace/adapters/inbound/api/schemas/product_schema.py.</summary>
    public class ProductResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public string ProductNumber { get; set; }

        public int? CustomerId { get; set; }

        public CustomerResponse Customer { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; }

        public int DesignatedUserId { get; set; }

        public DateTime Created { get; set; }

        public int CreatorId { get; set; }

        public PublicUserResponse Creator { get; set; }
    }
}

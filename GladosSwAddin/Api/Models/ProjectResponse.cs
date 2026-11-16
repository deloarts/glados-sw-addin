using System;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors ProjectResponse in src/modules/workspace/adapters/inbound/api/schemas/project_schema.py.</summary>
    public class ProjectResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public string ProjectNumber { get; set; }

        public int? CustomerId { get; set; }

        public CustomerResponse Customer { get; set; }

        public int? ProductId { get; set; }

        public ProductResponse Product { get; set; }

        public string Description { get; set; }

        public ProjectStatus Status { get; set; }

        public int DesignatedUserId { get; set; }

        public int? ProductionTimeBudget { get; set; }

        public DateTime Created { get; set; }

        public DateTime? Changed { get; set; }

        public int CreatorId { get; set; }

        public PublicUserResponse Creator { get; set; }

        public DateTime? RequiredDeliveryDateMade { get; set; }

        public DateTime? RequiredDeliveryDateBought { get; set; }
    }
}

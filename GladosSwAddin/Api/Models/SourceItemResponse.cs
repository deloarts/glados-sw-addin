using System;
using Newtonsoft.Json;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors SourceItemResponse in
    /// src/modules/catalogue/adapters/inbound/api/schemas/source_item_schema.py — client-only for now (see
    /// SourceItemsApiService), no task-pane UI reads this yet.</summary>
    public class SourceItemResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset? Changed { get; set; }

        public bool Archived { get; set; }

        public int TypeId { get; set; }

        public string TypeName { get; set; }

        public string Dimension { get; set; }

        public string Material { get; set; }

        public int? Mass { get; set; }

        public string Note { get; set; }

        // SnakeCaseNamingStrategy doesn't insert '_' before a trailing digit
        // (OrderCode1 -> "order_code1"), so the JSON key is pinned explicitly here — same as
        // BoughtItemResponse.
        [JsonProperty("order_code_1")]
        public string OrderCode1 { get; set; }

        [JsonProperty("order_code_2")]
        public string OrderCode2 { get; set; }

        public SourceItemCatalogueStatus Status { get; set; }

        public int CreatorId { get; set; }

        public int? ApproverId { get; set; }

        public int? ManufacturerId { get; set; }

        public string ManufacturerName { get; set; }

        public int? SuccessorItemId { get; set; }
    }
}

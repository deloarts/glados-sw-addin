using Newtonsoft.Json;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors SourceItemCreateRequest in
    /// src/modules/catalogue/adapters/inbound/api/schemas/source_item_schema.py — same request schema and
    /// permission rule as the web create (SourceItemsApiService.CreateSourceItemAsync).</summary>
    public class SourceItemCreateRequest
    {
        public int TypeId { get; set; }

        public string Dimension { get; set; }

        public string Material { get; set; }

        public int? Mass { get; set; }

        public string Note { get; set; }

        [JsonProperty("order_code_1")]
        public string OrderCode1 { get; set; }

        [JsonProperty("order_code_2")]
        public string OrderCode2 { get; set; }

        public SourceItemCatalogueStatus Status { get; set; } = SourceItemCatalogueStatus.Active;

        public int? ManufacturerId { get; set; }
    }
}

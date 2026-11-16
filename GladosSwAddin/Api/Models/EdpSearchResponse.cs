using System.Collections.Generic;
using Newtonsoft.Json;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors EdpSearchResponse in
    /// src/modules/external_data/adapters/inbound/api/schemas/bought_item_edp_schema.py, the answer of both
    /// GET /pat/bought-items-edp/match and .../search.</summary>
    public class EdpSearchResponse
    {
        public List<EdpSearchHitResponse> Hits { get; set; }
    }

    /// <summary>Mirrors EdpSearchHitResponse: one portal part, the bought-item values it fills,
    /// and the catalogue item that already describes it, if any.</summary>
    public class EdpSearchHitResponse
    {
        public EdpPartSummaryResponse Part { get; set; }

        public EdpItemDraftResponse Draft { get; set; }

        public BoughtItemResponse ExistingItem { get; set; }
    }

    /// <summary>Mirrors EdpPartSummaryResponse in
    /// src/modules/external_data/adapters/inbound/api/schemas/bought_item_import_schema.py. PictureUrl and
    /// PartPageUrl point at the portal itself; the picture is fetched through Glados instead
    /// (DataPortalApiService.GetPictureAsync), the page link is only ever opened in the
    /// browser.</summary>
    public class EdpPartSummaryResponse
    {
        public int PartId { get; set; }

        public string PartNumber { get; set; }

        public string OrderNumber { get; set; }

        public string TypeNumber { get; set; }

        public string Designation { get; set; }

        public string ManufacturerCode { get; set; }

        public string ManufacturerName { get; set; }

        public string ProductFamilyName { get; set; }

        public string PictureUrl { get; set; }

        public string PartPageUrl { get; set; }
    }

    /// <summary>Mirrors EdpItemDraftResponse: what the backend's edp_item_draft decided a part
    /// fills, with the order codes already swapped for a manufacturer that puts its type number
    /// first and the manufacturer resolved by its Data Portal code (null when no Glados supplier
    /// carries it). ProductFamily* has no custom property yet and is shown, never written.</summary>
    public class EdpItemDraftResponse
    {
        public int EdpPartId { get; set; }

        public string ArticleName { get; set; }

        // See BoughtItemResponse: the snake-case strategy writes "order_code1" otherwise.
        [JsonProperty("order_code_1")]
        public string OrderCode1 { get; set; }

        [JsonProperty("order_code_2")]
        public string OrderCode2 { get; set; }

        public string Ean { get; set; }

        public string Weblink { get; set; }

        public string ManufacturerCode { get; set; }

        public int? ManufacturerId { get; set; }

        public string ProductFamilyName { get; set; }

        public int? ProductFamilyId { get; set; }

        public List<int> ArticleGroupIds { get; set; }
    }
}

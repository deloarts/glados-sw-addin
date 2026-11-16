using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors BoughtItemResponse in src/modules/catalogue/adapters/inbound/api/schemas/bought_item_schema.py.</summary>
    public class BoughtItemResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public string PartNumber { get; set; }

        public BoughtItemCatalogueStatus Status { get; set; }

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset? Changed { get; set; }

        public bool Archived { get; set; }

        // SnakeCaseNamingStrategy doesn't insert '_' before a trailing digit
        // (OrderCode1 -> "order_code1"), so the JSON key is pinned explicitly here.
        [JsonProperty("order_code_1")]
        public string OrderCode1 { get; set; }

        [JsonProperty("order_code_2")]
        public string OrderCode2 { get; set; }

        public string Gtin { get; set; }

        public string Ean { get; set; }

        public string TariffNumber { get; set; }

        public string ArticleName { get; set; }

        public List<ArticleGroupResponse> ArticleGroups { get; set; }

        public DataSource DataSource { get; set; }

        public int? Mass { get; set; }

        public string Note { get; set; }

        public string Weblink { get; set; }

        public int? SuccessorItemId { get; set; }

        public int CreatorId { get; set; }

        public PublicUserResponse Creator { get; set; }

        public int ManufacturerId { get; set; }

        public string ManufacturerName { get; set; }

        /// <summary>The EPLAN Data Portal part the catalogue item is linked to, null when
        /// none.</summary>
        public int? EdpPartId { get; set; }

        public List<BoughtItemPdfFileResponse> PdfFiles { get; set; }
    }
}

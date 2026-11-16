using System;
using System.Collections.Generic;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Filters
{
    /// <summary>Mirrors BoughtItemFilterParams in src/modules/catalogue/adapters/inbound/api/schemas/bought_item_schema.py.</summary>
    public class BoughtItemFilterParams : IFilterParams
    {
        public int? Skip { get; set; }

        public int? Limit { get; set; }

        public string Search { get; set; }

        public int? Id { get; set; }

        public BoughtItemCatalogueStatus? Status { get; set; }

        public string OrderCode1 { get; set; }

        public string OrderCode2 { get; set; }

        public string Gtin { get; set; }

        public string Ean { get; set; }

        public string TariffNumber { get; set; }

        public string ArticleName { get; set; }

        public int? ArticleGroupId { get; set; }

        public DataSource? DataSource { get; set; }

        public string PartNumber { get; set; }

        public int? Mass { get; set; }

        public string Note { get; set; }

        public string Weblink { get; set; }

        public int? SuccessorItemId { get; set; }

        public int? ManufacturerId { get; set; }

        public string ManufacturerName { get; set; }

        public int? CreatorId { get; set; }

        public string CreatorFullName { get; set; }

        public int? ApproverId { get; set; }

        public DateTime? CreatedFrom { get; set; }

        public DateTime? CreatedTo { get; set; }

        public DateTime? ChangedFrom { get; set; }

        public DateTime? ChangedTo { get; set; }

        /// <summary>One of: id, article_name, manufacturer_name, changed, created.</summary>
        public string OrderBy { get; set; }

        /// <summary>One of: asc, desc.</summary>
        public string OrderDir { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToQueryParameters()
        {
            yield return new KeyValuePair<string, object>("skip", Skip);
            yield return new KeyValuePair<string, object>("limit", Limit);
            yield return new KeyValuePair<string, object>("search", Search);
            yield return new KeyValuePair<string, object>("id", Id);
            yield return new KeyValuePair<string, object>("status", Status);
            yield return new KeyValuePair<string, object>("order_code_1", OrderCode1);
            yield return new KeyValuePair<string, object>("order_code_2", OrderCode2);
            yield return new KeyValuePair<string, object>("gtin", Gtin);
            yield return new KeyValuePair<string, object>("ean", Ean);
            yield return new KeyValuePair<string, object>("tariff_number", TariffNumber);
            yield return new KeyValuePair<string, object>("article_name", ArticleName);
            yield return new KeyValuePair<string, object>("article_group_id", ArticleGroupId);
            yield return new KeyValuePair<string, object>("data_source", DataSource);
            yield return new KeyValuePair<string, object>("part_number", PartNumber);
            yield return new KeyValuePair<string, object>("mass", Mass);
            yield return new KeyValuePair<string, object>("note", Note);
            yield return new KeyValuePair<string, object>("weblink", Weblink);
            yield return new KeyValuePair<string, object>("successor_item_id", SuccessorItemId);
            yield return new KeyValuePair<string, object>("manufacturer_id", ManufacturerId);
            yield return new KeyValuePair<string, object>("manufacturer_name", ManufacturerName);
            yield return new KeyValuePair<string, object>("creator_id", CreatorId);
            yield return new KeyValuePair<string, object>("creator_full_name", CreatorFullName);
            yield return new KeyValuePair<string, object>("approver_id", ApproverId);
            yield return new KeyValuePair<string, object>("created_from", CreatedFrom);
            yield return new KeyValuePair<string, object>("created_to", CreatedTo);
            yield return new KeyValuePair<string, object>("changed_from", ChangedFrom);
            yield return new KeyValuePair<string, object>("changed_to", ChangedTo);
            yield return new KeyValuePair<string, object>("order_by", OrderBy);
            yield return new KeyValuePair<string, object>("order_dir", OrderDir);
        }
    }
}

using System;
using System.Collections.Generic;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Api.Filters
{
    /// <summary>Mirrors SourceItemFilterParams in
    /// src/modules/catalogue/adapters/inbound/api/schemas/source_item_schema.py, plus IncludeArchived — the PAT
    /// source-items list takes the same filters as the web list plus include_archived.</summary>
    public class SourceItemFilterParams : IFilterParams
    {
        public int? Skip { get; set; }

        public int? Limit { get; set; }

        public string Search { get; set; }

        public int? Id { get; set; }

        public SourceItemCatalogueStatus? Status { get; set; }

        public int? TypeId { get; set; }

        public string Material { get; set; }

        public string Dimension { get; set; }

        public string Note { get; set; }

        public string OrderCode1 { get; set; }

        public string OrderCode2 { get; set; }

        public int? SuccessorItemId { get; set; }

        public int? ManufacturerId { get; set; }

        public int? CreatorId { get; set; }

        public int? ApproverId { get; set; }

        public DateTime? CreatedFrom { get; set; }

        public DateTime? CreatedTo { get; set; }

        public DateTime? ChangedFrom { get; set; }

        public DateTime? ChangedTo { get; set; }

        /// <summary>One of: id, changed, created.</summary>
        public string OrderBy { get; set; }

        /// <summary>One of: asc, desc.</summary>
        public string OrderDir { get; set; }

        public bool? IncludeArchived { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToQueryParameters()
        {
            yield return new KeyValuePair<string, object>("skip", Skip);
            yield return new KeyValuePair<string, object>("limit", Limit);
            yield return new KeyValuePair<string, object>("search", Search);
            yield return new KeyValuePair<string, object>("id", Id);
            yield return new KeyValuePair<string, object>("status", Status);
            yield return new KeyValuePair<string, object>("type_id", TypeId);
            yield return new KeyValuePair<string, object>("material", Material);
            yield return new KeyValuePair<string, object>("dimension", Dimension);
            yield return new KeyValuePair<string, object>("note", Note);
            yield return new KeyValuePair<string, object>("order_code_1", OrderCode1);
            yield return new KeyValuePair<string, object>("order_code_2", OrderCode2);
            yield return new KeyValuePair<string, object>("successor_item_id", SuccessorItemId);
            yield return new KeyValuePair<string, object>("manufacturer_id", ManufacturerId);
            yield return new KeyValuePair<string, object>("creator_id", CreatorId);
            yield return new KeyValuePair<string, object>("approver_id", ApproverId);
            yield return new KeyValuePair<string, object>("created_from", CreatedFrom);
            yield return new KeyValuePair<string, object>("created_to", CreatedTo);
            yield return new KeyValuePair<string, object>("changed_from", ChangedFrom);
            yield return new KeyValuePair<string, object>("changed_to", ChangedTo);
            yield return new KeyValuePair<string, object>("order_by", OrderBy);
            yield return new KeyValuePair<string, object>("order_dir", OrderDir);
            yield return new KeyValuePair<string, object>("include_archived", IncludeArchived);
        }
    }
}

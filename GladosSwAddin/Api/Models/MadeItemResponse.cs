using System;
using System.Collections.Generic;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Subset of MadeItemResponse in
    /// src/modules/catalogue/adapters/inbound/api/schemas/made_item_schema.py — the add-in only needs
    /// the fields that drive the sync indicator, divergence compare and Load-from-Glados.
    /// Omitted: Created, Changed, RevisionDate, RevisionNote, SuccessorItemId, CreatorId,
    /// Creator, ChangedById, ChangedBy, BoughtItem, SourceItem, SourceLabel, SourceSublabel,
    /// PdmFileId, PdmFileVersion, PdmLockedFields, PdmSyncState, PdmSyncedAt, VaultId.</summary>
    public class MadeItemResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public string PartNumber { get; set; }

        public MadeItemCatalogueStatus Status { get; set; }

        public bool Archived { get; set; }

        public string Revision { get; set; }

        public string ArticleName { get; set; }

        public List<ArticleGroupResponse> ArticleGroups { get; set; }

        public DataSource DataSource { get; set; }

        public string Material { get; set; }

        public int? Mass { get; set; }

        public string BaseSize { get; set; }

        public string Note { get; set; }

        public string Weblink { get; set; }

        public int? BoughtItemId { get; set; }

        public int? SourceItemId { get; set; }

        /// <summary>The PDM shadow of SourceItemId (docs/FIELD_OWNERSHIP.md row 8): the value
        /// last received from PDM, next to the effective one above. Null on an older backend
        /// that doesn't send it yet, or when PDM hasn't set one — either way, the add-in's
        /// divergence check treats a missing value the same as "no id", never as a mismatch.
        /// See MadeItemSyncService.GetDivergentFields.</summary>
        public int? PdmSourceItemId { get; set; }

        public List<MadeItemProcessResponse> Processes { get; set; }

        public int? CustomerId { get; set; }

        public string CustomerName { get; set; }

        public string CustomerPartNumber { get; set; }

        public string CustomerRevision { get; set; }
    }
}

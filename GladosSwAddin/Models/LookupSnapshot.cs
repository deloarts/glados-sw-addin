using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Models
{
    /// <summary>Immutable snapshot of the offline lookup data (Manufacturer/Article Group/
    /// Fabrication Method/Inspection Method/Source Item Type/Source Item/Customer lists) the task pane's
    /// pickers and id-to-label resolvers read from. Built once by LookupService — from either a
    /// GET /pat/lookups response or the on-disk cache (LookupCacheService) — then swapped into
    /// LookupService.Current atomically; never mutated in place. Every row is kept, including
    /// archived ones, so id-to-name resolution always has an answer; it's the task pane's picker
    /// code that filters Archived out of what it offers to pick from (see
    /// docs/FIELD_OWNERSHIP.md and docs/DESIGN.md derived-property rules).</summary>
    public sealed class LookupSnapshot
    {
        /// <summary>The state before anything has ever been loaded (no cache on disk yet, no
        /// successful connect yet) — empty lists, no server/etag/sync provenance.</summary>
        public static readonly LookupSnapshot Empty = new LookupSnapshot(
            serverUrl: null,
            etag: null,
            serverTime: null,
            syncedAt: null,
            isFromCache: false,
            suppliers: null,
            articleGroups: null,
            fabricationMethods: null,
            inspectionMethods: null,
            sourceItemTypes: null,
            sourceItems: null,
            customers: null);

        public string ServerUrl { get; }

        public string ETag { get; }

        public DateTimeOffset? ServerTime { get; }

        public DateTimeOffset? SyncedAt { get; }

        /// <summary>True when this snapshot came from LookupCacheService.TryLoad rather than a
        /// just-completed GET /pat/lookups.</summary>
        public bool IsFromCache { get; }

        public IReadOnlyList<SupplierResponse> Suppliers { get; }

        public IReadOnlyList<ArticleGroupResponse> ArticleGroups { get; }

        public IReadOnlyList<FabricationMethodResponse> FabricationMethods { get; }

        public IReadOnlyList<InspectionMethodResponse> InspectionMethods { get; }

        public IReadOnlyList<SourceItemTypeResponse> SourceItemTypes { get; }

        public IReadOnlyList<SourceItemResponse> SourceItems { get; }

        public IReadOnlyList<CustomerLookupResponse> Customers { get; }

        public LookupSnapshot(
            string serverUrl,
            string etag,
            DateTimeOffset? serverTime,
            DateTimeOffset? syncedAt,
            bool isFromCache,
            IEnumerable<SupplierResponse> suppliers,
            IEnumerable<ArticleGroupResponse> articleGroups,
            IEnumerable<FabricationMethodResponse> fabricationMethods,
            IEnumerable<InspectionMethodResponse> inspectionMethods,
            IEnumerable<SourceItemTypeResponse> sourceItemTypes,
            IEnumerable<SourceItemResponse> sourceItems,
            IEnumerable<CustomerLookupResponse> customers)
        {
            ServerUrl = serverUrl;
            ETag = etag;
            ServerTime = serverTime;
            SyncedAt = syncedAt;
            IsFromCache = isFromCache;
            Suppliers = ToReadOnly(suppliers);
            ArticleGroups = ToReadOnly(articleGroups);
            FabricationMethods = ToReadOnly(fabricationMethods);
            InspectionMethods = ToReadOnly(inspectionMethods);
            SourceItemTypes = ToReadOnly(sourceItemTypes);
            SourceItems = ToReadOnly(sourceItems);
            Customers = ToReadOnly(customers);
        }

        /// <summary>A copy with SyncedAt bumped to now and everything else unchanged — used on a
        /// 304 (ETag matched, nothing to re-download: LookupService.RefreshAsync) and when
        /// persisting an unmodified cache reload.</summary>
        public LookupSnapshot WithSyncedAt(DateTimeOffset syncedAt)
        {
            return new LookupSnapshot(
                ServerUrl, ETag, ServerTime, syncedAt, IsFromCache,
                Suppliers, ArticleGroups, FabricationMethods, InspectionMethods, SourceItemTypes, SourceItems,
                Customers);
        }

        private static IReadOnlyList<T> ToReadOnly<T>(IEnumerable<T> items)
        {
            return new ReadOnlyCollection<T>((items ?? Enumerable.Empty<T>()).ToList());
        }
    }
}

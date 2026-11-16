using System;
using System.Collections.Generic;
using GladosSwAddin.Api.Models;
using Newtonsoft.Json;

namespace GladosSwAddin.Models
{
    /// <summary>On-disk shape of
    /// %LOCALAPPDATA%\GladosSwAddin\cache\&lt;serverKey&gt;\lookups.json — see
    /// LookupCacheService for serverKey and the atomic-write/locking scheme, and LookupSnapshot
    /// for the immutable in-memory shape this round-trips through (LookupService converts
    /// between the two). SchemaVersion and ServerUrl are stamped by LookupCacheService.Save
    /// right before writing; TryLoad treats a stored blob whose SchemaVersion doesn't match, or
    /// whose ServerUrl doesn't match the currently configured server, the same as "no
    /// cache".</summary>
    public class LookupCacheEnvelope
    {
        public int SchemaVersion { get; set; }

        public string ServerUrl { get; set; }

        // SnakeCaseNamingStrategy would turn "ETag" into "e_tag" (the same trailing-run rule
        // that makes OrderCode1 -> "order_code1" need pinning elsewhere in this codebase) —
        // pinned explicitly so the stored key is "etag".
        [JsonProperty("etag")]
        public string ETag { get; set; }

        public DateTimeOffset? ServerTime { get; set; }

        public DateTimeOffset SyncedAt { get; set; }

        public List<SupplierResponse> Suppliers { get; set; } = new List<SupplierResponse>();

        public List<ArticleGroupResponse> ArticleGroups { get; set; } = new List<ArticleGroupResponse>();

        public List<FabricationMethodResponse> FabricationMethods { get; set; } =
            new List<FabricationMethodResponse>();

        public List<InspectionMethodResponse> InspectionMethods { get; set; } =
            new List<InspectionMethodResponse>();

        public List<SourceItemTypeResponse> SourceItemTypes { get; set; } =
            new List<SourceItemTypeResponse>();

        public List<SourceItemResponse> SourceItems { get; set; } = new List<SourceItemResponse>();

        public List<CustomerLookupResponse> Customers { get; set; } = new List<CustomerLookupResponse>();
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Services;
using GladosSwAddin.Models;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Owns the in-memory LookupSnapshot the task pane's pickers and id-to-label resolvers read
    /// from (ResolveManufacturerLabel, ResolveArticleGroupLabels, ResolveStepMethodNames,
    /// PopulateLookupControls). Backed by LookupCacheService for the on-disk fallback and
    /// LookupsApiService for the live GET /pat/lookups call. Current is swapped atomically
    /// (Interlocked.Exchange) so a reader on another thread never sees a torn snapshot; Changed
    /// fires after every swap so the task pane can repopulate — its handler must marshal to the
    /// UI thread itself (via the existing OnUiThread), since a swap from RefreshAsync happens on
    /// whatever thread that async call resumes on.
    /// </summary>
    public class LookupService
    {
        private readonly LookupCacheService _cacheService;
        private readonly SettingsService _settingsService;

        private LookupSnapshot _current = LookupSnapshot.Empty;

        public LookupService(LookupCacheService cacheService, SettingsService settingsService)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public event EventHandler Changed;

        public LookupSnapshot Current => Volatile.Read(ref _current);

        /// <summary>Loads the on-disk cache for the currently configured server and swaps it in
        /// as Current — called before the PAT check even runs, so the pickers show last-known
        /// choices regardless of connectivity. A no-op (Current left as-is, no Changed event)
        /// if there's no valid cache for this server yet.</summary>
        public LookupSnapshot LoadCached()
        {
            var serverUrl = _settingsService.Load().ServerUrl;
            var envelope = _cacheService.TryLoad(serverUrl);
            if (envelope == null)
                return null;

            var snapshot = FromEnvelope(envelope, isFromCache: true);
            Swap(snapshot);
            return snapshot;
        }

        /// <summary>One GET /pat/lookups with If-None-Match set to Current's ETag (only when
        /// Current already belongs to the currently configured server — a server switch must
        /// never send a stale ETag from a different backend). On a 304 the snapshot is kept and
        /// SyncedAt is bumped; on 200 a new snapshot is built and both the in-memory Current and
        /// the on-disk cache are updated. Never throws: a transport/API failure just leaves
        /// whatever Current already holds serving requests, mirroring
        /// FieldMappingService.SyncAsync.</summary>
        public async Task RefreshAsync(LookupsApiService api)
        {
            if (api == null)
                throw new ArgumentNullException(nameof(api));

            var rawServerUrl = _settingsService.Load().ServerUrl;
            var normalizedServerUrl = NormalizeServerUrl(rawServerUrl);

            // The cross-process lock lives inside LookupCacheService.Save, around the write
            // only: a named Mutex is thread-affine and this method resumes on an arbitrary
            // thread after the await, so it must not be held across the download.
            var current = Current;
            var sameServer = string.Equals(
                NormalizeServerUrl(current.ServerUrl), normalizedServerUrl, StringComparison.OrdinalIgnoreCase);
            var etag = sameServer ? current.ETag : null;

            try
            {
                var result = await api.GetLookupsAsync(etag).ConfigureAwait(false);

                if (result.NotModified)
                {
                    if (sameServer)
                    {
                        var refreshed = current.WithSyncedAt(DateTimeOffset.UtcNow);
                        Swap(refreshed);
                        _cacheService.Save(rawServerUrl, ToEnvelope(refreshed));
                    }
                    return;
                }

                var response = result.Value;
                var snapshot = new LookupSnapshot(
                    serverUrl: normalizedServerUrl,
                    etag: result.ETag,
                    serverTime: response?.ServerTime,
                    syncedAt: DateTimeOffset.UtcNow,
                    isFromCache: false,
                    suppliers: response?.Suppliers,
                    articleGroups: response?.ArticleGroups,
                    fabricationMethods: response?.FabricationMethods,
                    inspectionMethods: response?.InspectionMethods,
                    sourceItemTypes: response?.SourceItemTypes,
                    sourceItems: response?.SourceItems,
                    customers: response?.Customers);

                Swap(snapshot);
                _cacheService.Save(rawServerUrl, ToEnvelope(snapshot));
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                // Never throws, mirroring FieldMappingService.SyncAsync: a transport
                // failure, a timed-out request (HttpClient surfaces those as
                // TaskCanceledException, not HttpRequestException), or any other error here
                // just leaves whatever Current already holds serving requests.
                Log.Warning("Lookups refresh failed, keeping the cached/current snapshot", ex);
            }
        }

        private void Swap(LookupSnapshot snapshot)
        {
            Interlocked.Exchange(ref _current, snapshot);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static LookupSnapshot FromEnvelope(LookupCacheEnvelope envelope, bool isFromCache)
        {
            return new LookupSnapshot(
                envelope.ServerUrl,
                envelope.ETag,
                envelope.ServerTime,
                envelope.SyncedAt,
                isFromCache,
                envelope.Suppliers,
                envelope.ArticleGroups,
                envelope.FabricationMethods,
                envelope.InspectionMethods,
                envelope.SourceItemTypes,
                envelope.SourceItems,
                envelope.Customers);
        }

        private static LookupCacheEnvelope ToEnvelope(LookupSnapshot snapshot)
        {
            return new LookupCacheEnvelope
            {
                ServerUrl = snapshot.ServerUrl,
                ETag = snapshot.ETag,
                ServerTime = snapshot.ServerTime,
                SyncedAt = snapshot.SyncedAt ?? DateTimeOffset.UtcNow,
                Suppliers = snapshot.Suppliers.ToList(),
                ArticleGroups = snapshot.ArticleGroups.ToList(),
                FabricationMethods = snapshot.FabricationMethods.ToList(),
                InspectionMethods = snapshot.InspectionMethods.ToList(),
                SourceItemTypes = snapshot.SourceItemTypes.ToList(),
                SourceItems = snapshot.SourceItems.ToList(),
                Customers = snapshot.Customers.ToList(),
            };
        }

        /// <summary>Same normalisation as LookupCacheService (trimmed, trailing slash
        /// stripped, lower-cased) so an in-memory snapshot's ServerUrl and a cache-loaded one
        /// compare equal for the same server.</summary>
        private static string NormalizeServerUrl(string url)
        {
            return (url ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
        }
    }
}

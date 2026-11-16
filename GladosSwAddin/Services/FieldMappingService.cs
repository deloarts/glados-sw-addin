using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Services;
using GladosSwAddin.Models;
using Newtonsoft.Json;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// The single consumer of GladosSwAddin.Models.FieldMapping.Defaults. Resolves which
    /// SolidWorks custom property backs a given backend field from whichever source is
    /// currently in effect for the configured server: a mapping just synced from it, a
    /// previously-synced one that's still valid for it, or the compiled-in defaults. Every
    /// other read/write path that needs a property name for a field goes through
    /// Resolve/ResolvePropertyName rather than reading FieldMapping.Defaults directly — see
    /// docs/FIELD_OWNERSHIP.md section 2.
    /// </summary>
    public class FieldMappingService
    {
        /// <summary>Bumped if the persisted JSON shape ever changes incompatibly — a stored
        /// blob from an older schema is treated the same as "nothing stored" rather than
        /// risking a mis-parsed mapping.</summary>
        private const int CurrentSchemaVersion = 1;

        private readonly SettingsService _settingsService;

        public FieldMappingService(SettingsService settingsService)
        {
            if (settingsService == null)
                throw new ArgumentNullException(nameof(settingsService));

            _settingsService = settingsService;
        }

        /// <summary>True while no server mapping is in effect for the currently configured
        /// server — never synced yet, or the stored copy belongs to a different server. The
        /// settings grid's provenance line switches on this.</summary>
        public bool IsDefaults => !TryGetValidStored(out _);

        /// <summary>UTC time of the last successful sync against the currently configured
        /// server, or null while IsDefaults is true.</summary>
        public DateTime? SyncedAt => TryGetValidStored(out var stored) ? (DateTime?)stored.SyncedAt : null;

        /// <summary>The server base URL the current mapping was synced from, or null while
        /// IsDefaults is true.</summary>
        public string ServerUrl => TryGetValidStored(out var stored) ? stored.ServerUrl : null;

        /// <summary>Looks up the mapping entry for a field, or null if this kind's current
        /// mapping (server-synced or defaults) has no entry for it.</summary>
        public FieldMappingEntry Resolve(ItemKind kind, ItemField field)
        {
            var entries = EffectiveEntries(kind);
            return entries.TryGetValue(field.ToWireName(), out var entry) ? entry : null;
        }

        /// <summary>Shorthand for Resolve(kind, field)?.PropertyName — what almost every call
        /// site actually wants. Null both when the field has no entry at all and when it has
        /// one with no backing property (e.g. a Web-owned field): either way, there is nothing
        /// to read or write, which is exactly what "unmapped = never written" means in
        /// practice for a caller.</summary>
        public string ResolvePropertyName(ItemKind kind, ItemField field)
        {
            return Resolve(kind, field)?.PropertyName;
        }

        /// <summary>Whether ingest treats <paramref name="field"/> as required for
        /// <paramref name="kind"/> — the locked identity fields plus whatever the admin has
        /// additionally ticked. False when the field has no entry at all, the same "unmapped,
        /// no opinion" reading ResolvePropertyName gives. GladosTaskPaneControl's required-field
        /// markers are the only intended caller.</summary>
        public bool IsRequired(ItemKind kind, ItemField field)
        {
            return Resolve(kind, field)?.Required ?? false;
        }

        /// <summary>The pseudo-rule required fields configured for <paramref name="kind"/> —
        /// docs/FIELD_OWNERSHIP.md's "required fields" paragraph, today only
        /// RequiredFieldRuleName.FabricationStep, made items only. Empty before the first sync
        /// (FieldMapping.DefaultRules) or on an older backend that never sent a `rules`
        /// map.</summary>
        public IReadOnlyList<RequiredFieldRule> Rules(ItemKind kind)
        {
            if (TryGetValidStored(out var stored)
                && stored.Rules != null
                && stored.Rules.TryGetValue(kind.ToWireValue(), out var rules)
                && rules != null)
                return rules;

            return FieldMapping.DefaultRules.TryGetValue(kind, out var defaults)
                ? defaults
                : new List<RequiredFieldRule>();
        }

        /// <summary>Whether the pseudo-rule named <paramref name="ruleName"/> is turned on for
        /// <paramref name="kind"/> — false both when the rule isn't in the current rules list at
        /// all and when it's there but not required, the same "not configured, no opinion"
        /// reading IsRequired gives for a field. A name this add-in build has no evaluator for
        /// simply never gets asked about, per the tolerant-parsing convention the rest of the
        /// field mapping follows.</summary>
        public bool IsRuleRequired(ItemKind kind, string ruleName)
        {
            return Rules(kind).Any(
                r => r != null && r.Required && string.Equals(r.Name, ruleName, StringComparison.Ordinal));
        }

        /// <summary>The feature-tree template for made items — AdminPdmSettings.
        /// made_feature_tree_template synced from the backend, or FieldMapping.
        /// DefaultTemplates.Made before the first sync (or on an older backend that doesn't
        /// send templates yet). Written verbatim by
        /// SwCustomPropertyService.WriteFeatureTreeRepresentation.</summary>
        public string MadeFeatureTreeTemplate =>
            (TryGetValidStored(out var stored) ? stored.Templates?.Made : null) ?? FieldMapping.DefaultTemplates.Made;

        /// <summary>Bought-item analogue of MadeFeatureTreeTemplate.</summary>
        public string BoughtFeatureTreeTemplate =>
            (TryGetValidStored(out var stored) ? stored.Templates?.Bought : null)
            ?? FieldMapping.DefaultTemplates.Bought;

        /// <summary>Rows for the settings grid: fields FieldMapping.Defaults expects for this
        /// kind come first, in the defaults table's order, followed by any field the current
        /// mapping carries that this add-in build doesn't recognize. Unknown fields are
        /// included rather than dropped, per docs/FIELD_OWNERSHIP.md's tolerant-parsing rule
        /// — the grid greys them out via FieldMappingRow.IsKnown.</summary>
        public IReadOnlyList<FieldMappingRow> Entries(ItemKind kind)
        {
            var effective = EffectiveEntries(kind);
            var defaults = FieldMapping.Defaults[kind];
            var rows = new List<FieldMappingRow>();

            foreach (var known in defaults)
            {
                if (effective.TryGetValue(known.Field, out var entry))
                    rows.Add(new FieldMappingRow(kind, entry, isKnown: true));
            }

            var knownFields = new HashSet<string>(defaults.Select(d => d.Field), StringComparer.Ordinal);
            foreach (var entry in effective.Values)
            {
                if (!knownFields.Contains(entry.Field))
                    rows.Add(new FieldMappingRow(kind, entry, isKnown: false));
            }

            return rows;
        }

        /// <summary>
        /// Fetches the server's field mapping and overwrites the stored copy unconditionally
        /// — safe because the mapping is read-only in the add-in, so there's nothing local to
        /// lose. Never throws: a 404 (an add-in build talking to a backend that predates this
        /// endpoint) and any other failure just leave the stored mapping or compiled-in
        /// defaults serving requests. A successful Glados connection must not depend on this
        /// succeeding — call it after connecting, not as part of connecting.
        /// </summary>
        public async Task SyncAsync(FieldMappingApiService api)
        {
            if (api == null)
                throw new ArgumentNullException(nameof(api));

            var serverUrl = NormalizeServerUrl(_settingsService.Load().ServerUrl);

            try
            {
                var response = await api.GetFieldMappingAsync().ConfigureAwait(false);

                Persist(
                    serverUrl,
                    DateTime.UtcNow,
                    response?.MadeItems ?? new List<FieldMappingEntry>(),
                    response?.BoughtItems ?? new List<FieldMappingEntry>(),
                    response?.Templates,
                    response?.Rules);
            }
            catch (GladosApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // A backend older than this endpoint — see docs/FIELD_OWNERSHIP.md section 1
                // row 10. Expected on a mixed-version fleet, not an error.
                Log.Info("Field-mapping endpoint not found on this server; keeping stored mapping/defaults");
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Warning("Field-mapping sync failed, keeping stored mapping/defaults", ex);
            }
        }

        private Dictionary<string, FieldMappingEntry> EffectiveEntries(ItemKind kind)
        {
            if (TryGetValidStored(out var stored))
                return ToDictionary(kind == ItemKind.Made ? stored.MadeItems : stored.BoughtItems);

            return ToDictionary(FieldMapping.Defaults[kind]);
        }

        /// <summary>The stored mapping, if there is one and its recorded server URL still
        /// matches the currently configured one — a server switch invalidates it back to
        /// defaults rather than resolving property names that belonged to a different
        /// backend.</summary>
        private bool TryGetValidStored(out StoredFieldMapping stored)
        {
            stored = TryLoadStored();
            return stored != null
                && UrlsMatch(stored.ServerUrl, _settingsService.Load().ServerUrl);
        }

        private StoredFieldMapping TryLoadStored()
        {
            var json = _settingsService.LoadFieldMappingJson();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var stored = JsonConvert.DeserializeObject<StoredFieldMapping>(json, GladosApiClient.JsonSettings);
                return stored != null && stored.SchemaVersion == CurrentSchemaVersion ? stored : null;
            }
            catch (JsonException ex)
            {
                Log.Warning("Could not parse the stored field mapping, falling back to defaults", ex);
                return null;
            }
        }

        private void Persist(
            string serverUrl,
            DateTime syncedAtUtc,
            List<FieldMappingEntry> madeItems,
            List<FieldMappingEntry> boughtItems,
            FeatureTreeTemplates templates,
            Dictionary<string, List<RequiredFieldRule>> rules)
        {
            var stored = new StoredFieldMapping
            {
                SchemaVersion = CurrentSchemaVersion,
                ServerUrl = serverUrl,
                SyncedAt = syncedAtUtc,
                MadeItems = madeItems,
                BoughtItems = boughtItems,
                Templates = templates,
                Rules = rules,
            };

            _settingsService.SaveFieldMappingJson(
                JsonConvert.SerializeObject(stored, GladosApiClient.JsonSettings));
        }

        private static Dictionary<string, FieldMappingEntry> ToDictionary(IEnumerable<FieldMappingEntry> entries)
        {
            var dict = new Dictionary<string, FieldMappingEntry>(StringComparer.Ordinal);
            foreach (var entry in entries ?? Enumerable.Empty<FieldMappingEntry>())
            {
                if (!string.IsNullOrEmpty(entry?.Field))
                    dict[entry.Field] = entry;
            }
            return dict;
        }

        private static bool UrlsMatch(string a, string b)
        {
            return string.Equals(NormalizeServerUrl(a), NormalizeServerUrl(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mirrors the trim/trailing-slash handling GladosApiClient applies when it
        /// builds the HttpClient's BaseAddress, so a stored "https://glados.example.com/" and
        /// a configured "https://glados.example.com" are recognized as the same server.</summary>
        private static string NormalizeServerUrl(string url)
        {
            return (url ?? string.Empty).Trim().TrimEnd('/');
        }

        /// <summary>JSON shape persisted via SettingsService (a single user-settings blob, not
        /// the %LOCALAPPDATA% cache LookupCacheService uses — this is small, user-scoped
        /// configuration state, not a best-effort offline fallback): schema version, the
        /// server it came from, when, and the entries for both kinds.</summary>
        private class StoredFieldMapping
        {
            public int SchemaVersion { get; set; }

            public string ServerUrl { get; set; }

            public DateTime SyncedAt { get; set; }

            public List<FieldMappingEntry> MadeItems { get; set; }

            public List<FieldMappingEntry> BoughtItems { get; set; }

            /// <summary>The two feature-tree templates synced from the backend
            /// (docs/FIELD_OWNERSHIP.md section 1 row 3) — null when nothing has synced yet or
            /// the stored copy predates this field, treated the same as "use the compiled-in
            /// defaults" by MadeFeatureTreeTemplate/BoughtFeatureTreeTemplate.</summary>
            public FeatureTreeTemplates Templates { get; set; }

            /// <summary>Pseudo-rule required fields synced from the backend, keyed "made"/
            /// "bought" (docs/FIELD_OWNERSHIP.md's "required fields" paragraph) — null when
            /// nothing has synced yet or the stored copy predates this field, treated the same
            /// as "no rules" by Rules' fallback to FieldMapping.DefaultRules.</summary>
            public Dictionary<string, List<RequiredFieldRule>> Rules { get; set; }
        }
    }

    /// <summary>One row for the settings grid — see FieldMappingService.Entries.</summary>
    public class FieldMappingRow
    {
        public ItemKind Kind { get; }

        public FieldMappingEntry Entry { get; }

        /// <summary>False when the server sent this field but this add-in build has no
        /// ItemField member (and no FieldMapping.Defaults row) for it — shown, per
        /// docs/FIELD_OWNERSHIP.md's tolerant-parsing rule, but greyed in the grid.</summary>
        public bool IsKnown { get; }

        public FieldMappingRow(ItemKind kind, FieldMappingEntry entry, bool isKnown)
        {
            Kind = kind;
            Entry = entry;
            IsKnown = isKnown;
        }
    }
}

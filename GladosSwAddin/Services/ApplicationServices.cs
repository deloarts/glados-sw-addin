using GladosSwAddin.Api;

namespace GladosSwAddin.Services
{
    public static class ApplicationServices
    {
        private static GladosApi _api;

        public static SettingsService Settings { get; } = new SettingsService();

        public static CredentialService Credentials { get; } = new CredentialService();

        public static LookupCacheService LookupCache { get; } = new LookupCacheService();

        /// <summary>Owns the offline lookup snapshot (Manufacturer/Article Group/Fabrication
        /// Method/Inspection Method/Source Item Type/Source Item) fed by GET /pat/lookups — see
        /// docs/FIELD_OWNERSHIP.md row 11. Depends on LookupCache/Settings for the same reason
        /// FieldMapping does, so it's constructed after both.</summary>
        public static LookupService Lookups { get; } = new LookupService(LookupCache, Settings);

        /// <summary>Resolves which SolidWorks custom property backs a given backend field —
        /// see docs/FIELD_OWNERSHIP.md section 2. Depends on Settings for the same reason
        /// CreateApi() does (the currently configured server), so it's constructed after it.</summary>
        public static FieldMappingService FieldMapping { get; } = new FieldMappingService(Settings);

        /// <summary>
        /// Facade over the Glados backend API, built from the current server URL
        /// and PAT. Lazily created; call RefreshApi() after either changes
        /// (e.g. once SettingsForm saves).
        /// </summary>
        public static GladosApi Api => _api ?? (_api = CreateApi());

        public static void RefreshApi()
        {
            _api?.Dispose();
            _api = CreateApi();
        }

        private static GladosApi CreateApi()
        {
            return new GladosApi(Settings.Load().ServerUrl, Credentials.GetPat());
        }
    }
}

using System;
using GladosSwAddin.Models;

namespace GladosSwAddin.Services
{
    public class SettingsService
    {
        public Settings Load()
        {
            return new Settings
            {
                ServerUrl = Properties.Settings.Default.ServerUrl ?? string.Empty,
            };
        }

        public void Save(Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Properties.Settings.Default.ServerUrl = settings.ServerUrl?.Trim() ?? string.Empty;
            Properties.Settings.Default.Save();
        }

        public void Reset()
        {
            Properties.Settings.Default.Reset();
            Properties.Settings.Default.Save();
        }

        /// <summary>Raw JSON blob backing FieldMappingService's persisted mapping — FieldMappingService
        /// owns the shape (schema version, server URL, synced_at, entries), this just stores it
        /// the same way ServerUrl is stored, and not in LookupCacheService's %LOCALAPPDATA% cache: it's
        /// small, user-scoped configuration state, not a best-effort offline fallback.</summary>
        public string LoadFieldMappingJson()
        {
            return Properties.Settings.Default.FieldMappingJson ?? string.Empty;
        }

        public void SaveFieldMappingJson(string json)
        {
            Properties.Settings.Default.FieldMappingJson = json ?? string.Empty;
            Properties.Settings.Default.Save();
        }
    }
}

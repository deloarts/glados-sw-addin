using System;
using System.Collections.Generic;

namespace GladosSwAddin.Api.Models
{
    /// <summary>Mirrors UserResponse in src/modules/platform/adapters/inbound/api/schemas/user_schema.py.</summary>
    public class UserResponse
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; }

        public string Username { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public DateTime Created { get; set; }

        public Language Language { get; set; }

        public ThemePreset ThemePreset { get; set; }

        public ThemeSurface ThemeSurface { get; set; }

        public ThemePrimary ThemePrimary { get; set; }

        public bool ThemeDarkMode { get; set; }

        /// <summary>Whether an RFID tag is enrolled for this user. Never the hash itself
        /// (rfid_configured in user_schema.py, replacing the old hashed_rfid field).</summary>
        public bool RfidConfigured { get; set; }

        public bool IsActive { get; set; }

        public bool IsSystemuser { get; set; }

        public string Timezone { get; set; }

        public DateFormat DateFormat { get; set; }

        public string AuthProvider { get; set; }

        public List<int> MemberIds { get; set; }

        public List<string> Permissions { get; set; }

        public DateTime? LastLoggedIn { get; set; }

        public bool TotpEnabled { get; set; }

        public bool HasAvatar { get; set; }

        public bool IsSharedAccount { get; set; }

        public bool ButtonFlatStyle { get; set; }

        public bool TableGridlines { get; set; }

        public bool TableStripedRows { get; set; }
    }
}

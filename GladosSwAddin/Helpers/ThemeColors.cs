using System.Drawing;
using System.Globalization;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Helpers
{
    /// <summary>
    /// Maps a signed-in user's ThemePrimary preference (chosen in the Glados web frontend) to
    /// a WinForms accent Color, so the task pane's header/footer follow what the user actually
    /// picked instead of being hardcoded to one fixed blue. Mirrors AppHeader.vue's own header
    /// background exactly: `background-color: var(--p-primary-500)`, unconditionally — that
    /// rule has no `.p-dark` override, so unlike most PrimeVue Aura accent tokens the header
    /// does NOT switch to a lighter shade in dark mode, and neither does this. Hex values are
    /// the frontend's PrimeVue palette's 500 shade (see
    /// glados/frontend/src/core/config/colorPalettes.ts, PRIMARY_COLORS).
    /// </summary>
    public static class ThemeColors
    {
        public static Color GetPrimaryAccent(ThemePrimary primary)
        {
            switch (primary)
            {
                case ThemePrimary.Glados:
                    return FromHex("#007acc");
                case ThemePrimary.Noir:
                    // Not defined in colorPalettes.ts (not currently offered as a primary
                    // option in the frontend UI) — best-effort near-black accent.
                    return FromHex("#18181b");
                case ThemePrimary.Soul:
                    return FromHex("#27272a");
                case ThemePrimary.Emerald:
                    return FromHex("#10b981");
                case ThemePrimary.Green:
                    return FromHex("#22c55e");
                case ThemePrimary.Lime:
                    return FromHex("#84cc16");
                case ThemePrimary.Red:
                    // Not defined in colorPalettes.ts — best-effort standard Tailwind red.
                    return FromHex("#ef4444");
                case ThemePrimary.Orange:
                    return FromHex("#ec7404");
                case ThemePrimary.Amber:
                    return FromHex("#f59e0b");
                case ThemePrimary.Yellow:
                    return FromHex("#eab308");
                case ThemePrimary.Teal:
                    return FromHex("#14b8a6");
                case ThemePrimary.Cyan:
                    return FromHex("#06b6d4");
                case ThemePrimary.Sky:
                    return FromHex("#0ea5e9");
                case ThemePrimary.Blue:
                    return FromHex("#3b82f6");
                case ThemePrimary.Indigo:
                    return FromHex("#6366f1");
                case ThemePrimary.Violet:
                    return FromHex("#8b5cf6");
                case ThemePrimary.Purple:
                    return FromHex("#a855f7");
                case ThemePrimary.Fuchsia:
                    return FromHex("#d946ef");
                case ThemePrimary.Pink:
                    return FromHex("#ec4899");
                case ThemePrimary.Rose:
                    return FromHex("#f43f5e");
                default:
                    return FromHex("#007acc");
            }
        }

        private static Color FromHex(string hex)
        {
            var value = hex.TrimStart('#');
            int r = int.Parse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int g = int.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int b = int.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return Color.FromArgb(r, g, b);
        }
    }
}

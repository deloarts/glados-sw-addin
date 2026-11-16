using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace GladosSwAddin.Helpers
{
    /// <summary>
    /// The five dimension shapes SourceItemPickerForm's Create panel can compose, mirroring the
    /// web's SourceItemDimensionInput.vue shape selector (there: box/hollow-box/rod/hollow-rod/
    /// custom). Order matches the web's dropdown order and comboBoxCreatePreset's item order.
    /// </summary>
    public enum DimensionShape
    {
        /// <summary>Three interchangeable numbers — "{w} × {h} × {t}". Web: "box".</summary>
        Block,

        /// <summary>Four numbers, only the first two interchangeable —
        /// "{w} × {h} × {l} × {d}". Web: "hollow-box".</summary>
        Profile,

        /// <summary>Two numbers, never reordered — "Ø{d} × {l}". Web: "rod".</summary>
        TurnedPart,

        /// <summary>Three numbers, never reordered — "Ø{d} × {l} × {t}". Web: "hollow-rod".</summary>
        Tube,

        /// <summary>Free text, trimmed and whitespace-normalized. Never recognised by
        /// <see cref="DimensionSpec.TryParse"/> — an existing dimension either matches one of
        /// the four structured shapes above or it's Custom by exclusion.</summary>
        Custom
    }

    /// <summary>
    /// Composition and canonicalisation for SourceItem.dimension, mirroring
    /// glados/backend/src/modules/catalogue/domain/services/dimension_spec.py byte-for-byte (that service is the
    /// authoritative canonicalisation — this is a client-side mirror, same relationship the
    /// backend module already has with glados/frontend's dimensionSpec.ts) — a legacy dimension
    /// typed with an ASCII "x"/"*" or a lowercase "ø" still canonicalizes the same as a
    /// freshly-composed "×"/"Ø" string, which is what makes <see cref="AreEquivalent"/> a
    /// reliable duplicate check against LookupService.Current.SourceItems. Only a Block's three
    /// numbers are fully permutation-equivalent (100 × 200 × 20 ≡ 200 × 100 × 20) — a Profile
    /// only its first two (width/height; length and wall thickness are not interchangeable with
    /// them), and a Turned Part's or Tube's numbers are never reordered (diameter isn't
    /// interchangeable with length) — see ComposeBlock/ComposeProfile below and
    /// SourceItemDimensionInput.vue's normalizeBoxOrder/normalizeProfileOrder.
    /// </summary>
    public static class DimensionSpec
    {
        private const string NumberPattern = @"-?\d+(?:\.\d+)?";
        private const string SeparatorPattern = @"\s*[×xX*]\s*";
        private const string DiameterPattern = "[Øø]";

        private static readonly Regex TubeRegex = new Regex(
            $"^{DiameterPattern}({NumberPattern}){SeparatorPattern}({NumberPattern}){SeparatorPattern}({NumberPattern})$");
        private static readonly Regex TurnedPartRegex = new Regex(
            $"^{DiameterPattern}({NumberPattern}){SeparatorPattern}({NumberPattern})$");
        private static readonly Regex ProfileRegex = new Regex(
            $"^({NumberPattern}){SeparatorPattern}({NumberPattern}){SeparatorPattern}({NumberPattern}){SeparatorPattern}({NumberPattern})$");
        private static readonly Regex BlockRegex = new Regex(
            $"^({NumberPattern}){SeparatorPattern}({NumberPattern}){SeparatorPattern}({NumberPattern})$");

        /// <summary>Parses a dimension box's raw text as typed by the user — tried first against
        /// CurrentCulture (so a German "12,5" works) and, failing that, InvariantCulture (so a
        /// plain "12.5" always works regardless of UI culture) — mirroring how
        /// buttonCreateSubmit_Click already double-parses textBoxCreateMass.</summary>
        public static bool TryParseNumber(string text, out double value)
        {
            var trimmed = text?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                    return true;
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return true;
            }

            value = 0;
            return false;
        }

        /// <summary>Invariant formatting for the canonical wire value — "#.##########" rather
        /// than a fixed decimal count so trailing zeros never appear ("100" not "100.0000000000"),
        /// matching normalize_number's string-surgery result in the Python/TS mirrors without
        /// having to reproduce their regex-substring approach here.</summary>
        public static string FormatNumber(double value)
        {
            return value.ToString("0.##########", CultureInfo.InvariantCulture);
        }

        /// <summary>Composes a Block dimension, sorting all three numbers descending — see the
        /// class doc comment on why only Block gets full permutation equivalence.</summary>
        public static string ComposeBlock(double width, double height, double thickness)
        {
            var sorted = new[] { width, height, thickness }.OrderByDescending(v => v);
            return string.Join(" × ", sorted.Select(FormatNumber));
        }

        /// <summary>Composes a Profile dimension. Only width/height are sorted (descending);
        /// length and wall thickness keep the order the user entered them in.</summary>
        public static string ComposeProfile(double width, double height, double length, double wallThickness)
        {
            var sortedWidthHeight = new[] { width, height }.OrderByDescending(v => v).ToArray();
            return string.Join(
                " × ",
                new[] { sortedWidthHeight[0], sortedWidthHeight[1], length, wallThickness }.Select(FormatNumber));
        }

        /// <summary>Composes a Turned Part dimension — "Ø{diameter} × {length}", never reordered.</summary>
        public static string ComposeTurnedPart(double diameter, double length)
        {
            return $"Ø{FormatNumber(diameter)} × {FormatNumber(length)}";
        }

        /// <summary>Composes a Tube dimension — "Ø{diameter} × {length} × {wallThickness}",
        /// never reordered.</summary>
        public static string ComposeTube(double diameter, double length, double wallThickness)
        {
            return $"Ø{FormatNumber(diameter)} × {FormatNumber(length)} × {FormatNumber(wallThickness)}";
        }

        /// <summary>Trims and collapses internal whitespace runs to a single space — the only
        /// normalisation a Custom dimension gets.</summary>
        public static string ComposeCustom(string text)
        {
            return NormalizeWhitespace(text);
        }

        private static string NormalizeWhitespace(string text)
        {
            return text == null ? string.Empty : Regex.Replace(text.Trim(), @"\s+", " ");
        }

        /// <summary>Canonicalises an arbitrary dimension string — including legacy data typed
        /// with an ASCII "x"/"*" separator or a lowercase "ø" — into the same form
        /// <see cref="ComposeBlock"/>/<see cref="ComposeProfile"/>/<see cref="ComposeTurnedPart"/>/
        /// <see cref="ComposeTube"/> would produce. An unrecognised (Custom) string comes back
        /// whitespace-normalized but otherwise unchanged. Null in, null out; empty/whitespace-only
        /// also comes back null, mirroring canonicalize_dimension's None handling.</summary>
        public static string Canonicalize(string dimension)
        {
            if (dimension == null)
                return null;

            var trimmed = NormalizeWhitespace(dimension);
            if (trimmed.Length == 0)
                return null;

            var tube = TubeRegex.Match(trimmed);
            if (tube.Success)
            {
                return $"Ø{NormalizeNumberText(tube.Groups[1].Value)}"
                    + $" × {NormalizeNumberText(tube.Groups[2].Value)}"
                    + $" × {NormalizeNumberText(tube.Groups[3].Value)}";
            }

            var turnedPart = TurnedPartRegex.Match(trimmed);
            if (turnedPart.Success)
            {
                return $"Ø{NormalizeNumberText(turnedPart.Groups[1].Value)}"
                    + $" × {NormalizeNumberText(turnedPart.Groups[2].Value)}";
            }

            var profile = ProfileRegex.Match(trimmed);
            if (profile.Success)
            {
                return string.Join(
                    " × ",
                    new[]
                    {
                        profile.Groups[1].Value, profile.Groups[2].Value,
                        profile.Groups[3].Value, profile.Groups[4].Value,
                    }.Select(NormalizeNumberText));
            }

            var block = BlockRegex.Match(trimmed);
            if (block.Success)
            {
                var numbers = new[] { block.Groups[1].Value, block.Groups[2].Value, block.Groups[3].Value }
                    .Select(NormalizeNumberText)
                    .OrderByDescending(n => double.Parse(n, CultureInfo.InvariantCulture));
                return string.Join(" × ", numbers);
            }

            return trimmed;
        }

        /// <summary>String surgery rather than a parse/format round-trip, so a huge or
        /// oddly-precise legacy value can't drift — "100.0" -&gt; "100", "12.50" -&gt; "12.5".
        /// Mirrors normalizeNumber/_normalize_number in the frontend/backend mirrors exactly.</summary>
        private static string NormalizeNumberText(string raw)
        {
            if (!raw.Contains("."))
                return raw;

            var stripped = raw.TrimEnd('0');
            return stripped.EndsWith(".", StringComparison.Ordinal)
                ? stripped.Substring(0, stripped.Length - 1)
                : stripped;
        }

        /// <summary>True when both dimensions canonicalise to the same string — the duplicate
        /// check SourceItemPickerForm runs against LookupService.Current.SourceItems.</summary>
        public static bool AreEquivalent(string a, string b)
        {
            return string.Equals(Canonicalize(a), Canonicalize(b), StringComparison.Ordinal);
        }

        /// <summary>Recognises an existing dimension string as one of the four structured shapes
        /// (widened separator/diameter classes, same as <see cref="Canonicalize"/>) — used both
        /// for base-size prefill (SourceItemPickerForm.PrefillCreateForm) and, via
        /// <see cref="Canonicalize"/>, for duplicate matching. Returns false for Custom/
        /// unrecognised/empty input, same as the frontend's detectDimensionShape returning null —
        /// <paramref name="numbers"/> is then null and <paramref name="shape"/> undefined.</summary>
        public static bool TryParse(string dimension, out DimensionShape shape, out double[] numbers)
        {
            shape = DimensionShape.Custom;
            numbers = null;

            if (string.IsNullOrWhiteSpace(dimension))
                return false;

            var trimmed = NormalizeWhitespace(dimension);

            var tube = TubeRegex.Match(trimmed);
            if (tube.Success)
            {
                shape = DimensionShape.Tube;
                numbers = ParseGroups(tube, 1, 2, 3);
                return true;
            }

            var turnedPart = TurnedPartRegex.Match(trimmed);
            if (turnedPart.Success)
            {
                shape = DimensionShape.TurnedPart;
                numbers = ParseGroups(turnedPart, 1, 2);
                return true;
            }

            var profile = ProfileRegex.Match(trimmed);
            if (profile.Success)
            {
                shape = DimensionShape.Profile;
                numbers = ParseGroups(profile, 1, 2, 3, 4);
                return true;
            }

            var block = BlockRegex.Match(trimmed);
            if (block.Success)
            {
                shape = DimensionShape.Block;
                numbers = ParseGroups(block, 1, 2, 3);
                return true;
            }

            return false;
        }

        private static double[] ParseGroups(Match match, params int[] groupIndexes)
        {
            return groupIndexes
                .Select(i => double.Parse(match.Groups[i].Value, CultureInfo.InvariantCulture))
                .ToArray();
        }
    }
}

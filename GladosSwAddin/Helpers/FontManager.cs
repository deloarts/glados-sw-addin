using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GladosSwAddin.Helpers
{
    /// <summary>
    /// Loads the embedded Audiowide, Material Icons and Play fonts into one process-wide
    /// PrivateFontCollection and hands out Font instances by family name. Each font's bytes are
    /// copied into unmanaged memory that is deliberately never freed: GDI+'s AddMemoryFont keeps
    /// referencing that buffer for as long as the collection lives, and the collection lives as
    /// long as the process. Freeing it straight after the call breaks that contract and
    /// risks garbled glyphs or access violations whenever GDI+ re-reads the font data.
    /// </summary>
    public static class FontManager
    {
        private static readonly PrivateFontCollection _fonts = new PrivateFontCollection();

        private static readonly Dictionary<string, FontFamily> _fontFamilies = new Dictionary<
            string,
            FontFamily
        >(StringComparer.OrdinalIgnoreCase);

        // Keeps every AddMemoryFont buffer reachable for the collection's (= the process's)
        // lifetime; see the class summary.
        private static readonly List<IntPtr> _fontMemory = new List<IntPtr>();

        private static bool _initialized;

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;

            LoadFont("GladosSwAddin.Resources.Fonts.Audiowide-Regular.ttf");
            LoadFont("GladosSwAddin.Resources.Fonts.MaterialIcons-Regular.ttf");
            LoadFont("GladosSwAddin.Resources.Fonts.Play-Regular.ttf");
        }

        private static void LoadFont(string resourceName)
        {
            Assembly assembly = typeof(FontManager).Assembly;
            byte[] fontData;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Could not find embedded font: {resourceName}");

                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    fontData = memoryStream.ToArray();
                }
            }

            // PrivateFontCollection.Families is returned alphabetically, not in insertion
            // order — diff the family names before/after adding rather than assuming the
            // newly-added font ends up last.
            var namesBefore = new HashSet<string>(
                Array.ConvertAll(_fonts.Families, f => f.Name),
                StringComparer.OrdinalIgnoreCase
            );

            IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
            Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
            _fonts.AddMemoryFont(fontPtr, fontData.Length);
            _fontMemory.Add(fontPtr);

            FontFamily family = Array.Find(_fonts.Families, f => !namesBefore.Contains(f.Name));
            if (family == null)
                throw new InvalidOperationException($"Could not resolve loaded font family for: {resourceName}");

            _fontFamilies[family.Name] = family;
        }

        public static Font GetFont(string fontName, float size, FontStyle style = FontStyle.Regular)
        {
            EnsureInitialized();

            if (!_fontFamilies.TryGetValue(fontName, out FontFamily family))
                throw new InvalidOperationException($"Font '{fontName}' is not loaded.");

            return new Font(family, size, style, GraphicsUnit.Point);
        }
    }
}

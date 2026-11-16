using System.Linq;
using GladosSwAddin.Api.Models;

namespace GladosSwAddin.Helpers
{
    /// <summary>
    /// Composes the source-item spec line shared by the made-item tab's read-only display and
    /// the picker's grid/detail panel (Forms/GladosTaskPaneControl.cs,
    /// Forms/SourceItemPickerForm.cs) and used as the derived Glados.SourceItemName label written
    /// at Save (see Services/SwCustomPropertyService.WriteSourceItem and
    /// GladosTaskPaneControl.ResolveSourceItemLabel) — one helper so all three read the exact
    /// same text: "&lt;type_name&gt; &lt;dimension&gt; &lt;material&gt;", empty parts skipped,
    /// single spaces between the rest. Never localized — like Glados.ProcessNames, the composed
    /// text is stored in the document and shown verbatim regardless of the running UI culture.
    /// </summary>
    public static class SourceItemLabelComposer
    {
        public static string Compose(string typeName, string dimension, string material)
        {
            return string.Join(
                " ",
                new[] { typeName, dimension, material }
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .Select(part => part.Trim()));
        }

        /// <summary>Shorthand for Compose(item.TypeName, item.Dimension, item.Material). Empty
        /// (never null) for a null <paramref name="item"/> so callers can compose straight off a
        /// possibly-missing lookup result without an extra null check.</summary>
        public static string Compose(SourceItemResponse item)
        {
            return item == null ? string.Empty : Compose(item.TypeName, item.Dimension, item.Material);
        }
    }
}

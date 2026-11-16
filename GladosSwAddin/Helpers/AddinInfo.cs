using System.Reflection;

namespace GladosSwAddin.Helpers
{
    /// <summary>
    /// The add-in's identity as users see it: the display name and description SOLIDWORKS'
    /// Add-Ins dialog shows, the task pane tooltip, and the release version shown in Settings.
    /// Not localized: the name is a product name, and the Add-Ins dialog reads its registry text
    /// before the add-in (and its UI culture) is loaded. The installer (Package.wxs) and the
    /// assembly attributes (AssemblyInfo.cs) restate the same name and description because they
    /// cannot reference a C# constant, so a rename touches exactly those three files.
    /// </summary>
    internal static class AddinInfo
    {
        internal const string DisplayName = "GLADOS SOLIDWORKS Add-in";

        internal const string Description =
            "Compares and edits GLADOS catalogue properties and checks documents before PDM release";

        /// <summary>The release number (AssemblyInformationalVersion, e.g. "0.1.0"), not
        /// AssemblyVersion: the latter is restated in the installer's COM rows and is only ever
        /// moved together with them, so it is not what users should quote.</summary>
        internal static string Version
        {
            get
            {
                var attribute = (AssemblyInformationalVersionAttribute)System.Attribute.GetCustomAttribute(
                    typeof(AddinInfo).Assembly, typeof(AssemblyInformationalVersionAttribute));
                return attribute?.InformationalVersion ?? string.Empty;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Finds the files a document references (3D Interconnect sources, derived-part parents,
    /// assembly components) that live in a Downloads folder — links that resolve on the machine
    /// that made them and nowhere else once the document is released to PDM. Reads SolidWorks'
    /// own reference list (ModelDoc2.GetDependencies2, what File › Find References shows) rather
    /// than walking the feature tree, because a 3D Interconnect feature exposes no source path of
    /// its own and the reference list covers every other kind of link with the same check.
    /// "A Downloads folder" means the current user's (resolved as a Windows known folder, so a
    /// redirected one counts) or any other profile's beside it: the link may have been made by a
    /// colleague. Read-only, and like every other check here it never throws — a SolidWorks
    /// failure is logged and reported as "nothing found".
    /// </summary>
    public static class DownloadsReferenceService
    {
        private const string DownloadsFolderName = "Downloads";

        /// <summary>FOLDERID_Downloads.</summary>
        private static readonly Guid DownloadsKnownFolderId = new Guid("374DE290-123F-4565-9164-B4428DF91FCA");

        /// <summary>The full paths of every file <paramref name="model"/> directly references
        /// that lies in a Downloads folder, in SolidWorks' order without duplicates; empty when
        /// there are none or the references can't be read.</summary>
        public static IList<string> FindDownloadsReferences(ModelDoc2 model)
        {
            if (model == null)
                return new List<string>();

            object dependencies;
            try
            {
                // Direct references only, as stored in the document (no search-path
                // resolution): a sub-assembly's own links are checked when that document is.
                dependencies = model.GetDependencies2(false, false, false);
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Warning("Document references could not be read", ex);
                return new List<string>();
            }

            var downloadsFolder = ResolveDownloadsFolder();
            var profilesRoot = ResolveProfilesRoot();
            return ParseDependencyPaths(dependencies)
                .Where(path => IsInDownloadsFolder(path, downloadsFolder, profilesRoot))
                .ToList();
        }

        /// <summary>The full paths out of GetDependencies2's result, which alternates file
        /// name and full path (the latter at odd indices); null (no references) or anything
        /// that isn't a string array yields none. Blank entries are skipped and repeats
        /// dropped, ignoring case as Windows paths do.</summary>
        internal static IList<string> ParseDependencyPaths(object dependencies)
        {
            var paths = new List<string>();
            if (!(dependencies is string[] entries))
                return paths;

            for (var i = 1; i < entries.Length; i += 2)
            {
                var path = entries[i];
                if (!string.IsNullOrWhiteSpace(path)
                    && !paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    paths.Add(path);
            }

            return paths;
        }

        /// <summary>True when <paramref name="path"/> lies anywhere below
        /// <paramref name="downloadsFolder"/>, or below "Downloads" directly inside any profile
        /// under <paramref name="profilesRoot"/> (e.g. C:\Users\someone\Downloads). Either folder
        /// may be null when it couldn't be resolved, and that rule is then skipped. Pure string
        /// comparison, ignoring case and slash direction — nothing here touches the file
        /// system, since the referenced file may not exist on this machine at all.</summary>
        internal static bool IsInDownloadsFolder(string path, string downloadsFolder, string profilesRoot)
        {
            var normalizedPath = NormalizePath(path);
            if (normalizedPath == null)
                return false;

            var normalizedDownloads = NormalizePath(downloadsFolder);
            if (normalizedDownloads != null && IsBelow(normalizedPath, normalizedDownloads))
                return true;

            var normalizedProfiles = NormalizePath(profilesRoot);
            if (normalizedProfiles == null || !IsBelow(normalizedPath, normalizedProfiles))
                return false;

            // <profilesRoot>\<profile>\Downloads\<at least one more segment>
            var segments = normalizedPath.Substring(normalizedProfiles.Length + 1).Split('\\');
            return segments.Length >= 3
                && string.Equals(segments[1], DownloadsFolderName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBelow(string path, string folder)
        {
            return path.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var normalized = path.Trim().Replace('/', '\\').TrimEnd('\\');
            return normalized.Length == 0 ? null : normalized;
        }

        /// <summary>The current user's Downloads folder wherever it has been redirected to, or
        /// null if Windows can't say.</summary>
        private static string ResolveDownloadsFolder()
        {
            var folderId = DownloadsKnownFolderId;
            var pathPointer = IntPtr.Zero;
            try
            {
                var result = SHGetKnownFolderPath(ref folderId, 0, IntPtr.Zero, out pathPointer);
                return result == 0 ? Marshal.PtrToStringUni(pathPointer) : null;
            }
            finally
            {
                // Freed even on failure, as SHGetKnownFolderPath requires.
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }

        /// <summary>The folder holding every user profile (normally C:\Users), or null if the
        /// current profile's location is unknown.</summary>
        private static string ResolveProfilesRoot()
        {
            var profile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(profile) ? null : Path.GetDirectoryName(profile);
        }

        [DllImport("shell32.dll", SetLastError = false)]
        private static extern int SHGetKnownFolderPath(
            ref Guid folderId, uint flags, IntPtr token, out IntPtr path);
    }
}

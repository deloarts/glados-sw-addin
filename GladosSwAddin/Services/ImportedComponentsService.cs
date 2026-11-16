using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace GladosSwAddin.Services
{
    /// <summary>How an EmbedImportedSubassemblies run ended.</summary>
    public enum EmbedImportedComponentsOutcome
    {
        Embedded,
        NothingToEmbed,
        ReadOnly,
        Failed,
    }

    /// <summary>The step an EmbedImportedSubassemblies run was on, so a failure can say where
    /// it stopped.</summary>
    public enum EmbedImportedComponentsStep
    {
        SaveAssembly,
        BreakLink,
        SaveToWorkFolder,
        Dissolve,
        MakeVirtual,
    }

    /// <summary>What an EmbedImportedSubassemblies run did. <see cref="FailedStep"/> applies
    /// only to <see cref="EmbedImportedComponentsOutcome.Failed"/>; <see cref="NotCheckedIn"/>
    /// lists the imported source files whose link was broken but that couldn't be checked in
    /// to PDM, whatever the outcome.</summary>
    public sealed class EmbedImportedComponentsResult
    {
        public EmbedImportedComponentsOutcome Outcome { get; set; }

        public EmbedImportedComponentsStep FailedStep { get; set; }

        public int EmbeddedCount { get; set; }

        public IList<string> NotCheckedIn { get; set; } = new List<string>();
    }

    /// <summary>
    /// Turns the 3D Interconnect subassemblies of a bought assembly (a downloaded STEP
    /// inserted as a component) into virtual parts of that assembly, so mates can make it move
    /// while it stays one file, and one BOM row, in PDM. A 3D Interconnect subassembly is rigid
    /// and its components only exist in memory once the link is broken, and Make Virtual needs a
    /// file to copy, so the run follows the manual workaround step by step: break the link,
    /// save every component into a private work folder under %TEMP%, dissolve the subassembly,
    /// make the components it leaves behind virtual, and rename them back from the
    /// "Copy of …" name SolidWorks gives a virtual copy.
    /// Once the link is broken, PDM no longer counts the imported file (the STEP) as a
    /// reference of the assembly and would leave it checked out when the assembly is checked
    /// in, so the run checks it in itself (PdmCheckInService) — even when a later step fails,
    /// since the link stays broken either way.
    /// The assembly is rebuilt and, when it has unsaved changes, saved once before the link is
    /// broken — Break Link fails on an assembly whose latest state isn't on disk, and the saved
    /// file is then exactly the state to go back to. It is never saved afterwards: the user
    /// checks the result and saves, or closes without saving to undo the whole run. For the
    /// same reason the work folder isn't deleted right away — SolidWorks may still hold its
    /// files until then — but by PurgeStaleWorkFolders at a later start.
    /// </summary>
    public static class ImportedComponentsService
    {
        private const string FallbackComponentName = "Component";

        /// <summary>A run's work folder is left alone for this long, so a SolidWorks session
        /// that is still using it isn't pulled from under by another one starting.</summary>
        private static readonly TimeSpan WorkFolderLifetime = TimeSpan.FromDays(1);

        private static readonly string[] NativeExtensions = { ".sldprt", ".sldasm" };

        private static string WorkRoot =>
            Path.Combine(Path.GetTempPath(), "GladosSwAddin", "ImportedComponents");

        /// <summary>Runs the whole conversion on <paramref name="model"/> — see the class
        /// summary. <paramref name="app"/> is needed only to switch SolidWorks' "update
        /// component names" option off while renaming (and back afterwards); null skips the
        /// rename. <paramref name="checkInComment"/> is the PDM version comment for the imported
        /// files and <paramref name="parentWindow"/> parents a PDM login dialog, should one be
        /// needed. Never throws: a SolidWorks failure is logged and reported as Failed with the
        /// step it happened in; past SaveAssembly that leaves the document modified but
        /// unsaved.</summary>
        public static EmbedImportedComponentsResult EmbedImportedSubassemblies(
            ISldWorks app, ModelDoc2 model, string checkInComment, int parentWindow)
        {
            var sourceFiles = new List<string>();
            var linkBroken = false;
            var result = Embed(app, model, sourceFiles, ref linkBroken);
            if (linkBroken)
                result.NotCheckedIn = CheckInSourceFiles(sourceFiles, checkInComment, parentWindow);
            return result;
        }

        /// <summary>The conversion itself. Fills <paramref name="sourceFiles"/> with the
        /// imported files' paths before breaking any link and sets
        /// <paramref name="linkBroken"/> once one is broken, so the caller knows what to check
        /// in whichever way this returns.</summary>
        private static EmbedImportedComponentsResult Embed(
            ISldWorks app, ModelDoc2 model, List<string> sourceFiles, ref bool linkBroken)
        {
            var assembly = model as AssemblyDoc;
            if (assembly == null)
                return Outcome(EmbedImportedComponentsOutcome.NothingToEmbed);

            if (model.IsOpenedReadOnly())
                return Outcome(EmbedImportedComponentsOutcome.ReadOnly);

            var step = EmbedImportedComponentsStep.SaveAssembly;
            try
            {
                var importedNames = FindImportedSubassemblyNames(assembly);
                if (importedNames.Count == 0)
                    return Outcome(EmbedImportedComponentsOutcome.NothingToEmbed);

                model.EditRebuild3();
                if (model.GetSaveFlag())
                {
                    int saveErrors = 0;
                    int saveWarnings = 0;
                    if (!model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings))
                        return Failed(step, string.Format(
                            CultureInfo.InvariantCulture, "Save3 failed with error {0}", saveErrors));
                }

                step = EmbedImportedComponentsStep.BreakLink;
                // Break Link, and later the save into the work folder, rename the imported
                // components, so from here on they are found as "every top-level component
                // that isn't one of the others" rather than by name.
                var otherNames = new HashSet<string>(
                    TopLevelComponents(assembly).Select(component => component.Name2).Except(importedNames),
                    StringComparer.OrdinalIgnoreCase);

                sourceFiles.AddRange(TopLevelComponents(assembly)
                    .Where(component => importedNames.Contains(component.Name2))
                    .Select(component => component.GetImportedPath())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase));

                foreach (var name in importedNames)
                {
                    var feature = assembly.FeatureByName(name) as Feature;
                    var error = feature?.BreakLink(true, true) ?? -1;
                    if (error != (int)sw3DInterconnectImportErrors_e.sw3DInterconnectImportErrors_None)
                        return Failed(step, string.Format(
                            CultureInfo.InvariantCulture, "BreakLink on {0} returned {1}", name, error));
                    linkBroken = true;
                }

                model.EditRebuild3();

                step = EmbedImportedComponentsStep.SaveToWorkFolder;
                var unlinked = ComponentsOtherThan(assembly, otherNames);
                if (unlinked.Count == 0)
                    return Failed(step, "no imported component left after BreakLink");

                var workFolder = Path.Combine(WorkRoot, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(workFolder);
                var savedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var component in unlinked)
                {
                    var error = SaveTreeToWorkFolder(component, workFolder, savedTitles);
                    if (error != null)
                        return Failed(step, error);
                }

                step = EmbedImportedComponentsStep.Dissolve;
                // Collected before the first dissolve: dissolving puts the children at the top
                // level, where they too are "other than" — and must not be dissolved in turn.
                foreach (var component in ComponentsOtherThan(assembly, otherNames))
                {
                    var name = component.Name2;
                    model.ClearSelection2(true);
                    if (!component.Select4(false, null, false) || !assembly.DissolveSubAssembly())
                        return Failed(step, "DissolveSubAssembly failed for " + name);
                }

                step = EmbedImportedComponentsStep.MakeVirtual;
                // Each component's wanted name is its work-folder file name — the component's
                // own title, made file-safe — read before Make Virtual moves it into the assembly.
                var embedded = TopLevelComponents(assembly)
                    .Select(component => new KeyValuePair<Component2, string>(component, component.GetPathName()))
                    .Where(pair => IsInFolder(pair.Value, workFolder))
                    .Select(pair => new KeyValuePair<Component2, string>(
                        pair.Key, Path.GetFileNameWithoutExtension(pair.Value)))
                    .ToList();

                foreach (var pair in embedded)
                {
                    // Instances of one file share one virtual copy: the first call embeds them all.
                    if (!pair.Key.IsVirtual && !pair.Key.MakeVirtual2(true))
                        return Failed(step, "MakeVirtual2 failed for " + pair.Value);
                }

                RestoreComponentNames(app, model, embedded);

                model.ClearSelection2(true);
                model.EditRebuild3();
                model.SetSaveFlag();

                Log.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "Embedded {0} imported component(s) as virtual components of {1}",
                    embedded.Count, model.GetPathName()));
                return new EmbedImportedComponentsResult
                {
                    Outcome = EmbedImportedComponentsOutcome.Embedded,
                    EmbeddedCount = embedded.Count,
                };
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Warning("Embedding imported components failed at " + step, ex);
                return Outcome(EmbedImportedComponentsOutcome.Failed, step);
            }
        }

        /// <summary>Checks each of <paramref name="sourceFiles"/> in to PDM and returns those
        /// that still need checking in by hand: a refused check-in, or a file lying in a vault
        /// folder without ever having been added. A file outside every vault, one already
        /// checked in, or no PDM client at all needs nothing.</summary>
        private static IList<string> CheckInSourceFiles(IEnumerable<string> sourceFiles, string comment, int parentWindow)
        {
            var notCheckedIn = new List<string>();
            foreach (var path in sourceFiles)
            {
                var outcome = PdmCheckInService.CheckIn(path, comment, parentWindow);
                if (outcome == PdmCheckInOutcome.Failed || outcome == PdmCheckInOutcome.NotAddedToVault)
                    notCheckedIn.Add(path);
            }

            return notCheckedIn;
        }

        /// <summary>Deletes the work folders of earlier runs once they are older than a day.
        /// Called when the add-in connects; best effort and never throws — a folder still in
        /// use just stays until a later start.</summary>
        public static void PurgeStaleWorkFolders()
        {
            try
            {
                if (!Directory.Exists(WorkRoot))
                    return;

                var now = DateTime.UtcNow;
                foreach (var folder in Directory.GetDirectories(WorkRoot))
                {
                    if (!IsStaleWorkFolder(Directory.GetCreationTimeUtc(folder), now))
                        continue;

                    try
                    {
                        Directory.Delete(folder, true);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        Log.Debug("Work folder " + folder + " could not be deleted yet: " + ex.Message);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warning("Imported-component work folders could not be purged", ex);
            }
        }

        /// <summary>The top-level component names of every 3D Interconnect component that is
        /// an assembly; an imported part is already a single rigid body and is left alone.</summary>
        private static List<string> FindImportedSubassemblyNames(AssemblyDoc assembly)
        {
            var names = new List<string>();
            foreach (var component in TopLevelComponents(assembly))
            {
                var name = component.Name2;
                var feature = assembly.FeatureByName(name) as Feature;
                if (feature == null || !feature.Is3DInterconnectFeature)
                    continue;

                var document = component.GetModelDoc2() as IModelDoc2;
                if (document != null && document.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                    names.Add(name);
            }

            return names;
        }

        /// <summary>Saves <paramref name="component"/>'s document and everything below it into
        /// <paramref name="workFolder"/>, children first so a subassembly never saves ahead of
        /// its own references. A document is saved once however many instances use it
        /// (<paramref name="savedTitles"/>). Returns why it failed, or null.</summary>
        private static string SaveTreeToWorkFolder(Component2 component, string workFolder, HashSet<string> savedTitles)
        {
            foreach (var child in Children(component))
            {
                var error = SaveTreeToWorkFolder(child, workFolder, savedTitles);
                if (error != null)
                    return error;
            }

            var document = component.GetModelDoc2() as ModelDoc2;
            if (document == null)
                return "component " + component.Name2 + " is not loaded";

            var title = document.GetTitle();
            if (!savedTitles.Add(title))
                return null;

            var extension = ((IModelDoc2)document).GetType() == (int)swDocumentTypes_e.swDocASSEMBLY
                ? ".SLDASM"
                : ".SLDPRT";
            var fileName = UniqueFileName(
                ToComponentName(title), extension, candidate => File.Exists(Path.Combine(workFolder, candidate)));
            var path = Path.Combine(workFolder, fileName);

            int errors = 0;
            int warnings = 0;
            var saved = document.Extension.SaveAs3(
                path,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                null,
                ref errors,
                ref warnings);
            return saved
                ? null
                : string.Format(CultureInfo.InvariantCulture, "SaveAs3 of {0} failed with error {1}", title, errors);
        }

        /// <summary>Renames each embedded component back to <c>Value</c> where SolidWorks named
        /// it otherwise; a name it can't restore is only logged, the component keeps the name
        /// SolidWorks gave the virtual copy. SolidWorks refuses a component rename while "update
        /// component names when documents are replaced" is on, so it is switched off for the
        /// duration and restored to whatever the user had.</summary>
        private static void RestoreComponentNames(
            ISldWorks app, ModelDoc2 model, IList<KeyValuePair<Component2, string>> components)
        {
            if (app == null)
            {
                Log.Warning("No SolidWorks application available; embedded components keep their virtual-copy names");
                return;
            }

            const int toggle = (int)swUserPreferenceToggle_e.swExtRefUpdateCompNames;
            var previous = app.GetUserPreferenceToggle(toggle);
            try
            {
                app.SetUserPreferenceToggle(toggle, false);
                foreach (var pair in components)
                {
                    var component = pair.Key;
                    var wanted = pair.Value;
                    try
                    {
                        if (ComponentBaseName(component.Name2) == wanted)
                            continue;

                        model.ClearSelection2(true);
                        component.Select4(false, null, false);
                        component.Name2 = wanted;
                        if (ComponentBaseName(component.Name2) != wanted)
                            Log.Warning("Component " + wanted + " kept the name " + component.Name2);
                    }
                    catch (Exception ex)
                        when (!(ex is OutOfMemoryException || ex is StackOverflowException))
                    {
                        Log.Warning("Component " + wanted + " could not be renamed", ex);
                    }
                }
            }
            finally
            {
                app.SetUserPreferenceToggle(toggle, previous);
            }
        }

        /// <summary>The top-level components whose names aren't in <paramref name="names"/>,
        /// as a list taken now.</summary>
        private static List<Component2> ComponentsOtherThan(AssemblyDoc assembly, HashSet<string> names)
        {
            return TopLevelComponents(assembly).Where(component => !names.Contains(component.Name2)).ToList();
        }

        private static IEnumerable<Component2> TopLevelComponents(AssemblyDoc assembly)
        {
            return (assembly.GetComponents(true) as object[] ?? new object[0]).OfType<Component2>();
        }

        private static IEnumerable<Component2> Children(Component2 component)
        {
            return (component.GetChildren() as object[] ?? new object[0]).OfType<Component2>();
        }

        private static EmbedImportedComponentsResult Outcome(
            EmbedImportedComponentsOutcome outcome,
            EmbedImportedComponentsStep step = EmbedImportedComponentsStep.BreakLink)
        {
            return new EmbedImportedComponentsResult { Outcome = outcome, FailedStep = step };
        }

        private static EmbedImportedComponentsResult Failed(EmbedImportedComponentsStep step, string reason)
        {
            Log.Warning("Embedding imported components failed at " + step + ": " + reason);
            return Outcome(EmbedImportedComponentsOutcome.Failed, step);
        }

        /// <summary>A document title as a file and component name: the "^parent" suffix of a
        /// virtual component's title (what an unlinked 3D Interconnect component carries) and a
        /// SolidWorks extension dropped, characters Windows or SolidWorks reject in a name
        /// (including '@', which SolidWorks reserves) replaced by '_', and a fallback for a
        /// title left empty.</summary>
        internal static string ToComponentName(string title)
        {
            var name = (title ?? string.Empty).Trim();
            var caret = name.IndexOf('^');
            if (caret >= 0)
                name = name.Substring(0, caret).Trim();
            foreach (var extension in NativeExtensions)
            {
                if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - extension.Length);
                    break;
                }
            }

            var invalid = Path.GetInvalidFileNameChars();
            var characters = name.Select(c => invalid.Contains(c) || c == '@' ? '_' : c).ToArray();
            name = new string(characters).Trim().TrimEnd('.');
            return name.Length == 0 ? FallbackComponentName : name;
        }

        /// <summary><paramref name="name"/> + <paramref name="extension"/>, or with " (2)",
        /// " (3)" … appended to the name while <paramref name="isTaken"/> says the file name is
        /// in use — two different documents can share a title.</summary>
        internal static string UniqueFileName(string name, string extension, Func<string, bool> isTaken)
        {
            var candidate = name + extension;
            for (var suffix = 2; isTaken(candidate); suffix++)
                candidate = string.Format(CultureInfo.InvariantCulture, "{0} ({1}){2}", name, suffix, extension);
            return candidate;
        }

        /// <summary>A component name without its instance number ("-1") and, for a virtual
        /// component, without the "^assembly" suffix SolidWorks appends — the part a rename
        /// sets.</summary>
        internal static string ComponentBaseName(string componentName)
        {
            var name = componentName ?? string.Empty;
            var dash = name.LastIndexOf('-');
            if (dash > 0 && dash < name.Length - 1 && name.Substring(dash + 1).All(char.IsDigit))
                name = name.Substring(0, dash);

            var caret = name.LastIndexOf('^');
            return caret > 0 ? name.Substring(0, caret) : name;
        }

        /// <summary>True when <paramref name="path"/> lies directly or deeper inside
        /// <paramref name="folder"/>, ignoring case and slash direction.</summary>
        internal static bool IsInFolder(string path, string folder)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder))
                return false;

            var normalizedPath = path.Trim().Replace('/', '\\');
            var normalizedFolder = folder.Trim().Replace('/', '\\').TrimEnd('\\') + "\\";
            return normalizedPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsStaleWorkFolder(DateTime createdUtc, DateTime nowUtc)
        {
            return nowUtc - createdUtc > WorkFolderLifetime;
        }
    }
}

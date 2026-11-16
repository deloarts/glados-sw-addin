using System;
using System.Collections.Generic;
using System.Linq;
using GladosSwAddin.Models;

namespace GladosSwAddin.Services
{
    /// <summary>
    /// Decides, field by field, what a Save writes when the document may have changed since
    /// the task pane loaded it — a PDM data card, File &gt; Properties or a macro writes the
    /// same custom properties, and the pane is not notified of any of them. Save puts the
    /// user's edits on top of the file as it is at Save time, not on top of what was loaded:
    /// a field the user did not edit takes the file's current value, a field the user edited
    /// keeps the pane's value, and a field both sides changed to different values is a
    /// conflict the caller resolves with the user.
    ///
    /// "Edited" and "changed" are judged against the loaded baseline with the same comparisons
    /// the dirty markers use (ordinal text with null equal to empty, id equality, article
    /// groups as a set, routing via MadeItemSyncService.SameRouting), so a field shows a dirty
    /// marker exactly when Save treats it as the user's edit. Derived labels (*Name) are never
    /// compared — they travel with their key, and Save re-resolves them from the merged key.
    /// Mass, Material and Base Size are not merged here: their save path already writes only
    /// on an explicit override detected against the baseline, and their resolved value moves
    /// with every rebuild, so "changed in the file since load" cannot be told apart from a
    /// recalculation. Pure logic, no SolidWorks access.
    /// </summary>
    public static class SaveMergeService
    {
        /// <summary>One mergeable field: how two snapshots compare on it, and how to copy it
        /// (with its label, if it has one) from one snapshot into another.
        /// <see cref="FileSame"/> compares the baseline against the file; it differs from
        /// <see cref="Same"/> only where the file read carries state the pane never has (an
        /// unreadable routing).</summary>
        private sealed class MergeField<T>
        {
            public ItemField Field;
            public Func<T, T, bool> Same;
            public Func<T, T, bool> FileSame;
            public Action<T, T> CopyInto;
        }

        /// <summary>Merges a bought-tab Save. Returns a new object: the pane's values, with every
        /// field the user did not edit but the file changed replaced by the file's value.
        /// Conflicting fields keep the pane's value; <paramref name="conflicts"/> lists them for
        /// the caller to resolve (TakeFromFile).</summary>
        public static BoughtItemProperties MergeBought(
            BoughtItemProperties baseline,
            BoughtItemProperties pane,
            BoughtItemProperties file,
            out List<ItemField> conflicts)
        {
            return Merge(baseline, pane, file, BoughtMergeFields(), CloneBought, out conflicts);
        }

        /// <summary>Made-tab analogue of MergeBought.</summary>
        public static MadeItemProperties MergeMade(
            MadeItemProperties baseline,
            MadeItemProperties pane,
            MadeItemProperties file,
            out List<ItemField> conflicts)
        {
            return Merge(baseline, pane, file, MadeMergeFields(), CloneMade, out conflicts);
        }

        /// <summary>Assembly-tab analogue of MergeBought, over the three fields that tab
        /// edits.</summary>
        public static MadeItemProperties MergeAssembly(
            MadeItemProperties baseline,
            MadeItemProperties pane,
            MadeItemProperties file,
            out List<ItemField> conflicts)
        {
            return Merge(baseline, pane, file, AssemblyMergeFields(), CloneMade, out conflicts);
        }

        /// <summary>Resolves conflicts in favour of the file: copies each of
        /// <paramref name="fields"/> from <paramref name="file"/> into
        /// <paramref name="merged"/>.</summary>
        public static void TakeFromFile(
            BoughtItemProperties merged, BoughtItemProperties file, IEnumerable<ItemField> fields)
        {
            TakeFromFile(merged, file, fields, BoughtMergeFields());
        }

        /// <summary>Made/assembly analogue of the bought TakeFromFile.</summary>
        public static void TakeFromFile(
            MadeItemProperties merged, MadeItemProperties file, IEnumerable<ItemField> fields)
        {
            TakeFromFile(merged, file, fields, MadeMergeFields());
        }

        /// <summary>True when the bought tab holds an unsaved edit: any field a Save writes
        /// differs from the loaded baseline, by the same comparisons as the merge — Mass
        /// included, which the merge leaves to its own save path. Decides whether a change
        /// noticed in the file can simply be reloaded into the pane or would discard
        /// work.</summary>
        public static bool HasEdits(BoughtItemProperties baseline, BoughtItemProperties pane)
        {
            return BoughtMergeFields().Any(field => !field.Same(pane, baseline))
                || pane.Mass != baseline.Mass;
        }

        /// <summary>Made-tab analogue of the bought HasEdits, Material and Base Size included.
        /// Also right for the assembly tab, whose pane copies every field it has no control
        /// for from the baseline.</summary>
        public static bool HasEdits(MadeItemProperties baseline, MadeItemProperties pane)
        {
            return MadeMergeFields().Any(field => !field.Same(pane, baseline))
                || pane.Mass != baseline.Mass
                || !TextSame(pane.Material, baseline.Material)
                || !TextSame(pane.BaseSize, baseline.BaseSize);
        }

        private static bool TextSame(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
        }

        private static T Merge<T>(
            T baseline,
            T pane,
            T file,
            List<MergeField<T>> fields,
            Func<T, T> clone,
            out List<ItemField> conflicts)
        {
            conflicts = new List<ItemField>();
            var merged = clone(pane);

            foreach (var field in fields)
            {
                bool edited = !field.Same(pane, baseline);
                bool changedInFile = !field.FileSame(file, baseline);

                if (!changedInFile)
                    // Nothing happened in the file: the pane's value is right either way, and
                    // keeping it (rather than copying the equal file value) leaves an untouched
                    // document's Save exactly as it was before this merge existed.
                    continue;

                if (!edited)
                    field.CopyInto(merged, file);
                else if (!field.Same(pane, file))
                    conflicts.Add(field.Field);
            }

            return merged;
        }

        private static void TakeFromFile<T>(
            T merged, T file, IEnumerable<ItemField> fields, List<MergeField<T>> table)
        {
            var wanted = new HashSet<ItemField>(fields ?? Enumerable.Empty<ItemField>());
            foreach (var field in table)
            {
                if (wanted.Contains(field.Field))
                    field.CopyInto(merged, file);
            }
        }

        #region Field tables

        private static List<MergeField<BoughtItemProperties>> BoughtMergeFields()
        {
            return new List<MergeField<BoughtItemProperties>>
            {
                Text<BoughtItemProperties>(ItemField.ArticleName, p => p.ArticleName, (p, v) => p.ArticleName = v),
                Text<BoughtItemProperties>(ItemField.OrderCode1, p => p.OrderCode1, (p, v) => p.OrderCode1 = v),
                Text<BoughtItemProperties>(ItemField.OrderCode2, p => p.OrderCode2, (p, v) => p.OrderCode2 = v),
                Text<BoughtItemProperties>(ItemField.Gtin, p => p.Gtin, (p, v) => p.Gtin = v),
                Text<BoughtItemProperties>(ItemField.Ean, p => p.Ean, (p, v) => p.Ean = v),
                Text<BoughtItemProperties>(ItemField.TariffNumber, p => p.TariffNumber, (p, v) => p.TariffNumber = v),
                Text<BoughtItemProperties>(ItemField.Note, p => p.Note, (p, v) => p.Note = v),
                Text<BoughtItemProperties>(ItemField.Weblink, p => p.Weblink, (p, v) => p.Weblink = v),
                Id<BoughtItemProperties>(ItemField.EdpPartId, p => p.EdpPartId, (target, source) =>
                    target.EdpPartId = source.EdpPartId),
                Id<BoughtItemProperties>(ItemField.ManufacturerId, p => p.ManufacturerId, (target, source) =>
                {
                    target.ManufacturerId = source.ManufacturerId;
                    target.ManufacturerName = source.ManufacturerName;
                }),
                Groups<BoughtItemProperties>(p => p.ArticleGroupIds, (target, source) =>
                {
                    target.ArticleGroupIds = new List<int>(source.ArticleGroupIds ?? new List<int>());
                    target.ArticleGroupNames = new List<string>(source.ArticleGroupNames ?? new List<string>());
                }),
            };
        }

        private static List<MergeField<MadeItemProperties>> MadeMergeFields()
        {
            return new List<MergeField<MadeItemProperties>>
            {
                Text<MadeItemProperties>(ItemField.ArticleName, p => p.ArticleName, (p, v) => p.ArticleName = v),
                Text<MadeItemProperties>(ItemField.Note, p => p.Note, (p, v) => p.Note = v),
                Groups<MadeItemProperties>(p => p.ArticleGroupIds, (target, source) =>
                {
                    target.ArticleGroupIds = new List<int>(source.ArticleGroupIds ?? new List<int>());
                    target.ArticleGroupNames = new List<string>(source.ArticleGroupNames ?? new List<string>());
                }),
                Id<MadeItemProperties>(ItemField.SourceItemId, p => p.SourceItemId, (target, source) =>
                {
                    target.SourceItemId = source.SourceItemId;
                    target.SourceItemName = source.SourceItemName;
                }),
                Routing(),
                Id<MadeItemProperties>(ItemField.CustomerId, p => p.CustomerId, (target, source) =>
                {
                    target.CustomerId = source.CustomerId;
                    target.CustomerName = source.CustomerName;
                }),
                Text<MadeItemProperties>(
                    ItemField.CustomerPartNumber, p => p.CustomerPartNumber, (p, v) => p.CustomerPartNumber = v),
                Text<MadeItemProperties>(
                    ItemField.CustomerRevision, p => p.CustomerRevision, (p, v) => p.CustomerRevision = v),
            };
        }

        private static List<MergeField<MadeItemProperties>> AssemblyMergeFields()
        {
            return new List<MergeField<MadeItemProperties>>
            {
                Text<MadeItemProperties>(ItemField.ArticleName, p => p.ArticleName, (p, v) => p.ArticleName = v),
                Text<MadeItemProperties>(ItemField.Note, p => p.Note, (p, v) => p.Note = v),
                Routing(),
            };
        }

        private static MergeField<T> Text<T>(ItemField field, Func<T, string> get, Action<T, string> set)
        {
            Func<T, T, bool> same = (a, b) =>
                string.Equals(get(a) ?? string.Empty, get(b) ?? string.Empty, StringComparison.Ordinal);
            return new MergeField<T>
            {
                Field = field,
                Same = same,
                FileSame = same,
                CopyInto = (target, source) => set(target, get(source)),
            };
        }

        private static MergeField<T> Id<T>(ItemField field, Func<T, int?> get, Action<T, T> copyInto)
        {
            Func<T, T, bool> same = (a, b) => get(a) == get(b);
            return new MergeField<T> { Field = field, Same = same, FileSame = same, CopyInto = copyInto };
        }

        private static MergeField<T> Groups<T>(Func<T, List<int>> get, Action<T, T> copyInto)
        {
            Func<T, T, bool> same = (a, b) =>
                new HashSet<int>(get(a) ?? new List<int>()).SetEquals(get(b) ?? new List<int>());
            return new MergeField<T>
            {
                Field = ItemField.ArticleGroupIds,
                Same = same,
                FileSame = same,
                CopyInto = copyInto,
            };
        }

        /// <summary>The routing compares by MadeItemSyncService.SameRouting. The file side also
        /// counts a routing that has become unreadable since load as a change: its parse is an
        /// empty list, which would otherwise look unchanged next to an empty baseline. Copying
        /// an unreadable file routing carries ProcessesUnreadable along, which tells Save to
        /// leave the stored text alone instead of overwriting it with an empty routing. The
        /// pane side never compares the flag — the pane's own ProcessesUnreadable is always
        /// false (it describes a read, not an edit).</summary>
        private static MergeField<MadeItemProperties> Routing()
        {
            return new MergeField<MadeItemProperties>
            {
                Field = ItemField.Processes,
                Same = (a, b) => MadeItemSyncService.SameRouting(a.Processes, b.Processes),
                FileSame = (file, baseline) =>
                    file.ProcessesUnreadable == baseline.ProcessesUnreadable
                    && MadeItemSyncService.SameRouting(file.Processes, baseline.Processes),
                CopyInto = (target, source) =>
                {
                    target.Processes = CloneSteps(source.Processes);
                    target.ProcessesUnreadable = source.ProcessesUnreadable;
                    target.ProcessNames = source.ProcessNames;
                },
            };
        }

        #endregion

        #region Cloning

        private static BoughtItemProperties CloneBought(BoughtItemProperties p)
        {
            return new BoughtItemProperties
            {
                PartNumber = p.PartNumber,
                Revision = p.Revision,
                ArticleName = p.ArticleName,
                OrderCode1 = p.OrderCode1,
                OrderCode2 = p.OrderCode2,
                Gtin = p.Gtin,
                Ean = p.Ean,
                TariffNumber = p.TariffNumber,
                Mass = p.Mass,
                Note = p.Note,
                Weblink = p.Weblink,
                ManufacturerId = p.ManufacturerId,
                ArticleGroupIds = new List<int>(p.ArticleGroupIds ?? new List<int>()),
                EdpPartId = p.EdpPartId,
                ManufacturerName = p.ManufacturerName,
                ArticleGroupNames = new List<string>(p.ArticleGroupNames ?? new List<string>()),
            };
        }

        private static MadeItemProperties CloneMade(MadeItemProperties p)
        {
            return new MadeItemProperties
            {
                PartNumber = p.PartNumber,
                Revision = p.Revision,
                ArticleName = p.ArticleName,
                Material = p.Material,
                Mass = p.Mass,
                BaseSize = p.BaseSize,
                Note = p.Note,
                Weblink = p.Weblink,
                ArticleGroupIds = new List<int>(p.ArticleGroupIds ?? new List<int>()),
                ArticleGroupNames = new List<string>(p.ArticleGroupNames ?? new List<string>()),
                SourceItemId = p.SourceItemId,
                SourceItemName = p.SourceItemName,
                Processes = CloneSteps(p.Processes),
                ProcessesUnreadable = p.ProcessesUnreadable,
                ProcessesStored = p.ProcessesStored,
                ProcessNames = p.ProcessNames,
                CustomerId = p.CustomerId,
                CustomerName = p.CustomerName,
                CustomerPartNumber = p.CustomerPartNumber,
                CustomerRevision = p.CustomerRevision,
            };
        }

        private static List<ProcessStep> CloneSteps(IEnumerable<ProcessStep> steps)
        {
            return (steps ?? Enumerable.Empty<ProcessStep>()).Select(step => step.Clone()).ToList();
        }

        #endregion
    }
}

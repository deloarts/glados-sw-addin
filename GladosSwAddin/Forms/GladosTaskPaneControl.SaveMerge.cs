using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GladosSwAddin.Forms;
using GladosSwAddin.Models;
using GladosSwAddin.Resources;
using GladosSwAddin.Services;
using SolidWorks.Interop.sldworks;

namespace GladosSwAddin
{
    /// <summary>
    /// The Save paths' guard against overwriting what changed in the file behind the task
    /// pane's back. Nothing notifies the pane when a PDM data card, File &gt; Properties or a
    /// macro writes one of the add-in's properties, so right before writing, each Save re-reads
    /// the document and lets SaveMergeService put the user's edits on top of it: a field the
    /// user did not edit takes the file's current value, and a field both sides changed
    /// differently is put to the user (SaveConflictForm). Every Save writes the merged result
    /// through its unchanged write sequence, so the fan-out to every configuration, the
    /// re-resolved labels and the unmapped-is-never-written rule all stay as they were.
    /// </summary>
    public partial class GladosTaskPaneControl
    {
        /// <summary>Bought-tab merge. False when the Save must stop: the user cancelled the
        /// conflict dialog, or the document could not be re-read — a failed read never falls
        /// back to writing the pane's possibly stale values.</summary>
        private bool TryMergeBoughtSave(
            ModelDoc2 model, FieldMappingService fieldMapping, BoughtItemProperties pane,
            out BoughtItemProperties merged)
        {
            merged = null;

            BoughtItemProperties file;
            if (!TryReadForMerge(() => SwCustomPropertyService.ReadBoughtItem(model, fieldMapping), out file))
                return false;

            List<ItemField> conflicts;
            merged = SaveMergeService.MergeBought(_boughtItemLocal, pane, file, out conflicts);
            if (conflicts.Count == 0)
                return true;

            var rows = conflicts.Select(field => new SaveConflictRow(
                ConflictFieldLabel(field, assembly: false),
                FormatBoughtConflictValue(field, file),
                FormatBoughtConflictValue(field, pane)));

            switch (AskSaveConflict(rows))
            {
                case SaveConflictChoice.UseMine:
                    return true;
                case SaveConflictChoice.KeepFile:
                    SaveMergeService.TakeFromFile(merged, file, conflicts);
                    return true;
                default:
                    merged = null;
                    return false;
            }
        }

        /// <summary>Made-tab and assembly-tab analogue of TryMergeBoughtSave;
        /// <paramref name="assembly"/> picks the assembly tab's field set and labels.</summary>
        private bool TryMergeMadeSave(
            ModelDoc2 model, FieldMappingService fieldMapping, MadeItemProperties baseline,
            MadeItemProperties pane, bool assembly, out MadeItemProperties merged)
        {
            merged = null;

            MadeItemProperties file;
            if (!TryReadForMerge(() => SwCustomPropertyService.ReadMadeItem(model, fieldMapping), out file))
                return false;

            // Same as every load: a step read back from the file has ids only, and both the
            // conflict dialog and the process-names label need its method name.
            ResolveStepMethodNames(file.Processes);

            List<ItemField> conflicts;
            merged = assembly
                ? SaveMergeService.MergeAssembly(baseline, pane, file, out conflicts)
                : SaveMergeService.MergeMade(baseline, pane, file, out conflicts);
            if (conflicts.Count == 0)
                return true;

            var rows = conflicts.Select(field => new SaveConflictRow(
                ConflictFieldLabel(field, assembly),
                FormatMadeConflictValue(field, file),
                FormatMadeConflictValue(field, pane)));

            switch (AskSaveConflict(rows))
            {
                case SaveConflictChoice.UseMine:
                    return true;
                case SaveConflictChoice.KeepFile:
                    SaveMergeService.TakeFromFile(merged, file, conflicts);
                    return true;
                default:
                    merged = null;
                    return false;
            }
        }

        private bool TryReadForMerge<T>(Func<T> read, out T file)
            where T : class
        {
            try
            {
                file = read();
                return true;
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Error("Re-reading the document before Save failed", ex);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorFailedToSaveDocumentFormat, ex.Message));
                file = null;
                return false;
            }
        }

        private SaveConflictChoice AskSaveConflict(IEnumerable<SaveConflictRow> rows)
        {
            using (var form = new SaveConflictForm(rows))
            {
                form.ShowDialog(this);
                EnsureUiSynchronizationContext();
                return form.Choice;
            }
        }

        /// <summary>The same localized label the field carries on its tab.</summary>
        private static string ConflictFieldLabel(ItemField field, bool assembly)
        {
            switch (field)
            {
                case ItemField.ArticleName:
                    return assembly ? Strings.LabelAssemblyNameText : Strings.LabelArticleNameText;
                case ItemField.OrderCode1:
                    return Strings.LabelOrderCode1Text;
                case ItemField.OrderCode2:
                    return Strings.LabelOrderCode2Text;
                case ItemField.Gtin:
                    return Strings.LabelGtinText;
                case ItemField.Ean:
                    return Strings.LabelEanText;
                case ItemField.TariffNumber:
                    return Strings.LabelTariffNumberText;
                case ItemField.Note:
                    return Strings.LabelNoteText;
                case ItemField.Weblink:
                    return Strings.LabelWeblinkText;
                case ItemField.EdpPartId:
                    return Strings.FieldDataPortalPartText;
                case ItemField.ManufacturerId:
                    return Strings.LabelManufacturerText;
                case ItemField.ArticleGroupIds:
                    return Strings.LabelArticleGroupsText;
                case ItemField.SourceItemId:
                    return Strings.LabelSourceItemText;
                case ItemField.Processes:
                    return Strings.MadeItemStepsTitleText;
                case ItemField.CustomerId:
                    return Strings.LabelCustomerText;
                case ItemField.CustomerPartNumber:
                    return Strings.LabelCustomerPartNumberText;
                case ItemField.CustomerRevision:
                    return Strings.LabelCustomerRevisionText;
                default:
                    return field.ToString();
            }
        }

        private string FormatBoughtConflictValue(ItemField field, BoughtItemProperties p)
        {
            switch (field)
            {
                case ItemField.ArticleName:
                    return ConflictText(p.ArticleName);
                case ItemField.OrderCode1:
                    return ConflictText(p.OrderCode1);
                case ItemField.OrderCode2:
                    return ConflictText(p.OrderCode2);
                case ItemField.Gtin:
                    return ConflictText(p.Gtin);
                case ItemField.Ean:
                    return ConflictText(p.Ean);
                case ItemField.TariffNumber:
                    return ConflictText(p.TariffNumber);
                case ItemField.Note:
                    return ConflictText(p.Note);
                case ItemField.Weblink:
                    return ConflictText(p.Weblink);
                case ItemField.EdpPartId:
                    return p.EdpPartId.HasValue
                        ? string.Format(CultureInfo.CurrentCulture, Strings.LabelDataPortalLinkedFormat, p.EdpPartId.Value)
                        : ConflictText(null);
                case ItemField.ManufacturerId:
                    return ConflictReference(p.ManufacturerId, ResolveManufacturerLabel(p.ManufacturerId), p.ManufacturerName);
                case ItemField.ArticleGroupIds:
                    return ConflictGroups(p.ArticleGroupIds, p.ArticleGroupNames);
                default:
                    return string.Empty;
            }
        }

        private string FormatMadeConflictValue(ItemField field, MadeItemProperties p)
        {
            switch (field)
            {
                case ItemField.ArticleName:
                    return ConflictText(p.ArticleName);
                case ItemField.Note:
                    return ConflictText(p.Note);
                case ItemField.ArticleGroupIds:
                    return ConflictGroups(p.ArticleGroupIds, p.ArticleGroupNames);
                case ItemField.SourceItemId:
                    return ConflictReference(p.SourceItemId, ResolveSourceItemLabel(p.SourceItemId), p.SourceItemName);
                case ItemField.Processes:
                    return p.ProcessesUnreadable
                        ? Strings.SaveConflictUnreadableValueText
                        : ConflictText(SwCustomPropertyService.BuildProcessNamesLabel(p.Processes));
                case ItemField.CustomerId:
                    return ConflictReference(p.CustomerId, ResolveCustomerLabel(p.CustomerId), p.CustomerName);
                case ItemField.CustomerPartNumber:
                    return ConflictText(p.CustomerPartNumber);
                case ItemField.CustomerRevision:
                    return ConflictText(p.CustomerRevision);
                default:
                    return string.Empty;
            }
        }

        private static string ConflictText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? Strings.SaveConflictEmptyValueText : value;
        }

        /// <summary>A key shown by name: resolved from the lookups, else the label stored next
        /// to it, else "#id".</summary>
        private static string ConflictReference(int? id, string resolved, string stored)
        {
            if (!id.HasValue)
                return Strings.SaveConflictEmptyValueText;

            return !string.IsNullOrWhiteSpace(resolved) ? resolved : PlaceholderLabel(stored, id.Value);
        }

        private string ConflictGroups(List<int> ids, List<string> stored)
        {
            ids = ids ?? new List<int>();
            if (ids.Count == 0)
                return Strings.SaveConflictEmptyValueText;

            var names = ResolveArticleGroupLabels(ids);
            if (names == null && stored != null && stored.Count == ids.Count)
                names = stored;

            return string.Join(
                "; ",
                names ?? ids.Select(id => "#" + id.ToString(CultureInfo.InvariantCulture)).ToList());
        }
    }
}

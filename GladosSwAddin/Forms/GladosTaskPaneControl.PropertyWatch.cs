using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using GladosSwAddin.Resources;
using GladosSwAddin.Services;
using SolidWorks.Interop.sldworks;

namespace GladosSwAddin
{
    /// <summary>
    /// Notices when the active document's properties change behind the task pane's back — a
    /// PDM data card, File &gt; Properties, a macro, PDM assigning a part number — and brings
    /// the pane up to date. Polled from the document watchdog rather than hooked to
    /// SolidWorks' custom-property notifications: those are not guaranteed to fire for every
    /// writer (API writes in particular), while a poll sees every change whoever made it.
    /// Each load captures a fingerprint of every property the pane reads
    /// (SwCustomPropertyService.ReadPropertyFingerprint); a tick that finds a different one
    /// reloads the pane when it holds no unsaved edit, and otherwise only marks the Reload
    /// From File buttons, leaving the edits alone — Save already puts them on top of the
    /// file's current values (GladosTaskPaneControl.SaveMerge.cs).
    /// </summary>
    public partial class GladosTaskPaneControl
    {
        /// <summary>How often the watchdog (1 s) compares fingerprints: every second tick, so a
        /// change shows within about two seconds.</summary>
        private const int PropertyWatchEveryTicks = 2;

        /// <summary>The fingerprint the pane was last loaded against; null while there is
        /// nothing to watch (no document, a never-saved one, or a failed read).</summary>
        private string _propertyFingerprint;

        private int _propertyWatchTicks;

        /// <summary>Set while the pane's own Save or Calc. writes are under way, so the watch
        /// never mistakes them for an outside change. A counter, not a flag, in case two ever
        /// overlap.</summary>
        private int _propertyWritesInProgress;

        /// <summary>True when a change was noticed but not loaded because the pane held unsaved
        /// edits; shown on the Reload From File buttons until the next load.</summary>
        private bool _propertiesChangedOutside;

        /// <summary>True during a reload the watch started. The pane held no unsaved edit then,
        /// so the populate methods may overwrite even a control that reports focus — the
        /// focus guard protects typing, and there is none to protect here.</summary>
        private bool _overwriteFocusedOnPopulate;

        /// <summary>Starts watching <paramref name="model"/> against its properties as they are
        /// now. Called at the start of every load, before anything is read, so a change made
        /// while the load runs is still noticed afterwards.</summary>
        private void WatchPropertiesOf(ModelDoc2 model)
        {
            _propertyFingerprint = TryReadPropertyFingerprint(model);
            _propertyWatchTicks = 0;
            SetPropertiesChangedOutside(false);
        }

        private void StopPropertyWatch()
        {
            _propertyFingerprint = null;
            SetPropertiesChangedOutside(false);
        }

        private static string TryReadPropertyFingerprint(ModelDoc2 model)
        {
            try
            {
                return SwCustomPropertyService.ReadPropertyFingerprint(model, ApplicationServices.FieldMapping);
            }
            catch (COMException ex)
            {
                Log.Debug("Reading the property fingerprint failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Runs a Save or Calc. so that the watch sits it out — see
        /// _propertyWritesInProgress. Its own closing reload re-captures the
        /// fingerprint.</summary>
        private async Task WritingProperties(Func<Task> write)
        {
            _propertyWritesInProgress++;
            try
            {
                await write();
            }
            finally
            {
                _propertyWritesInProgress--;
            }
        }

        /// <summary>The watchdog's per-tick property check, for a document it has already found
        /// unchanged. Skipped while the pane writes, while a modal dialog of this add-in is
        /// open (a source-item pick or a Save conflict must not be reloaded underneath) and
        /// while the document is held locked as never saved. A reload it starts is tracked
        /// through _documentWatchdogTickInProgress like any other watchdog-started load.</summary>
        private void CheckForOutsidePropertyChange(ModelDoc2 model)
        {
            if (_propertyFingerprint == null || _propertyWritesInProgress > 0 || _activeDocumentUnsaved)
                return;

            if (++_propertyWatchTicks < PropertyWatchEveryTicks)
                return;
            _propertyWatchTicks = 0;

            if (Application.OpenForms.Cast<Form>().Any(form => form.Modal))
                return;

            var current = TryReadPropertyFingerprint(model);
            if (current == null || string.Equals(current, _propertyFingerprint, StringComparison.Ordinal))
                return;

            _propertyFingerprint = current;

            if (PaneHasUnsavedEdits())
            {
                Log.Info("Document properties changed outside the add-in; unsaved edits kept, reload offered");
                SetPropertiesChangedOutside(true);
                return;
            }

            Log.Info("Document properties changed outside the add-in; reloading the task pane");
            _documentWatchdogTickInProgress = true;
            RunGuarded(async () =>
            {
                _overwriteFocusedOnPopulate = true;
                try
                {
                    await ReloadAllForModel(model);
                }
                finally
                {
                    _overwriteFocusedOnPopulate = false;
                    _documentWatchdogTickInProgress = false;
                }
            });
        }

        /// <summary>True when any loaded tab holds an edit Save would write, by the same
        /// comparisons as the Save merge (SaveMergeService.HasEdits) — routing included, which
        /// has no dirty marker on the made tab.</summary>
        private bool PaneHasUnsavedEdits()
        {
            if (_boughtItemLocal != null && SaveMergeService.HasEdits(_boughtItemLocal, ReadBoughtItemFromUi()))
                return true;
            if (_madeItemLocal != null && SaveMergeService.HasEdits(_madeItemLocal, ReadMadeItemFromUi()))
                return true;
            return _assemblyLocal != null && SaveMergeService.HasEdits(_assemblyLocal, ReadAssemblyFromUi());
        }

        private void SetPropertiesChangedOutside(bool changed)
        {
            _propertiesChangedOutside = changed;
            ApplyPropertiesChangedOutsideMarker();
        }

        /// <summary>Colours the three Reload From File buttons and swaps their tooltip while a
        /// noticed change waits to be loaded. Re-applied after a language switch, which resets
        /// the tooltips.</summary>
        private void ApplyPropertiesChangedOutsideMarker()
        {
            OnUiThread(() =>
            {
                var tooltip = _propertiesChangedOutside
                    ? Strings.TooltipReloadFromFileChangedOutside
                    : Strings.TooltipReloadFromFile;

                MarkReloadButton(buttonBoughtItemReloadProperties, _boughtItemButtonTooltip, tooltip);
                MarkReloadButton(buttonMadeItemReloadProperties, _madeItemButtonTooltip, tooltip);
                MarkReloadButton(buttonAssemblyReloadProperties, _assemblyButtonTooltip, tooltip);
            });
        }

        private void MarkReloadButton(Button button, ToolTip toolTip, string tooltip)
        {
            if (_propertiesChangedOutside)
                button.ForeColor = ChangedOutsideColor;
            else
                button.ResetForeColor();

            toolTip.SetToolTip(button, tooltip);
        }

        /// <summary>The same orange as the routing's "unreadable" hint.</summary>
        private static readonly System.Drawing.Color ChangedOutsideColor = System.Drawing.Color.DarkOrange;
    }
}

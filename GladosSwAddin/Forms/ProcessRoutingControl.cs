using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Helpers;
using GladosSwAddin.Models;
using GladosSwAddin.Resources;
using GladosSwAddin.Services;

namespace GladosSwAddin
{
    /// <summary>
    /// Replaces the made tab's former step ListBox + step editor + ProcessGroupingControl chain
    /// with a "filled-plus-one" row list (deloarts/pytia-property-manager's pattern): one panel
    /// per existing ProcessStep plus a permanently-present trailing empty row, so adding a step
    /// is just picking a method in the row that's already there instead of a separate Add
    /// button. A link/split icon button between two consecutive rows carries the grouping that
    /// used to be ProcessGroupingControl's own chip chain; setup minutes and minutes-per-piece
    /// are deliberately not editable here (manufacturing data, decided out of the UI) even
    /// though ProcessStep still carries them untouched.
    ///
    /// Rows are rebuilt from scratch on every structural change (SetSteps, a method pick that
    /// adds/removes a step, a reorder, a boundary toggle) - simplest correct option given every
    /// row's enabled state, title number and neighbouring boundary all depend on the full list,
    /// not just the row that changed. A plain note edit is the one exception (see
    /// HandleNoteChanged): rebuilding on every keystroke would tear down the very TextBox the
    /// user is typing into.
    ///
    /// Like the ProcessGroupingControl it replaces, SetSteps keeps the caller's List&lt;ProcessStep&gt;
    /// by reference rather than cloning it, and every structural edit below replaces that same
    /// list's *contents* in place (Clear + AddRange, or an in-place GroupIndex rewrite) instead
    /// of assigning a new list - so GladosTaskPaneControl's own _madeItemSteps field, which was
    /// handed to SetSteps, sees every change without any further plumbing.
    /// </summary>
    public partial class ProcessRoutingControl : UserControl
    {
        // The row template's height, vertical spacing and per-row control offsets are fixed -
        // SolidWorks task panes only ever resize in width, never height. Width is the opposite:
        // every row and its children are laid out from ClientSize.Width fresh on every
        // RebuildRows (see AddRow/AddBoundaryControl) rather than a fixed constant, since a
        // plain Anchor on a child only tracks a resize that happens after it's added - relying
        // on Anchor alone would snap every row back to whatever width it had at its last
        // rebuild. OnResize below re-triggers RebuildRows whenever the width actually changes.
        // RowSpacing leaves a 35px gap below every row for the boundary control between it and
        // the next one.
        private const int RowHeight = 115;
        private const int RowSpacing = 150;
        private const int BoundaryWidth = 26;
        private const int BoundaryHeight = 28;
        private const int BoundaryTopOffset = RowHeight + 4;

        // Floors the width used for row layout math (comboWidth/noteWidth subtract margins from
        // it) so a pathologically narrow host - the task pane undocked and squeezed thin, or a
        // transient 0-width layout pass - can never drive a control Size negative, which WinForms
        // rejects.
        private const int MinimumWidth = 100;

        private static readonly Color MutedColor = Color.Gray;

        private readonly ToolTip _toolTip = new ToolTip();
        private readonly List<Control> _dynamicControls = new List<Control>();

        private List<ProcessStep> _steps = new List<ProcessStep>();
        private List<MethodChoice> _fabricationChoices = new List<MethodChoice>();
        private List<MethodChoice> _inspectionChoices = new List<MethodChoice>();

        private bool _readOnly;
        private Color _accentColor = ThemeColors.GetPrimaryAccent(ThemePrimary.Glados);

        /// <summary>The ClientSize.Width RebuildRows last laid rows out at - compared against
        /// the current width in OnResize so a rebuild is only triggered by a real width change,
        /// never by the Height assignment RebuildRows itself makes on every add/remove/move
        /// (Control.Height's setter raises Resize too, even though the width didn't change).
        /// Set at the top of RebuildRows, before that Height assignment, so the nested OnResize
        /// it triggers already sees "no width change" and doesn't recursively rebuild a second
        /// time.</summary>
        private int _lastWidth;

        /// <summary>Suppresses every row control's change handler while RebuildRows is
        /// (re)populating them - without this, assigning a freshly-built ComboBox's Items or a
        /// restored TextBox.Text would be read back as the user editing the very row that's just
        /// being (re)constructed. Mirrors the old step editor's
        /// _suppressMadeItemStepDetailChange.</summary>
        private bool _suppressChange;

        /// <summary>Fires after every committed structural or note edit (append, remove, method
        /// change, reorder, boundary toggle, note text) - never from SetSteps or SetMethodChoices
        /// themselves, since repopulating from a freshly loaded/reloaded baseline isn't a user
        /// edit. The host reacts the same way it tracks every other field's dirty marker (see
        /// GladosTaskPaneControl.WireMadeItemDirtyTracking).</summary>
        public event EventHandler RoutingChanged;

        public ProcessRoutingControl()
        {
            InitializeComponent();
            Disposed += (s, e) => _toolTip.Dispose();
            RebuildRows();
        }

        /// <summary>When true, every row's combobox/note box/arrow button and every boundary
        /// toggle is disabled - the rows still render so the current routing stays visible, it
        /// just can't be edited. Not currently driven by GladosTaskPaneControl.ApplyConnectionState:
        /// like the old step editor, routing edits are local-only and don't need a server
        /// connection (the method choices themselves come from the cached lookup snapshot).</summary>
        public bool ReadOnly
        {
            get { return _readOnly; }
            set
            {
                if (_readOnly == value)
                    return;

                _readOnly = value;
                RebuildRows();
            }
        }

        /// <summary>The accent color used to tint a linked (grouped) boundary toggle - set by
        /// the host from ThemeColors.GetPrimaryAccent(currentThemePrimary), mirroring
        /// GroupingChain.vue's --p-primary-color the way ProcessGroupingControl's own
        /// AccentColor did, though which state actually carries the accent is inverted here -
        /// see AddBoundaryControl. Defaults to the Glados theme's own accent so a control that's
        /// never had this set still renders sensibly.</summary>
        public Color AccentColor
        {
            get { return _accentColor; }
            set
            {
                _accentColor = value;
                RebuildRows();
            }
        }

        /// <summary>
        /// Replaces the working list shown by the rows, in position order - kept by reference
        /// (see the class doc comment), not cloned. Rebuilds every row from scratch but never
        /// raises RoutingChanged: an initial or reloaded population isn't a user edit, exactly
        /// like the old ProcessGroupingControl.SetSteps.
        /// </summary>
        public void SetSteps(List<ProcessStep> steps)
        {
            _steps = steps ?? new List<ProcessStep>();
            RebuildRows();
        }

        /// <summary>
        /// Rebuilds the one method-choice model every row's combobox is built from: an empty
        /// entry first (the trailing row's default, and what picking removes a filled row back
        /// to), then every pickable fabrication method plain, then every pickable inspection
        /// method suffixed via Strings.ProcessRoutingInspectionChoiceFormat so the two catalogues
        /// stay visually distinguishable in one flat list (decided over separate role/method
        /// controls - see the class doc comment). Callers pass the current lookup snapshot's
        /// non-archived rows (GladosTaskPaneControl.PickableFabricationMethods/
        /// PickableInspectionMethods) - a step referencing an id that isn't in either list
        /// (archived or unknown) still gets an extra per-row entry so it keeps displaying; see
        /// BuildRowChoices.
        /// </summary>
        public void SetMethodChoices(
            IReadOnlyList<FabricationMethodResponse> fabrication,
            IReadOnlyList<InspectionMethodResponse> inspection)
        {
            _fabricationChoices = (fabrication ?? new FabricationMethodResponse[0])
                .Select(m => new MethodChoice(m.Id, m.Name, m.Name, StepRole.Fabrication))
                .ToList();

            _inspectionChoices = (inspection ?? new InspectionMethodResponse[0])
                .Select(m => new MethodChoice(
                    m.Id,
                    m.Name,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ProcessRoutingInspectionChoiceFormat,
                        m.Name,
                        Strings.MadeItemStepRoleInspectionText),
                    StepRole.Inspection))
                .ToList();

            RebuildRows();
        }

        private void OnRoutingChanged()
        {
            var handler = RoutingChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        /// <summary>One entry of a row combobox's Items - a uniform shape for both
        /// FabricationMethodResponse and InspectionMethodResponse (only Id/Name are ever needed
        /// here), plus which ProcessStep field a pick writes to. DisplayName is what the combo
        /// shows (the inspection suffix lives here, never on Name - see ProcessStep.MethodName's
        /// own contract for why the stored name has to stay plain). Empty is the sentinel for
        /// "nothing picked", compared by reference rather than by Id, since a real fabrication
        /// and inspection method can validly share the same numeric id (separate tables).
        /// </summary>
        private sealed class MethodChoice
        {
            public static readonly MethodChoice Empty =
                new MethodChoice(0, string.Empty, string.Empty, StepRole.Fabrication);

            public MethodChoice(int id, string name, string displayName, StepRole role)
            {
                Id = id;
                Name = name;
                DisplayName = displayName;
                Role = role;
            }

            public int Id { get; }

            public string Name { get; }

            public string DisplayName { get; }

            public StepRole Role { get; }
        }

        /// <summary>The boundary set implied by <paramref name="steps"/>' current GroupIndex
        /// values (a new boundary wherever a step's GroupIndex differs from its predecessor's),
        /// plus a forced boundary on both sides of every inspection step regardless of what its
        /// raw GroupIndex says - a QA checkpoint is always its own group
        /// (SwCustomPropertyService.ValidateProcessGrouping's inspection-standalone rule).
        /// Direct port of ProcessGroupingControl's own DeriveBoundaries.</summary>
        private static HashSet<int> DeriveBoundaries(List<ProcessStep> steps)
        {
            var boundaries = new HashSet<int>();
            int? previousGroupIndex = null;

            for (int i = 0; i < steps.Count; i++)
            {
                if (previousGroupIndex.HasValue && steps[i].GroupIndex != previousGroupIndex.Value)
                    boundaries.Add(i);

                previousGroupIndex = steps[i].GroupIndex;
            }

            for (int i = 1; i < steps.Count; i++)
            {
                if (steps[i - 1].Role == StepRole.Inspection || steps[i].Role == StepRole.Inspection)
                    boundaries.Add(i);
            }

            return boundaries;
        }

        /// <summary>Writes contiguous, 0-based GroupIndex values onto <paramref name="steps"/>
        /// for the groups <paramref name="boundaries"/> implies - direct port of
        /// ProcessGroupingControl's own Groups property + ApplyGroupIndexes loop, folded into
        /// one step since nothing else here ever needs the boundary-derived groups on their
        /// own.</summary>
        private static void ApplyBoundaries(List<ProcessStep> steps, HashSet<int> boundaries)
        {
            int group = -1;
            for (int i = 0; i < steps.Count; i++)
            {
                if (i == 0 || boundaries.Contains(i))
                    group++;

                steps[i].GroupIndex = group;
            }
        }

        /// <summary>Canonical finishing step for every edit that changes list membership, order
        /// or a step's role: re-normalizes contiguous group runs
        /// (SwCustomPropertyService.NormalizeProcessGroups - the same port the backend uses),
        /// then re-derives and reapplies boundaries so a step that's now an inspection is forced
        /// standalone, replaces _steps' contents in place (Clear + AddRange - keeps the List
        /// reference the host handed to SetSteps valid, see the class doc comment), rebuilds the
        /// rows and raises RoutingChanged. A plain boundary toggle doesn't go through this - see
        /// ToggleBoundary - since it already has an explicit boundary set to apply and calling
        /// NormalizeProcessGroups first would just re-derive the boundaries the click is trying
        /// to override.</summary>
        private void CommitStructuralChange()
        {
            var normalized = SwCustomPropertyService.NormalizeProcessGroups(_steps);
            ApplyBoundaries(normalized, DeriveBoundaries(normalized));

            _steps.Clear();
            _steps.AddRange(normalized);

            RebuildRows();
            OnRoutingChanged();
        }

        /// <summary>Appends a step from the trailing row's pick (new group at the end, role from
        /// whichever catalogue the choice came from), removes a filled row's step when its combo
        /// is set back to the empty entry, or updates an existing step's role/method id -
        /// mirrors AddMadeItemStep/buttonMadeItemStepRemove_Click/
        /// comboBoxMadeItemStepMethod_SelectedIndexChanged from the old step editor, folded into
        /// one handler since every row's combobox now does all three jobs.</summary>
        private void HandleRowMethodChanged(int rowIndex, MethodChoice choice)
        {
            if (choice == null)
                return;

            bool isTrailing = rowIndex == _steps.Count;
            bool isEmpty = ReferenceEquals(choice, MethodChoice.Empty);

            if (isTrailing)
            {
                if (isEmpty)
                    return;

                var step = new ProcessStep { Role = choice.Role, MethodName = choice.Name };
                if (choice.Role == StepRole.Fabrication)
                    step.FabricationMethodId = choice.Id;
                else
                    step.InspectionMethodId = choice.Id;

                // A freshly added step never silently inherits grouping it wasn't part of -
                // CommitStructuralChange's NormalizeProcessGroups then renumbers this
                // contiguously with the rest.
                step.GroupIndex = _steps.Count == 0 ? 0 : _steps[_steps.Count - 1].GroupIndex + 1;

                _steps.Add(step);
                CommitStructuralChange();
                return;
            }

            if (rowIndex < 0 || rowIndex >= _steps.Count)
                return;

            if (isEmpty)
            {
                _steps.RemoveAt(rowIndex);
                CommitStructuralChange();
                return;
            }

            var existing = _steps[rowIndex];
            existing.Role = choice.Role;
            existing.FabricationMethodId = choice.Role == StepRole.Fabrication ? choice.Id : (int?)null;
            existing.InspectionMethodId = choice.Role == StepRole.Inspection ? choice.Id : (int?)null;
            existing.MethodName = choice.Name;

            // Always goes through the full commit, not just a display refresh: switching a
            // step's role changes whether it must be forced standalone (see DeriveBoundaries).
            CommitStructuralChange();
        }

        private void MoveStep(int index, int direction)
        {
            int target = index + direction;
            if (index < 0 || index >= _steps.Count || target < 0 || target >= _steps.Count)
                return;

            var step = _steps[index];
            _steps.RemoveAt(index);
            _steps.Insert(target, step);
            CommitStructuralChange();
        }

        /// <summary>The grouping control's own click, folded in: toggles whether the step at
        /// <paramref name="index"/> is grouped with the one above it, then commits directly via
        /// ApplyBoundaries (see CommitStructuralChange's doc comment for why this doesn't route
        /// through NormalizeProcessGroups first).</summary>
        private void ToggleBoundary(int index)
        {
            var boundaries = DeriveBoundaries(_steps);
            if (boundaries.Contains(index))
                boundaries.Remove(index);
            else
                boundaries.Add(index);

            ApplyBoundaries(_steps, boundaries);

            RebuildRows();
            OnRoutingChanged();
        }

        /// <summary>Writes a note edit straight onto the step - deliberately does not go through
        /// RebuildRows (nothing structural changed) since that would recreate this very TextBox
        /// mid-edit and drop focus/cursor position on every keystroke.</summary>
        private void HandleNoteChanged(int rowIndex, TextBox textBox)
        {
            if (_suppressChange || rowIndex < 0 || rowIndex >= _steps.Count)
                return;

            _steps[rowIndex].Note = textBox.Text;
            OnRoutingChanged();
        }

        /// <summary>Tears down and rebuilds every row panel and boundary control from the
        /// current _steps - the one place that owns _dynamicControls' lifetime, so every tooltip
        /// association (_toolTip.RemoveAll) and every panel/button (Controls.Remove + Dispose)
        /// from the previous population is cleared before the next one is built, never leaking
        /// either. Guarded by _suppressChange for the whole call: reassigning a freshly-built
        /// combo's Items or restoring a note box's Text fires the very change handlers a real
        /// user edit would, and none of that is a user edit while rows are being
        /// (re)constructed.</summary>
        private void RebuildRows()
        {
            bool wasSuppressed = _suppressChange;
            _suppressChange = true;
            SuspendLayout();
            try
            {
                // Every arrow button, boundary button and forced marker below registers a
                // tooltip against _toolTip - clear those associations before disposing the
                // controls themselves, or _toolTip keeps a growing list of associations for
                // controls that no longer exist.
                _toolTip.RemoveAll();

                foreach (var control in _dynamicControls)
                {
                    Controls.Remove(control);
                    control.Dispose();
                }

                _dynamicControls.Clear();

                int stepCount = _steps.Count;
                var boundaries = DeriveBoundaries(_steps);
                int width = Math.Max(ClientSize.Width, MinimumWidth);
                // Recorded before the Height assignment below, which raises Resize on its own
                // (see _lastWidth's doc comment) - OnResize must see this rebuild's width as
                // already current or it would recursively re-enter RebuildRows for a resize that
                // never actually touched the width.
                _lastWidth = width;

                for (int i = 0; i <= stepCount; i++)
                {
                    int rowTop = i * RowSpacing;

                    if (i > 0 && i < stepCount)
                    {
                        bool forced = _steps[i - 1].Role == StepRole.Inspection
                            || _steps[i].Role == StepRole.Inspection;
                        AddBoundaryControl(
                            i, rowTop - RowSpacing + BoundaryTopOffset, boundaries.Contains(i), forced, width);
                    }

                    AddRow(i, i < stepCount ? _steps[i] : null, i == stepCount, rowTop, width);
                }

                Height = stepCount * RowSpacing + RowHeight;
            }
            finally
            {
                ResumeLayout(true);
                _suppressChange = wasSuppressed;
            }
        }

        /// <summary>Rebuilds the rows whenever the control's actual width changes (a pane
        /// resize/dock-width change propagated down via the Designer's Anchor on
        /// processRoutingControl1) - a plain per-child Anchor only tracks a resize that happens
        /// after the child was added, so without this, every add/remove/reorder's own RebuildRows
        /// would snap every row back to whatever width _lastWidth was at last check. Guarded
        /// against RebuildRows' own Height assignment via _lastWidth - see its doc comment.
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (ClientSize.Width == _lastWidth)
                return;

            RebuildRows();
        }

        /// <summary>Builds one row panel from the fixed template: title, method combobox, note
        /// label/box, and two move arrows, all laid out against <paramref name="width"/> (the
        /// control's current ClientSize.Width - see RebuildRows/OnResize) rather than a fixed
        /// constant, so a row always spans the full pane instead of the 267px it was originally
        /// hand-placed at. <paramref name="step"/> is null exactly when
        /// <paramref name="isTrailing"/> is true - the always-present empty trailing row shares
        /// the exact same template, only the note box and arrows disabled, so the "add a step"
        /// affordance never looks like a different kind of control from the steps already
        /// routed.</summary>
        private void AddRow(int index, ProcessStep step, bool isTrailing, int rowTop, int width)
        {
            var panel = new Panel
            {
                Location = new Point(0, rowTop),
                Size = new Size(width, RowHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };

            var title = new Label
            {
                AutoSize = true,
                Location = new Point(3, 3),
                Text = string.Format(
                    CultureInfo.CurrentCulture, Strings.ProcessRoutingRowTitleFormat, index + 1),
            };

            // Right edge at width - 3, matching the 6px-in/3px-margin the sibling text boxes in
            // panelMadeItemProperties use (that panel is 3px wider than this control).
            int comboWidth = width - 6 - 3;
            var choices = BuildRowChoices(step);
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(6, 19),
                Size = new Size(comboWidth, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = !ReadOnly,
                DisplayMember = "DisplayName",
            };
            // Items, not DataSource: this combo has no parent yet (it's added to panel, and
            // panel to this control, further below), so it has no BindingContext -
            // ListControl.SetDataConnection would defer a DataSource binding until it's
            // parented, silently dropping the SelectedItem assigned here in favour of whatever
            // the eventual binding's CurrencyManager position picks (index 0 - the empty entry)
            // once RebuildRows adds this panel to Controls. Plain Items needs no BindingContext,
            // so the selection set here sticks.
            combo.Items.AddRange(choices.ToArray());
            combo.SelectedItem = ChoiceFor(step, choices);
            combo.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressChange)
                    return;

                var picked = combo.SelectedItem as MethodChoice;
                // Deferred past this event's own dispatch - CommitStructuralChange rebuilds
                // rows, which disposes this very combo, and WinForms controls shouldn't dispose
                // themselves from inside their own event handler. Mirrors the same
                // BeginInvoke-deferred pattern GladosTaskPaneControl already uses for
                // checkedListBoxMadeItemArticleGroups.ItemCheck.
                BeginInvoke(new Action(() => HandleRowMethodChanged(index, picked)));
            };

            var noteLabel = new Label
            {
                AutoSize = true,
                Location = new Point(3, 43),
                Text = Strings.LabelMadeItemStepNoteText,
            };

            // The arrow buttons sit to the note box's right, so the note box is narrower than
            // the combo above it - both track width, just against different right edges.
            int arrowX = width - 3 - 21;
            int noteWidth = (arrowX - 6) - 6;
            var noteBox = new TextBox
            {
                Multiline = true,
                Location = new Point(6, 59),
                Size = new Size(noteWidth, 50),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = !isTrailing && !ReadOnly,
                Text = step != null ? step.Note ?? string.Empty : string.Empty,
            };
            noteBox.TextChanged += (s, e) => HandleNoteChanged(index, noteBox);

            var upButton = CreateArrowButton(
                MaterialIcons.KeyboardArrowUp,
                Strings.TooltipProcessRoutingMoveUp,
                new Point(arrowX, 60),
                !isTrailing && index > 0 && !ReadOnly);
            upButton.Click += (s, e) => BeginInvoke(new Action(() => MoveStep(index, -1)));

            var downButton = CreateArrowButton(
                MaterialIcons.KeyboardArrowDown,
                Strings.TooltipProcessRoutingMoveDown,
                new Point(arrowX, 87),
                !isTrailing && index < _steps.Count - 1 && !ReadOnly);
            downButton.Click += (s, e) => BeginInvoke(new Action(() => MoveStep(index, 1)));

            panel.Controls.Add(title);
            panel.Controls.Add(combo);
            panel.Controls.Add(noteLabel);
            panel.Controls.Add(noteBox);
            panel.Controls.Add(upButton);
            panel.Controls.Add(downButton);

            Controls.Add(panel);
            _dynamicControls.Add(panel);
        }

        private Button CreateArrowButton(string glyph, string tooltip, Point location, bool enabled)
        {
            var button = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Location = location,
                Size = new Size(21, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = FontManager.GetFont("Material Icons", 10),
                Text = glyph,
                Enabled = enabled,
                UseVisualStyleBackColor = true,
            };
            button.FlatAppearance.BorderSize = 0;
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        /// <summary>Either the clickable link/split toggle between two fabrication rows, or a
        /// fixed, dimmed split marker when either neighbour is an inspection step - mirrors
        /// ProcessGroupingControl.CreateBoundaryControl, just using LinkOff (closer visually to
        /// Link than the old CallSplit workaround, now that a real link_off glyph is verified
        /// available). A linked boundary draws Link in AccentColor - the grouped state is the
        /// one worth drawing the eye to - and a split boundary draws LinkOff in MutedColor; this
        /// deliberately inverts ProcessGroupingControl's own convention, which accented the
        /// split state instead. The forced, non-interactive marker always uses LinkOff in
        /// MutedColor regardless of which way the toggle itself reads.</summary>
        private void AddBoundaryControl(int index, int y, bool isSplit, bool forced, int width)
        {
            int x = (width - BoundaryWidth) / 2;

            if (forced)
            {
                var marker = new Label
                {
                    AutoSize = false,
                    Location = new Point(x, y),
                    Size = new Size(BoundaryWidth, BoundaryHeight),
                    Font = FontManager.GetFont("Material Icons", 12),
                    Text = MaterialIcons.LinkOff,
                    ForeColor = MutedColor,
                    TextAlign = ContentAlignment.MiddleCenter,
                };
                _toolTip.SetToolTip(marker, Strings.MadeItemInspectionStandaloneTooltip);

                Controls.Add(marker);
                _dynamicControls.Add(marker);
                return;
            }

            var button = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Location = new Point(x, y),
                Size = new Size(BoundaryWidth, BoundaryHeight),
                Font = FontManager.GetFont("Material Icons", 12),
                Text = isSplit ? MaterialIcons.LinkOff : MaterialIcons.Link,
                ForeColor = isSplit ? MutedColor : _accentColor,
                Enabled = !ReadOnly,
                UseVisualStyleBackColor = true,
            };
            button.FlatAppearance.BorderSize = 0;
            // Tooltip describes the action a click performs, not the current state - a split
            // boundary's click links, so it carries the "link" tooltip, and vice versa.
            _toolTip.SetToolTip(button, isSplit ? Strings.TooltipProcessRoutingLink : Strings.TooltipProcessRoutingSplit);
            button.Click += (s, e) => BeginInvoke(new Action(() => ToggleBoundary(index)));

            Controls.Add(button);
            _dynamicControls.Add(button);
        }

        /// <summary>One row's combobox items: the shared empty-first-then-fabrication-then-
        /// inspection model from SetMethodChoices, plus one extra entry when
        /// <paramref name="step"/> references a method id that isn't in either pickable list
        /// (archived or unknown) - mirrors the old SetMadeItemStepMethodChoices. The extra
        /// entry's label falls back to step.MethodName (already resolved from the full lookup
        /// snapshot, archived rows included - see GladosTaskPaneControl.ResolveStepMethodNames)
        /// or "#&lt;id&gt;" when even that couldn't be resolved (offline, empty cache).</summary>
        private List<MethodChoice> BuildRowChoices(ProcessStep step)
        {
            var items = new List<MethodChoice> { MethodChoice.Empty };
            items.AddRange(_fabricationChoices);
            items.AddRange(_inspectionChoices);

            if (step != null && step.MethodId.HasValue
                && !items.Any(c => c.Role == step.Role && c.Id == step.MethodId.Value))
            {
                string label = !string.IsNullOrWhiteSpace(step.MethodName)
                    ? step.MethodName
                    : "#" + step.MethodId.Value.ToString(CultureInfo.InvariantCulture);
                items.Add(new MethodChoice(step.MethodId.Value, label, label, step.Role));
            }

            return items;
        }

        private static MethodChoice ChoiceFor(ProcessStep step, List<MethodChoice> choices)
        {
            if (step == null || !step.MethodId.HasValue)
                return MethodChoice.Empty;

            return choices.FirstOrDefault(c => c.Role == step.Role && c.Id == step.MethodId.Value)
                ?? MethodChoice.Empty;
        }
    }
}

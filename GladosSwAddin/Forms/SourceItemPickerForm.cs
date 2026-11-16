using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Helpers;
using GladosSwAddin.Resources;
using GladosSwAddin.Services;

namespace GladosSwAddin.Forms
{
    /// <summary>
    /// Modal picker for a made item's raw-material source item, opened from
    /// GladosTaskPaneControl.SelectSourceItemAsync via ShowDialog(this). Browse data is the
    /// offline lookup snapshot (ApplicationServices.Lookups.Current.SourceItems/
    /// SourceItemTypes/Suppliers) filtered client-side — the same snapshot the task pane's other
    /// pickers read, so this works offline too. The one Glados write this add-in ever performs —
    /// creating a source item from the inline Create panel — lives entirely in this form; picking
    /// an existing row writes nothing here, the caller writes the id/label to the document on
    /// Save.
    ///
    /// The Create panel's dimension row (comboBoxCreatePreset + the four textBoxCreateDimension*
    /// boxes) mirrors the web's SourceItemDimensionInput.vue shape selector — see
    /// GladosSwAddin.Helpers.DimensionSpec for the composition/canonicalisation rules shared with
    /// glados/frontend's dimensionSpec.ts and glados/backend's dimension_spec.py.
    /// </summary>
    public partial class SourceItemPickerForm : Form
    {
        private const string RequiredCreatePermission = "write:source_items";

        private static readonly System.Drawing.Color CreateErrorColor = System.Drawing.Color.Red;
        private static readonly System.Drawing.Color CreateInfoColor = System.Drawing.Color.DarkGreen;

        private readonly bool _isConnected;

        /// <summary>The made item's current Material, prefilled into textBoxCreateMaterial (only
        /// when that box is still empty) so creating a source item for the part in hand doesn't
        /// require retyping its material — see PrefillCreateForm/ClearCreateForm.</summary>
        private readonly string _madeItemMaterial;

        /// <summary>The made item's current BaseSize (Glados.BaseSize on the document) — shown
        /// read-only via labelBaseSize and, when DimensionSpec.TryParse recognises it as one of
        /// the four structured shapes, used to preselect comboBoxCreatePreset and prefill the
        /// dimension boxes on load (see PrefillCreateForm). Never re-derived from the Create
        /// panel afterwards — this is a one-time convenience seed, not a live binding.</summary>
        private readonly string _madeItemBaseSize;

        /// <summary>textBoxCreateDimensionWidth's designer-authored Width, captured once so
        /// ApplyDimensionShape can widen it to span the row for Custom and restore it for every
        /// other shape.</summary>
        private readonly int _defaultDimensionWidthWidth;

        /// <summary>Items created via the panel below but not yet reflected in
        /// ApplicationServices.Lookups.Current (the background refresh kicked off after a
        /// successful create hasn't landed yet) — merged into the filtered grid so the new row
        /// is visible immediately, rather than waiting on that refresh. A row here is superseded,
        /// not duplicated, once the real snapshot catches up — see ApplyFilters.</summary>
        private readonly List<SourceItemResponse> _locallyCreatedItems = new List<SourceItemResponse>();

        private readonly ToolTip _createTooltip = new ToolTip();

        /// <summary>The row currently selected in the grid — preselected from the id passed into
        /// the constructor (the document's current pick, even if archived), then tracked live off
        /// dataGridViewSourceItems.SelectionChanged. Null means nothing selected.</summary>
        private int? _selectedId;

        /// <summary>The signed-in user's permissions from GET /pat/users/me, fetched once in
        /// SourceItemPickerForm_Load. Null both before that call completes and when it fails or
        /// the wire carries no permissions field at all — either way read as "unknown", which
        /// ApplyCreateAvailability treats as "don't proactively block, let the backend's 403
        /// decide".</summary>
        private List<string> _userPermissions;

        /// <summary>The shape comboBoxCreatePreset currently has selected — drives which
        /// dimension boxes are visible/labelled and how TryComposeDimension reads them. Kept in
        /// sync with the combo purely through its SelectedIndexChanged handler
        /// (comboBoxCreatePreset_SelectedIndexChanged); never set directly outside of it.</summary>
        private DimensionShape _dimensionShape = DimensionShape.Block;

        /// <summary>The existing source item that exactly matches the panel's current type,
        /// material, manufacturer and canonical dimension — recomputed by UpdateDuplicateDetection
        /// after every relevant field change. Non-null disables buttonCreateSubmit (Use, on the
        /// row UpdateDuplicateDetection selects, is the way forward instead) — see the web's own
        /// inline duplicate-detection mode this mirrors.</summary>
        private SourceItemResponse _duplicateMatch;

        /// <summary>True while labelCreateError is showing the duplicate notice rather than a
        /// validation/API error — so UpdateDuplicateDetection knows it's safe to clear the label
        /// once the duplicate goes away, without also clearing an unrelated error a Create attempt
        /// left behind.</summary>
        private bool _duplicateNoticeShown;

        /// <summary>The picked row once the dialog closes with DialogResult.OK — null on
        /// Cancel/Esc. The composed label (SourceItemLabelComposer) the caller writes is derived
        /// from this at Save time, not carried here, so it always reflects the lookup snapshot
        /// at the moment of Save rather than of picking.</summary>
        public SourceItemResponse SelectedSourceItem { get; private set; }

        /// <summary>Builds the picker preselected on <paramref name="currentSourceItemId"/> and
        /// filtered from the current lookup snapshot.</summary>
        /// <param name="isConnected">Mirrors GladosTaskPaneControl._isConnected — gates the
        /// Create panel (disabled while disconnected), not the picker itself, which works from
        /// the offline snapshot regardless.</param>
        /// <param name="currentSourceItemId">The made tab's current pick, if any — preselected in
        /// the grid even if archived.</param>
        /// <param name="material">The made item's current Material (_madeItemLocal.Material at
        /// the call site) — prefilled into the Create panel's Material box; null-safe.</param>
        /// <param name="baseSize">The made item's current BaseSize (_madeItemLocal.BaseSize) —
        /// shown via labelBaseSize and, when recognised, used to preselect a shape and prefill
        /// the dimension boxes; null-safe.</param>
        public SourceItemPickerForm(bool isConnected, int? currentSourceItemId, string material, string baseSize)
        {
            InitializeComponent();

            _isConnected = isConnected;
            _selectedId = currentSourceItemId;
            _madeItemMaterial = material;
            _madeItemBaseSize = baseSize;
            _defaultDimensionWidthWidth = textBoxCreateDimensionWidth.Width;

            ApplyLocalizedStaticText();
            PopulateFilterChoices();
            PopulateCreatePresetChoices();
            WireCreatePanelEvents();
            SelectPreset(DimensionShape.Block);
            ApplyFilters();
            ApplyCreateAvailability();
            PrefillCreateForm();

            ApplicationServices.Lookups.Changed += OnLookupsChanged;
            Disposed += (s, e) =>
            {
                ApplicationServices.Lookups.Changed -= OnLookupsChanged;
                _createTooltip.Dispose();
            };
        }

        /// <summary>Proactive permission check: GET /pat/users/me once, while the dialog is
        /// already showing with the offline-derived state from the constructor. A failed or
        /// older-backend call (no Permissions field) leaves _userPermissions null — "unknown",
        /// not "denied" — so Create stays enabled and any actual lack of permission surfaces via
        /// the 403 the create call itself would get (buttonCreateSubmit_Click).</summary>
        private async void SourceItemPickerForm_Load(object sender, EventArgs e)
        {
            if (!_isConnected)
                return;

            try
            {
                var me = await ApplicationServices.Api.Users.GetMeAsync();
                _userPermissions = me?.Permissions;
            }
            catch (GladosApiException)
            {
                _userPermissions = null;
            }
            catch (HttpRequestException)
            {
                _userPermissions = null;
            }

            ApplyCreateAvailability();
        }

        private void ApplyLocalizedStaticText()
        {
            Text = Strings.SourceItemPickerTitleText;

            labelFilterSearch.Text = Strings.LabelSourceItemSearchText;
            labelFilterType.Text = Strings.LabelSourceItemTypeText;
            labelFilterMaterial.Text = Strings.LabelMaterialText;
            labelFilterDimension.Text = Strings.LabelSourceItemDimensionText;
            labelFilterManufacturer.Text = Strings.LabelManufacturerText;
            checkBoxShowArchived.Text = Strings.CheckBoxSourceItemShowArchivedText;
            buttonRefresh.Text = Strings.ButtonSourceItemRefreshText;

            columnType.HeaderText = Strings.LabelSourceItemTypeText;
            columnDimension.HeaderText = Strings.LabelSourceItemDimensionText;
            columnMaterial.HeaderText = Strings.LabelMaterialText;
            columnMass.HeaderText = Strings.LabelMassText;
            columnManufacturer.HeaderText = Strings.LabelManufacturerText;
            columnOrderCode1.HeaderText = Strings.LabelOrderCode1Text;
            columnStatus.HeaderText = Strings.LabelSourceItemStatusText;

            groupBoxCreateSourceItem.Text = Strings.GroupBoxSourceItemCreateText;
            labelCreateDimension.Text = Strings.LabelSourceItemDimensionText;
            // The separator glyphs are set here rather than trusted to the designer file's own
            // encoding — see DimensionSpec's callers below for why "Ø"/"×" specifically matter.
            labelCreateDimensionD.Text = "Ø";
            labelCreateDimensionX1.Text = "×";
            labelCreateDimensionX2.Text = "×";
            labelCreateDimensionX3.Text = "×";
            labelCreateType.Text = Strings.LabelSourceItemTypeText;
            labelCreateMaterial.Text = Strings.LabelMaterialText;
            labelCreateMass.Text = Strings.LabelMassText;
            labelCreateManufacturer.Text = Strings.LabelManufacturerText;
            labelCreateOrderCode1.Text = Strings.LabelOrderCode1Text;
            labelCreateOrderCode2.Text = Strings.LabelOrderCode2Text;
            labelCreateNote.Text = Strings.LabelSourceItemNoteText;
            buttonCreateSubmit.Text = Strings.ButtonSourceItemCreateText;
            labelCreateError.Text = string.Empty;

            buttonUse.Text = Strings.ButtonSourceItemUseText;
            buttonCancel.Text = Strings.ButtonSourceItemCancelText;
        }

        #region Filter/lookup choices

        private void PopulateFilterChoices()
        {
            var snapshot = ApplicationServices.Lookups.Current;
            var types = LookupOrder.HandKept(
                    snapshot.SourceItemTypes, t => t.SortOrder, t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var suppliers = snapshot.Suppliers.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

            BindCombo(comboBoxFilterType, types.ToList());
            BindCombo(comboBoxFilterManufacturer, suppliers.ToList());
            BindCombo(comboBoxCreateType, types.ToList());
            BindCombo(comboBoxCreateManufacturer, suppliers.ToList());
        }

        /// <summary>Reassigns DataSource with its own list instance per combo, mirroring
        /// GladosTaskPaneControl.BindManufacturerChoices — binding the very same list object to
        /// two combos would otherwise link their SelectedIndex through a shared
        /// CurrencyManager.</summary>
        private static void BindCombo<T>(ComboBox combo, List<T> items)
        {
            var selected = combo.SelectedValue as int?;
            combo.DataSource = null;
            combo.DisplayMember = "Name";
            combo.ValueMember = "Id";
            combo.DataSource = items;
            if (selected.HasValue)
                combo.SelectedValue = selected.Value;
            else
                combo.SelectedIndex = -1;
        }

        /// <summary>Marshals LookupService.Changed to the UI thread itself, per the event's own
        /// doc comment — a background RefreshAsync (Reload, or the one this form starts after a
        /// successful create) can land on any thread.</summary>
        private void OnLookupsChanged(object sender, EventArgs e)
        {
            OnUiThread(() =>
            {
                PopulateFilterChoices();
                ApplyFilters();
                UpdateDuplicateDetection();
            });
        }

        private void OnUiThread(Action action)
        {
            if (IsHandleCreated && InvokeRequired)
                Invoke(action);
            else
                action();
        }

        #endregion

        #region Grid filtering and selection

        private void Filter_Changed(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        /// <summary>Rebuilds the grid from the current lookup snapshot plus any just-created,
        /// not-yet-synced rows (_locallyCreatedItems), applying every active filter client-side.
        /// Archived rows are hidden unless "Show archived" is checked or the row is the one
        /// currently selected (_selectedId) — same "keep the current pick visible even if
        /// archived" rule as GladosTaskPaneControl.VisibleRows, greyed exactly like the settings
        /// grid's unknown-field rows (SettingsForm.LoadFieldMapping).</summary>
        private void ApplyFilters()
        {
            var snapshot = ApplicationServices.Lookups.Current;
            var knownIds = new HashSet<int>(snapshot.SourceItems.Select(i => i.Id));
            var all = snapshot.SourceItems
                .Concat(_locallyCreatedItems.Where(i => !knownIds.Contains(i.Id)));
            var typeOrder = LookupOrder.SortOrderById(snapshot.SourceItemTypes, t => t.Id, t => t.SortOrder);

            var search = textBoxFilterSearch.Text.Trim();
            var material = textBoxFilterMaterial.Text.Trim();
            var dimension = textBoxFilterDimension.Text.Trim();
            var typeId = comboBoxFilterType.SelectedValue as int?;
            var manufacturerId = comboBoxFilterManufacturer.SelectedValue as int?;
            var showArchived = checkBoxShowArchived.Checked;

            bool MatchesText(string haystack, string needle) =>
                string.IsNullOrEmpty(needle)
                || (haystack ?? string.Empty).IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;

            // The selected row (the document's current source item, or the duplicate the
            // create panel just found) is always shown, whatever the filters say — the same
            // rule the archived toggle already follows — so a selection is never invisible.
            bool MatchesFilters(SourceItemResponse item) =>
                (!item.Archived || showArchived)
                && (!typeId.HasValue || item.TypeId == typeId.Value)
                && (!manufacturerId.HasValue || item.ManufacturerId == manufacturerId.Value)
                && MatchesText(item.Material, material)
                && MatchesText(item.Dimension, dimension)
                && (string.IsNullOrEmpty(search)
                    || MatchesText(item.TypeName, search)
                    || MatchesText(item.Dimension, search)
                    || MatchesText(item.Material, search)
                    || MatchesText(item.OrderCode1, search)
                    || MatchesText(item.OrderCode2, search));

            var filtered = all
                .Where(item => item.Id == _selectedId || MatchesFilters(item))
                .OrderBy(item => typeOrder(item.TypeId))
                .ThenBy(item => item.TypeName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Dimension, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            RepopulateGrid(filtered);
        }

        private void RepopulateGrid(List<SourceItemResponse> items)
        {
            dataGridViewSourceItems.SuspendLayout();
            dataGridViewSourceItems.Rows.Clear();
            foreach (var item in items)
            {
                int rowIndex = dataGridViewSourceItems.Rows.Add(
                    item.TypeName,
                    item.Dimension,
                    item.Material,
                    item.Mass?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
                    item.ManufacturerName,
                    item.OrderCode1,
                    StatusDisplayText(item.Status));

                var row = dataGridViewSourceItems.Rows[rowIndex];
                row.Tag = item;
                if (item.Archived)
                    row.DefaultCellStyle.ForeColor = System.Drawing.Color.Gray;
            }
            dataGridViewSourceItems.ResumeLayout();

            SelectRowById(_selectedId);
            UpdateDetailPanel();
            UpdateUseEnabled();
        }

        private void SelectRowById(int? id)
        {
            dataGridViewSourceItems.ClearSelection();
            if (!id.HasValue)
                return;

            foreach (DataGridViewRow row in dataGridViewSourceItems.Rows)
            {
                if (((SourceItemResponse)row.Tag).Id == id.Value)
                {
                    row.Selected = true;
                    dataGridViewSourceItems.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }

        private SourceItemResponse CurrentSelectedItem()
        {
            return dataGridViewSourceItems.SelectedRows.Count > 0
                ? (SourceItemResponse)dataGridViewSourceItems.SelectedRows[0].Tag
                : null;
        }

        private void dataGridViewSourceItems_SelectionChanged(object sender, EventArgs e)
        {
            _selectedId = CurrentSelectedItem()?.Id;
            UpdateDetailPanel();
            UpdateUseEnabled();
        }

        private void dataGridViewSourceItems_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                UseSelected();
        }

        /// <summary>DataGridView normally consumes Enter itself (cell navigation) before it can
        /// reach the Form's AcceptButton, so "Enter = Use" (A5) needs its own handler rather than
        /// relying on AcceptButton alone.</summary>
        private void dataGridViewSourceItems_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                UseSelected();
            }
        }

        private void UpdateUseEnabled()
        {
            buttonUse.Enabled = _selectedId.HasValue && CurrentSelectedItem() != null;
        }

        private void UpdateDetailPanel()
        {
            var item = CurrentSelectedItem();
            if (item == null)
            {
                labelDetailTitle.Text = string.Empty;
                labelDetailManufacturer.Text = string.Empty;
                labelDetailOrderCode1.Text = string.Empty;
                labelDetailOrderCode2.Text = string.Empty;
                labelDetailMass.Text = string.Empty;
                labelDetailStatus.Text = string.Empty;
                labelDetailNote.Text = string.Empty;
                return;
            }

            labelDetailTitle.Text = SourceItemLabelComposer.Compose(item);
            labelDetailManufacturer.Text = Strings.LabelManufacturerText + ": " + (item.ManufacturerName ?? string.Empty);
            labelDetailOrderCode1.Text = Strings.LabelOrderCode1Text + ": " + (item.OrderCode1 ?? string.Empty);
            labelDetailOrderCode2.Text = Strings.LabelOrderCode2Text + ": " + (item.OrderCode2 ?? string.Empty);
            labelDetailMass.Text = Strings.LabelMassText + ": "
                + (item.Mass?.ToString(CultureInfo.CurrentCulture) ?? string.Empty);
            labelDetailStatus.Text = Strings.LabelSourceItemStatusText + ": " + StatusDisplayText(item.Status);
            labelDetailNote.Text = Strings.LabelSourceItemNoteText + ": " + (item.Note ?? string.Empty);
        }

        private static string StatusDisplayText(SourceItemCatalogueStatus status)
        {
            switch (status)
            {
                case SourceItemCatalogueStatus.Active:
                    return Strings.StateActive;
                case SourceItemCatalogueStatus.Inactive:
                    return Strings.StateInactive;
                case SourceItemCatalogueStatus.Replaced:
                    return Strings.StateReplaced;
                default:
                    return status.ToString();
            }
        }

        #endregion

        #region Use / Cancel

        private void buttonUse_Click(object sender, EventArgs e)
        {
            UseSelected();
        }

        private void UseSelected()
        {
            var item = CurrentSelectedItem();
            if (item == null)
                return;

            SelectedSourceItem = item;
            DialogResult = DialogResult.OK;
            Close();
        }

        #endregion

        #region Refresh

        private async void buttonRefresh_Click(object sender, EventArgs e)
        {
            buttonRefresh.Enabled = false;
            try
            {
                // Never throws (LookupService.RefreshAsync's own doc) - a transport failure just
                // leaves whatever the snapshot already holds serving the grid.
                await ApplicationServices.Lookups.RefreshAsync(ApplicationServices.Api.Lookups);
            }
            finally
            {
                buttonRefresh.Enabled = true;
            }
        }

        /// <summary>Fire-and-forget background refresh after a successful create - never awaited
        /// by the caller, mirrors GladosTaskPaneControl.RefreshAuthStateAsync's own
        /// "RunGuarded(() => ...RefreshAsync(...))" fire-and-forget call.</summary>
        private async void StartBackgroundLookupRefresh()
        {
            try
            {
                await ApplicationServices.Lookups.RefreshAsync(ApplicationServices.Api.Lookups);
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Warning("Background lookup refresh failed", ex);
            }
        }

        #endregion

        #region Create panel - shape preset

        /// <summary>Wraps one comboBoxCreatePreset entry — a plain Items-based combo (unlike the
        /// lookup-bound combos above) since the five shapes are fixed, not drawn from the lookup
        /// snapshot. ToString() drives the combo's own rendering, so no DisplayMember is set.</summary>
        private sealed class DimensionShapeItem
        {
            public DimensionShape Shape { get; }
            public string DisplayName { get; }

            public DimensionShapeItem(DimensionShape shape, string displayName)
            {
                Shape = shape;
                DisplayName = displayName;
            }

            public override string ToString() => DisplayName;
        }

        /// <summary>Same five shapes, same order, as the web's shape selector — see
        /// SourceItemDimensionInput.vue's dimensionShapeOptions.</summary>
        private void PopulateCreatePresetChoices()
        {
            comboBoxCreatePreset.Items.Clear();
            comboBoxCreatePreset.Items.Add(new DimensionShapeItem(DimensionShape.Block, Strings.DimensionShapeBlockText));
            comboBoxCreatePreset.Items.Add(new DimensionShapeItem(DimensionShape.Profile, Strings.DimensionShapeProfileText));
            comboBoxCreatePreset.Items.Add(new DimensionShapeItem(DimensionShape.TurnedPart, Strings.DimensionShapeTurnedPartText));
            comboBoxCreatePreset.Items.Add(new DimensionShapeItem(DimensionShape.Tube, Strings.DimensionShapeTubeText));
            comboBoxCreatePreset.Items.Add(new DimensionShapeItem(DimensionShape.Custom, Strings.DimensionShapeCustomText));
        }

        private void WireCreatePanelEvents()
        {
            comboBoxCreatePreset.SelectedIndexChanged += ComboBoxCreatePreset_SelectedIndexChanged;
            textBoxCreateDimensionWidth.TextChanged += (s, e) => UpdateDuplicateDetection();
            textBoxCreateDimensionHeight.TextChanged += (s, e) => UpdateDuplicateDetection();
            textBoxCreateDimensionLength.TextChanged += (s, e) => UpdateDuplicateDetection();
            textBoxCreateDimensionWallThickness.TextChanged += (s, e) => UpdateDuplicateDetection();
            comboBoxCreateType.SelectedIndexChanged += (s, e) => UpdateDuplicateDetection();
            textBoxCreateMaterial.TextChanged += (s, e) => UpdateDuplicateDetection();
            comboBoxCreateManufacturer.SelectedIndexChanged += (s, e) => UpdateDuplicateDetection();
        }

        private void ComboBoxCreatePreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = comboBoxCreatePreset.SelectedItem as DimensionShapeItem;
            if (item == null)
                return;

            ApplyDimensionShape(item.Shape);
            UpdateDuplicateDetection();
        }

        private void SelectPreset(DimensionShape shape)
        {
            foreach (DimensionShapeItem item in comboBoxCreatePreset.Items)
            {
                if (item.Shape == shape)
                {
                    comboBoxCreatePreset.SelectedItem = item;
                    return;
                }
            }
        }

        /// <summary>Shows/hides and relabels the dimension row's controls for
        /// <paramref name="shape"/> — box1 (textBoxCreateDimensionWidth) is always visible; box2
        /// (Height), box3 (Length) and box4 (WallThickness) are shown only where the shape needs
        /// them, each paired with the "×" label that precedes it (labelCreateDimensionX1 with
        /// box2, X2 with box3, X3 with box4) so hiding a box also hides its separator. Never
        /// touches the boxes' Text — switching shapes keeps whatever the user already typed,
        /// same as the web's SourceItemDimensionInput.vue (its `numbers` survive a shape
        /// switch too).</summary>
        private void ApplyDimensionShape(DimensionShape shape)
        {
            _dimensionShape = shape;

            bool showDiameter = shape == DimensionShape.TurnedPart || shape == DimensionShape.Tube;
            bool showBox2 = shape != DimensionShape.Custom;
            bool showBox3 = shape == DimensionShape.Block || shape == DimensionShape.Profile || shape == DimensionShape.Tube;
            bool showBox4 = shape == DimensionShape.Profile;

            labelCreateDimensionD.Visible = showDiameter;

            textBoxCreateDimensionHeight.Visible = showBox2;
            labelCreateDimensionHeight.Visible = showBox2;
            labelCreateDimensionX1.Visible = showBox2;

            textBoxCreateDimensionLength.Visible = showBox3;
            labelCreateDimensionLength.Visible = showBox3;
            labelCreateDimensionX2.Visible = showBox3;

            textBoxCreateDimensionWallThickness.Visible = showBox4;
            labelCreateDimensionWallThickness.Visible = showBox4;
            labelCreateDimensionX3.Visible = showBox4;

            labelCreateDimensionWidth.Text = string.Format(
                CultureInfo.CurrentCulture,
                Strings.LabelDimensionUnitFormat,
                showDiameter ? Strings.LabelDimensionDiameterText
                    : shape == DimensionShape.Custom ? Strings.LabelSourceItemDimensionText
                    : Strings.LabelDimensionWidthText);

            if (showBox2)
            {
                labelCreateDimensionHeight.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.LabelDimensionUnitFormat,
                    showDiameter ? Strings.LabelDimensionLengthText : Strings.LabelDimensionHeightText);
            }

            if (showBox3)
            {
                labelCreateDimensionLength.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.LabelDimensionUnitFormat,
                    shape == DimensionShape.Block ? Strings.LabelDimensionThicknessText
                        : shape == DimensionShape.Tube ? Strings.LabelDimensionWallThicknessText
                        : Strings.LabelDimensionLengthText);
            }

            if (showBox4)
            {
                labelCreateDimensionWallThickness.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.LabelDimensionUnitFormat, Strings.LabelDimensionWallThicknessText);
            }

            // Custom has only one field, so it gets the rest of the row - restored to its
            // designer width for every other shape.
            textBoxCreateDimensionWidth.Width = shape == DimensionShape.Custom
                ? textBoxCreateDimensionWallThickness.Right - textBoxCreateDimensionWidth.Left
                : _defaultDimensionWidthWidth;
        }

        #endregion

        #region Create panel - prefill

        /// <summary>Seeds the Create panel from the made item's current state: Material (only
        /// when the box is still empty - never overwrites something the user already typed) and,
        /// when BaseSize parses as one of the four structured shapes, that shape and its numbers.
        /// labelBaseSize itself always shows BaseSize verbatim (or "-"), independent of whether
        /// it was recognised.</summary>
        private void PrefillCreateForm()
        {
            if (textBoxCreateMaterial.Text.Length == 0 && !string.IsNullOrWhiteSpace(_madeItemMaterial))
                textBoxCreateMaterial.Text = _madeItemMaterial.Trim();

            var baseSizeDisplay = string.IsNullOrWhiteSpace(_madeItemBaseSize) ? "-" : _madeItemBaseSize.Trim();
            labelBaseSize.Text = string.Format(CultureInfo.CurrentCulture, Strings.LabelSourceItemBaseSizeFormat, baseSizeDisplay);

            if (DimensionSpec.TryParse(_madeItemBaseSize, out var shape, out var numbers))
            {
                SelectPreset(shape);
                FillDimensionBoxes(shape, numbers);
            }

            UpdateDuplicateDetection();
        }

        private void FillDimensionBoxes(DimensionShape shape, double[] numbers)
        {
            switch (shape)
            {
                case DimensionShape.Block:
                    textBoxCreateDimensionWidth.Text = FormatForEditing(numbers[0]);
                    textBoxCreateDimensionHeight.Text = FormatForEditing(numbers[1]);
                    textBoxCreateDimensionLength.Text = FormatForEditing(numbers[2]);
                    break;
                case DimensionShape.Profile:
                    textBoxCreateDimensionWidth.Text = FormatForEditing(numbers[0]);
                    textBoxCreateDimensionHeight.Text = FormatForEditing(numbers[1]);
                    textBoxCreateDimensionLength.Text = FormatForEditing(numbers[2]);
                    textBoxCreateDimensionWallThickness.Text = FormatForEditing(numbers[3]);
                    break;
                case DimensionShape.TurnedPart:
                    textBoxCreateDimensionWidth.Text = FormatForEditing(numbers[0]);
                    textBoxCreateDimensionHeight.Text = FormatForEditing(numbers[1]);
                    break;
                case DimensionShape.Tube:
                    textBoxCreateDimensionWidth.Text = FormatForEditing(numbers[0]);
                    textBoxCreateDimensionHeight.Text = FormatForEditing(numbers[1]);
                    textBoxCreateDimensionLength.Text = FormatForEditing(numbers[2]);
                    break;
            }
        }

        /// <summary>Current-culture formatting for a box the user is about to edit further -
        /// DimensionSpec.FormatNumber is invariant on purpose (it's the wire format), so
        /// round-tripping a parsed base size back into a textbox goes through this instead.</summary>
        private static string FormatForEditing(double value)
        {
            return value.ToString("0.##########", CultureInfo.CurrentCulture);
        }

        #endregion

        #region Create panel - availability, duplicate detection, submit

        /// <summary>Disables the always-visible Create panel while disconnected, or once
        /// GET /pat/users/me has reported permissions that don't include write:source_items;
        /// the reason is shown as a tooltip on the panel's group box and its Create button.
        /// Unknown permissions (not fetched yet, older backend, or the check itself failed)
        /// leave Create enabled - the 403 a create attempt would then get is the fallback, not a
        /// proactive block.</summary>
        private void ApplyCreateAvailability()
        {
            if (!_isConnected)
            {
                SetCreateEnabled(false, Strings.TooltipSourceItemCreateDisconnected);
                return;
            }

            bool missingPermission = _userPermissions != null
                && !_userPermissions.Contains(RequiredCreatePermission, StringComparer.Ordinal);

            SetCreateEnabled(!missingPermission, missingPermission ? Strings.TooltipSourceItemCreateNoPermission : string.Empty);
        }

        private void SetCreateEnabled(bool enabled, string tooltip)
        {
            groupBoxCreateSourceItem.Enabled = enabled;
            _createTooltip.SetToolTip(groupBoxCreateSourceItem, tooltip);
            _createTooltip.SetToolTip(buttonCreateSubmit, tooltip);
        }

        /// <summary>Recomputes _duplicateMatch from the panel's current type/material/manufacturer/
        /// dimension and reflects it in the UI - called after every relevant field change
        /// (WireCreatePanelEvents) and whenever the lookup snapshot itself changes
        /// (OnLookupsChanged). Mirrors the web's inline duplicate-detection mode: found means
        /// "Use the existing one instead", not "fix your input", so it disables Create rather
        /// than erroring.</summary>
        private void UpdateDuplicateDetection()
        {
            _duplicateMatch = FindDuplicateMatch();

            if (_duplicateMatch != null)
            {
                SelectRowById(_duplicateMatch.Id);
                ShowCreateInfo(Strings.InfoSourceItemDuplicateText);
            }
            else if (_duplicateNoticeShown)
            {
                ClearCreateMessage();
            }

            buttonCreateSubmit.Enabled = _duplicateMatch == null;
        }

        /// <summary>Same match rule as the web/backend's duplicate check: same TypeId, same
        /// Material (trimmed, ordinal-ignore-case), same ManufacturerId (null == null), and an
        /// equivalent canonical dimension (DimensionSpec.Canonicalize) - against both the lookup
        /// snapshot and this session's not-yet-synced _locallyCreatedItems, same merge as
        /// ApplyFilters. Returns null while the panel's dimension isn't yet a complete, valid
        /// value (mid-typing) rather than treating that as a mismatch worth reporting.</summary>
        private SourceItemResponse FindDuplicateMatch()
        {
            if (!TryComposeDimension(out var dimension))
                return null;

            var typeId = comboBoxCreateType.SelectedValue as int?;
            if (!typeId.HasValue)
                return null;

            var material = textBoxCreateMaterial.Text.Trim();
            var manufacturerId = comboBoxCreateManufacturer.SelectedValue as int?;
            var canonical = DimensionSpec.Canonicalize(dimension);

            var knownIds = new HashSet<int>(ApplicationServices.Lookups.Current.SourceItems.Select(i => i.Id));
            var candidates = ApplicationServices.Lookups.Current.SourceItems
                .Concat(_locallyCreatedItems.Where(i => !knownIds.Contains(i.Id)));

            return candidates.FirstOrDefault(item =>
                item.TypeId == typeId.Value
                && item.ManufacturerId == manufacturerId
                && string.Equals((item.Material ?? string.Empty).Trim(), material, StringComparison.OrdinalIgnoreCase)
                && string.Equals(DimensionSpec.Canonicalize(item.Dimension), canonical, StringComparison.Ordinal));
        }

        /// <summary>Reads the currently-visible dimension boxes for _dimensionShape and composes
        /// the canonical dimension string via DimensionSpec - false when any visible numeric box
        /// isn't a positive number, or Custom's free text is empty.</summary>
        private bool TryComposeDimension(out string dimension)
        {
            dimension = null;

            switch (_dimensionShape)
            {
                case DimensionShape.Block:
                    if (!TryParsePositive(textBoxCreateDimensionWidth, out var blockWidth)
                        || !TryParsePositive(textBoxCreateDimensionHeight, out var blockHeight)
                        || !TryParsePositive(textBoxCreateDimensionLength, out var blockThickness))
                        return false;
                    dimension = DimensionSpec.ComposeBlock(blockWidth, blockHeight, blockThickness);
                    return true;

                case DimensionShape.Profile:
                    if (!TryParsePositive(textBoxCreateDimensionWidth, out var profileWidth)
                        || !TryParsePositive(textBoxCreateDimensionHeight, out var profileHeight)
                        || !TryParsePositive(textBoxCreateDimensionLength, out var profileLength)
                        || !TryParsePositive(textBoxCreateDimensionWallThickness, out var profileWallThickness))
                        return false;
                    dimension = DimensionSpec.ComposeProfile(profileWidth, profileHeight, profileLength, profileWallThickness);
                    return true;

                case DimensionShape.TurnedPart:
                    if (!TryParsePositive(textBoxCreateDimensionWidth, out var turnedDiameter)
                        || !TryParsePositive(textBoxCreateDimensionHeight, out var turnedLength))
                        return false;
                    dimension = DimensionSpec.ComposeTurnedPart(turnedDiameter, turnedLength);
                    return true;

                case DimensionShape.Tube:
                    if (!TryParsePositive(textBoxCreateDimensionWidth, out var tubeDiameter)
                        || !TryParsePositive(textBoxCreateDimensionHeight, out var tubeLength)
                        || !TryParsePositive(textBoxCreateDimensionLength, out var tubeWallThickness))
                        return false;
                    dimension = DimensionSpec.ComposeTube(tubeDiameter, tubeLength, tubeWallThickness);
                    return true;

                case DimensionShape.Custom:
                    var custom = DimensionSpec.ComposeCustom(textBoxCreateDimensionWidth.Text);
                    if (custom.Length == 0)
                        return false;
                    dimension = custom;
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryParsePositive(TextBox box, out double value)
        {
            return DimensionSpec.TryParseNumber(box.Text, out value) && value > 0;
        }

        /// <summary>Whether any currently-visible dimension box has text in it - used only to
        /// pick which validation message buttonCreateSubmit_Click shows (nothing entered yet vs.
        /// entered but invalid), not for anything functional.</summary>
        private bool AnyDimensionFieldHasText()
        {
            return HasText(textBoxCreateDimensionWidth)
                || (textBoxCreateDimensionHeight.Visible && HasText(textBoxCreateDimensionHeight))
                || (textBoxCreateDimensionLength.Visible && HasText(textBoxCreateDimensionLength))
                || (textBoxCreateDimensionWallThickness.Visible && HasText(textBoxCreateDimensionWallThickness));
        }

        private static bool HasText(TextBox box)
        {
            return box.Text.Trim().Length > 0;
        }

        private void ShowCreateError(string message)
        {
            labelCreateError.ForeColor = CreateErrorColor;
            labelCreateError.Text = message;
            _duplicateNoticeShown = false;
        }

        private void ShowCreateInfo(string message)
        {
            labelCreateError.ForeColor = CreateInfoColor;
            labelCreateError.Text = message;
            _duplicateNoticeShown = true;
        }

        private void ClearCreateMessage()
        {
            labelCreateError.ForeColor = CreateErrorColor;
            labelCreateError.Text = string.Empty;
            _duplicateNoticeShown = false;
        }

        private async void buttonCreateSubmit_Click(object sender, EventArgs e)
        {
            ClearCreateMessage();

            var typeId = comboBoxCreateType.SelectedValue as int?;
            var material = textBoxCreateMaterial.Text.Trim();
            var dimensionValid = TryComposeDimension(out var dimension);

            if (!typeId.HasValue || material.Length == 0 || !dimensionValid)
            {
                ShowCreateError(!dimensionValid && AnyDimensionFieldHasText()
                    ? Strings.ErrorSourceItemCreateDimensionInvalid
                    : Strings.ErrorSourceItemCreateRequiredFields);
                return;
            }

            int? mass = null;
            var massText = textBoxCreateMass.Text.Trim();
            if (massText.Length > 0)
            {
                int parsedMass;
                if (!int.TryParse(massText, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsedMass)
                    && !int.TryParse(massText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedMass))
                {
                    ShowCreateError(Strings.ErrorSourceItemCreateInvalidMass);
                    return;
                }
                mass = parsedMass;
            }

            var request = new SourceItemCreateRequest
            {
                TypeId = typeId.Value,
                Dimension = dimension,
                Material = material,
                Mass = mass,
                ManufacturerId = comboBoxCreateManufacturer.SelectedValue as int?,
                OrderCode1 = EmptyToNull(textBoxCreateOrderCode1.Text),
                OrderCode2 = EmptyToNull(textBoxCreateOrderCode2.Text),
                Note = EmptyToNull(textBoxCreateNote.Text),
                Status = SourceItemCatalogueStatus.Active,
            };

            buttonCreateSubmit.Enabled = false;
            try
            {
                var created = await ApplicationServices.Api.SourceItems.CreateSourceItemAsync(request);

                _locallyCreatedItems.Add(created);
                _selectedId = created.Id;
                ClearCreateForm();
                ApplyFilters();

                // Re-syncs the lookup snapshot in the background - the freshly created row is
                // already visible above via _locallyCreatedItems; this just lets the real
                // snapshot catch up so it's there for every other picker too.
                StartBackgroundLookupRefresh();
            }
            catch (GladosApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                ShowCreateError(Strings.ErrorSourceItemCreateForbidden);
            }
            catch (GladosApiException ex)
            {
                // The backend rejects duplicate specs (and any other domain rule) with a message
                // meant for display - shown verbatim, same as every other GladosApiException
                // message surfaced elsewhere in this add-in.
                ShowCreateError(ex.Message);
            }
            catch (HttpRequestException)
            {
                ShowCreateError(Strings.ErrorSourceItemCreateNoConnection);
            }
            finally
            {
                // Recomputes rather than a bare "= true": buttonCreateSubmit.Enabled must stay
                // false if the panel's current fields still match _duplicateMatch (in practice
                // never the case here - Create is only reachable while it's null - but this is
                // the one source of truth for the button's enabled state either way).
                UpdateDuplicateDetection();
            }
        }

        private void ClearCreateForm()
        {
            comboBoxCreateType.SelectedIndex = -1;
            SelectPreset(DimensionShape.Block);
            textBoxCreateDimensionWidth.Text = string.Empty;
            textBoxCreateDimensionHeight.Text = string.Empty;
            textBoxCreateDimensionLength.Text = string.Empty;
            textBoxCreateDimensionWallThickness.Text = string.Empty;
            textBoxCreateMaterial.Text = string.Empty;
            textBoxCreateMass.Text = string.Empty;
            comboBoxCreateManufacturer.SelectedIndex = -1;
            textBoxCreateOrderCode1.Text = string.Empty;
            textBoxCreateOrderCode2.Text = string.Empty;
            textBoxCreateNote.Text = string.Empty;
            ClearCreateMessage();

            if (!string.IsNullOrWhiteSpace(_madeItemMaterial))
                textBoxCreateMaterial.Text = _madeItemMaterial.Trim();
        }

        private static string EmptyToNull(string value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        #endregion
    }
}

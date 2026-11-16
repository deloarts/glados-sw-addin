using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Filters;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Api.Services;
using GladosSwAddin.Forms;
using GladosSwAddin.Helpers;
using GladosSwAddin.Models;
using GladosSwAddin.Resources;
using GladosSwAddin.Services;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace GladosSwAddin
{
    /// <summary>
    /// Task pane for reviewing and editing a document's Glados properties. Connection to the
    /// backend is two independent facts, never tracked anywhere else: ServerKnown (a field
    /// mapping synced from the currently configured server is in effect — see
    /// FieldMappingService.IsDefaults — this machine's persisted "has connected before" signal)
    /// and Online (this session's PAT test succeeded, with a reason when it hasn't —
    /// OfflineReason). ApplyConnectionState is the only place either fact is turned into an
    /// enabled/disabled control or a status icon/tooltip; see README's "Absence and
    /// unreachability are different" for the three resulting states.
    /// </summary>
    // SwAddin.CreateTaskpane instantiates this control across COM, by ProgId, via
    // ITaskpaneView.AddControl("GladosSwAddin.GladosTaskPaneControl", ...) - the assembly-level
    // [assembly: ComVisible(false)] in AssemblyInfo.cs means that requires an explicit opt-in
    // here (SwAddin.cs's own [ComVisible(true)]/[Guid] pair is the same opt-in for the add-in's
    // COM entry point). The Guid is fixed so the installer's authored registry rows (see
    // GladosSwAddin.Installer/Package.wxs) name a stable CLSID; never change it without also updating those
    // rows. ClassInterfaceType.None matches SwAddin.cs's convention - this control exposes no
    // custom COM interface, it is only ever hosted by CLSID/ProgId, never called through
    // IDispatch.
    [ComVisible(true)]
    [Guid("E633F40F-85FC-493F-9DFA-025A154E6A98")]
    [ProgId("GladosSwAddin.GladosTaskPaneControl")]
    [ClassInterface(ClassInterfaceType.None)]
    public partial class GladosTaskPaneControl : UserControl
    {
        private readonly SettingsService _settingsService;
        private readonly CredentialService _credentialService;

        /// <summary>What the Manufacturer combo is actually bound to: the current lookup
        /// snapshot's non-archived suppliers (ApplicationServices.Lookups.Current.Suppliers),
        /// plus a placeholder entry when the active document references a supplier id the
        /// snapshot can't resolve at all. Kept separate from the snapshot so a placeholder can
        /// never leak into what gets persisted to the lookup cache file.</summary>
        private readonly List<SupplierResponse> _manufacturerChoices =
            new List<SupplierResponse>();

        private ModelDoc2 _activeModel;

        /// <summary>True while the active document has never been saved (it has no file path
        /// yet) — set only by SetActiveDocumentUnsaved. ApplyConnectionState folds it into the
        /// Source and Configuration dropdowns' enabled state, so a connection change can never
        /// unlock them for such a document. See ApplyUnsavedDocumentLock.</summary>
        private bool _activeDocumentUnsaved;

        /// <summary>True while the active document's Glados.Kind holds a recognised value — set
        /// only by ApplyKindLock, cleared by ResetToNoDocument. Kept as a field rather than left
        /// implicit in comboBoxSource.Enabled because ApplyConnectionState re-runs after every
        /// Load from Glados / Validate click and would otherwise unlock the dropdown again. See
        /// SourceDropdownAllowed.</summary>
        private bool _activeDocumentKindLocked;

        /// <summary>The one rule for enabling comboBoxSource (and with it the Default tab's two
        /// shortcut buttons, see SetSourceSelectionEnabled), shared by every place that does
        /// (ApplyConnectionState, ApplyKindLock, ResetToNoDocument) so none can undo another's
        /// lock: a server synced before, a document saved at least once, and no Kind stamped on
        /// it yet.</summary>
        private bool SourceDropdownAllowed =>
            ServerKnown && !_activeDocumentUnsaved && !_activeDocumentKindLocked;

        private BoughtItemProperties _boughtItemLocal;

        /// <summary>The made-item baseline last read from (or written to) the document - Reload
        /// From File repopulates from this, dirty markers compare the UI against it. Null until
        /// LoadMadeItemAsync has run once. Mirrors _boughtItemLocal.</summary>
        private MadeItemProperties _madeItemLocal;

        /// <summary>The assembly tab's baseline - the last-read-from/written-to-document
        /// snapshot for a made assembly, playing the same role _madeItemLocal plays for the
        /// Made tab (Reload From File repopulates from this, dirty markers compare the UI
        /// against it). Holds a full MadeItemProperties, not just the two fields this tab
        /// edits, because ReadAssemblyFromUi has to carry the rest of the snapshot through
        /// unchanged - see that method's doc comment. Null until LoadAssemblyAsync has run
        /// once. See the Assembly item region for why this tab exists at all.</summary>
        private MadeItemProperties _assemblyLocal;

        /// <summary>The Assembly tab's working routing — what processRoutingControlAssembly edits in
        /// place, cloned from _assemblyLocal.Processes so edits never touch the baseline. Mirrors
        /// _madeItemSteps.</summary>
        private List<ProcessStep> _assemblySteps = new List<ProcessStep>();

        /// <summary>processRoutingControl1's shared working copy of the routing - see the class
        /// doc comment on ProcessRoutingControl for why handing it this exact List instance
        /// (not a copy) is what makes every row edit visible here without extra plumbing.
        /// Cloned from _madeItemLocal.Processes on every load/discard (CloneSteps) so editing
        /// this never mutates the baseline.</summary>
        private List<ProcessStep> _madeItemSteps = new List<ProcessStep>();

        /// <summary>The made tab's current source-item pick — working state, mirroring
        /// _madeItemSteps: reset from _madeItemLocal.SourceItemId on every PopulateMadeItemFields,
        /// changed only by the Select/Clear buttons (SelectSourceItemAsync,
        /// buttonMadeItemSourceItemClear_Click), read by ReadMadeItemFromUi at Save. Null means
        /// "nothing picked", not "unknown" — mirrors comboBoxBoughtItemManufacturer.SelectedValue.
        /// </summary>
        private int? _madeItemSourceItemId;

        /// <summary>The stored label for _madeItemSourceItemId — set from the picker's selection
        /// (SourceItemLabelComposer.Compose) or from the loaded baseline, never re-resolved just
        /// to repaint the display (see UpdateSourceItemDisplay/PlaceholderLabel). Only
        /// ResolveSourceItemLabel, called at Save, re-derives it from the current lookup
        /// snapshot.</summary>
        private string _madeItemSourceItemName;

        private bool _suppressConfigurationChange;
        private bool _suppressSourceChange;
        private string _currentSource;

        /// <summary>Why the add-in isn't Online (see below) this session — meaningless while
        /// _online is true. NoCredentials doubles as the constructor's initial value, matching
        /// what RefreshAuthStateAsync reports before any PAT/URL is configured.</summary>
        private enum OfflineReason
        {
            NoCredentials,
            Unreachable,
            TokenRejected,
            ServerError,
        }

        /// <summary>True once the PAT test has succeeded this session — set only by SetOnline/
        /// SetOffline, applied only by ApplyConnectionState. Never read directly outside those
        /// three; every other call site goes through the _isConnected property below so it can
        /// never see a stale copy.</summary>
        private bool _online;

        /// <summary>Paired with _online — see OfflineReason.</summary>
        private OfflineReason _offlineReason = OfflineReason.NoCredentials;

        /// <summary>The HTTP status code of the error answer while _offlineReason is
        /// ServerError, shown in the status tooltip; meaningless for every other reason.</summary>
        private int _offlineStatusCode;

        /// <summary>True once a field mapping synced from the currently configured server URL
        /// is in effect — reused as-is from FieldMappingService rather than tracked separately,
        /// since a synced mapping only ever exists after a successful PAT test (FieldMappingService.
        /// IsDefaults already invalidates it back to false on a server-URL change, via the same
        /// normalisation it uses to compare URLs). This machine's persisted "has connected to
        /// Glados before" signal — see ApplyConnectionState and README's "Absence and
        /// unreachability are different".</summary>
        private static bool ServerKnown => !ApplicationServices.FieldMapping.IsDefaults;

        /// <summary>What every API-calling code path (the possible-duplicate check, both Glados
        /// lookups, the source-item picker's Create panel) actually wants to know: is the PAT
        /// test good right now. Kept under its historical name but derived from _online rather
        /// than a separately-maintained field, so it can never drift from what ApplyConnectionState
        /// just applied.</summary>
        private bool _isConnected => _online;

        /// <summary>Bumped whenever a document/configuration/source load restarts (see
        /// OnActiveDocChangedAsync, ApplySourceGateAsync). Async work that captures this value
        /// before awaiting checks it again afterward, so a superseded load (e.g. the user
        /// switches documents mid-lookup) discards its stale result instead of painting it
        /// over whatever's actually active now.</summary>
        private int _loadGeneration;

        private static readonly Color ConnectionStatusColorNormal = Color.White;
        private static readonly Color ConnectionStatusColorError = Color.Firebrick;
        private static readonly Color ConnectionStatusColorWarning = Color.Orange;

        // PDM validation indicator colors — shared between the Bought and Made tabs (unlike
        // StateColor*/SyncColor* below, which are per-tab because BoughtItemMatchState and
        // MadeItemMatchState are separate domain enums; PDM validation has one shared shape).
        private static readonly Color PdmValidationColorNeutral = SystemColors.GrayText;
        private static readonly Color PdmValidationColorReady = Color.SeaGreen;
        private static readonly Color PdmValidationColorBlocked = Color.DarkOrange;
        private static readonly Color PdmValidationColorError = Color.Firebrick;
        private static readonly Color PdmValidationColorDisabled = Color.Gray;
        private readonly System.Windows.Forms.Timer _statusBlinkTimer =
            new System.Windows.Forms.Timer { Interval = 600 };
        private readonly ToolTip _boughtItemSyncTooltip = new ToolTip();
        private readonly ToolTip _boughtItemButtonTooltip = new ToolTip();
        private readonly ToolTip _dataPortalLinkTooltip = new ToolTip();

        /// <summary>The token permission the Data Portal lookups need. The backend gates match,
        /// search and picture on it and includes it in the SolidWorks add-in token preset.</summary>
        private const string DataPortalPermission = "use:data_portal";

        /// <summary>Whether Glados has the EPLAN Data Portal switched on, read from
        /// GET .../bought-items-edp/status after every successful PAT test
        /// (RefreshDataPortalStatusAsync). False until then and whenever that call fails, so the
        /// lookup is never offered against a server that can't answer it.</summary>
        private bool _dataPortalEnabled;

        /// <summary>The warning PatContract.WarningFor built from GET pat/version after this
        /// session's PAT test, or null when the backend serves the supported contract, the call
        /// failed, or the add-in is offline. Written by RefreshBackendVersionAsync and
        /// SetOffline, shown only by ApplyConnectionState.</summary>
        private string _backendVersionWarning;

        /// <summary>Whether the token may use the lookups. The PAT test reports the token's own
        /// permissions (Glados caps a token at the scope it was created with, whatever its
        /// owner holds); a missing list reads as "allowed" so the backend's own 403 decides.</summary>
        private bool _dataPortalPermitted = true;

        /// <summary>Whether the Data Portal lookup can run right now: online, the portal switched
        /// on and the permission held. Written only by ApplyConnectionState, which decides it
        /// with every other connection-dependent control.</summary>
        private bool _dataPortalUsable;

        /// <summary>The Data Portal part the bought tab links, unsaved edits included.
        /// Glados.EdpPartId has no control of its own: it is set when a part is taken from the
        /// Data Portal dialog and shown by linkLabelBoughtItemDataPortal.</summary>
        private int? _boughtItemEdpPartId;

        /// <summary>The order code the last background match ran for, and the parts it found;
        /// cleared whenever the order code or the document changes. Advisory only: a failed
        /// match leaves them empty without telling anyone.</summary>
        private string _dataPortalMatchedNumber;

        private IReadOnlyList<EdpSearchHitResponse> _dataPortalHits = new List<EdpSearchHitResponse>();
        private readonly ToolTip _madeItemButtonTooltip = new ToolTip();
        private readonly ToolTip _assemblyButtonTooltip = new ToolTip();

        /// <summary>Explains labelRemoteStatus's icon — see ApplyConnectionState, the only
        /// writer.</summary>
        private readonly ToolTip _remoteStatusTooltip = new ToolTip();

        /// <summary>Carries the full PDM validation status (or, when blocked, the backend's
        /// reasons verbatim) on hover — the inline label uses AutoEllipsis and can truncate.
        /// One per tab, like _boughtItemButtonTooltip/_madeItemButtonTooltip, rather than shared,
        /// since a shared ToolTip only tracks one active association at a time.</summary>
        private readonly ToolTip _boughtItemPdmValidationTooltip = new ToolTip();
        private readonly ToolTip _madeItemPdmValidationTooltip = new ToolTip();

        /// <summary>Explains why comboBoxSource is disabled once Glados.Kind is set — see
        /// ApplyKindLock. Kept separate from the other tooltips above since it's the only one
        /// whose text (and whether it's shown at all) depends on the active document rather
        /// than being set once from ApplyLocalizedStaticText.</summary>
        private readonly ToolTip _sourceLockTooltip = new ToolTip();

        /// <summary>Shows the last time the lookup snapshot was synced from the server (or that
        /// it's never been synced) — see UpdateReloadTooltip.</summary>
        private readonly ToolTip _reloadTooltip = new ToolTip();

        /// <summary>The required-field marker's color (docs/FIELD_OWNERSHIP.md "required
        /// fields") — deliberately the same red as PdmValidationColorError, a different
        /// signal shown on a different control (a field label's ForeColor, not the PDM
        /// validation line), so both read as "something's missing" without introducing a
        /// third color for the same idea. Independent of, and combinable with, the dirty " *"
        /// suffix SetDirtyMarker applies to the same label's Text: one channel is color, the
        /// other is text.</summary>
        private static readonly Color RequiredFieldMissingColor = Color.Firebrick;

        /// <summary>One ToolTip shared by every required-field marker across both tabs and the
        /// shared Part Number/Revision labels — a single ToolTip instance already tracks a
        /// per-control association (SetToolTip), so there is no need for one per label the way
        /// _boughtItemPdmValidationTooltip/_madeItemPdmValidationTooltip need one each.</summary>
        private readonly ToolTip _requiredFieldTooltip = new ToolTip();

        /// <summary>Explains the Assembly tab's permanent "not tracked" indicators (see
        /// SetAssemblyStatusIndicatorsNeutral) - its own instance for the same reason
        /// _boughtItemPdmValidationTooltip/_madeItemPdmValidationTooltip get one each rather
        /// than sharing: it is set on all four indicator labels at once, and a single ToolTip
        /// only tracks one active association.</summary>
        private readonly ToolTip _assemblyIndicatorTooltip = new ToolTip();

        /// <summary>Lists the referenced files behind labelDownloadsReference — see
        /// ShowDownloadsReferences, the only writer.</summary>
        private readonly ToolTip _downloadsReferenceTooltip = new ToolTip();

        /// <summary>The active document's references into a Downloads folder as last found by
        /// ShowDownloadsReferences, kept so ApplyLocalizedStaticText can repaint the warning in a
        /// new language without re-reading the document. Empty when there is no document.</summary>
        private IList<string> _downloadsReferences = new List<string>();

        /// <summary>Whether the Bought tab currently shows its Embed Imported Components row —
        /// see SetEmbedImportedComponentsVisible, the only writer. Tracked here rather than read
        /// from the button, whose Visible reads false whenever the tab itself isn't showing.</summary>
        private bool _embedImportedComponentsShown;

        private readonly Dictionary<Label, string> _baseLabelText = new Dictionary<Label, string>();

        public GladosTaskPaneControl()
        {
            // SolidWorks hosts this control via COM (ITaskpaneView.AddControl) rather than a
            // .NET Application.Run() loop, so nothing else guarantees a WindowsFormsSynchronizationContext
            // is installed on SolidWorks' UI thread. Without one, `await` continuations in
            // async methods below (RefreshAuthStateAsync, etc.) resume on an arbitrary
            // thread-pool thread instead of marshaling back here, causing cross-thread
            // InvalidOperationExceptions the moment they touch a control.
            EnsureUiSynchronizationContext();

            InitializeComponent();

            _settingsService = ApplicationServices.Settings;
            _credentialService = ApplicationServices.Credentials;

            labelHeader.Font = FontManager.GetFont("Audiowide", 16);

            labelRemoteStatus.Font = FontManager.GetFont("Material Icons", 12);

            labelBoughtItemStateMdi.Font = FontManager.GetFont("Material Icons", 12);
            labelBoughtItemSyncMdi.Font = FontManager.GetFont("Material Icons", 12);
            labelMadeItemStateMdi.Font = FontManager.GetFont("Material Icons", 12);
            labelMadeItemSyncMdi.Font = FontManager.GetFont("Material Icons", 12);
            labelBoughtItemPdmValidationMdi.Font = FontManager.GetFont("Material Icons", 12);
            labelMadeItemPdmValidationMdi.Font = FontManager.GetFont("Material Icons", 12);
            labelAssemblyStateMdi.Font = FontManager.GetFont("Material Icons", 12);
            labelAssemblySyncMdi.Font = FontManager.GetFont("Material Icons", 12);
            labelDownloadsReferenceMdi.Font = FontManager.GetFont("Material Icons", 12);

            pictureBoxUserAvatar.SizeMode = PictureBoxSizeMode.Zoom;

            _statusBlinkTimer.Tick += (s, e) =>
                labelRemoteStatus.Visible = !labelRemoteStatus.Visible;
            ApplicationServices.Lookups.Changed += OnLookupsChanged;
            Disposed += (s, e) =>
            {
                _statusBlinkTimer.Dispose();
                _boughtItemSyncTooltip.Dispose();
                _boughtItemButtonTooltip.Dispose();
                _dataPortalLinkTooltip.Dispose();
                _madeItemButtonTooltip.Dispose();
                _assemblyButtonTooltip.Dispose();
                _remoteStatusTooltip.Dispose();
                _sourceLockTooltip.Dispose();
                _reloadTooltip.Dispose();
                _boughtItemPdmValidationTooltip.Dispose();
                _madeItemPdmValidationTooltip.Dispose();
                _requiredFieldTooltip.Dispose();
                _assemblyIndicatorTooltip.Dispose();
                _downloadsReferenceTooltip.Dispose();
                ApplicationServices.Lookups.Changed -= OnLookupsChanged;
            };

            // Applied with the default culture/theme for now — RefreshAuthStateAsync re-applies
            // both once the signed-in user's actual UserResponse comes back.
            ApplyLanguage(null);
            ApplyTheme(ThemePrimary.Glados);

            // Reflects whatever ServerKnown/_online already say before RefreshAuthStateAsync
            // (GladosTaskPaneControl_Load) has even run — ServerKnown is persisted, so a machine
            // that has connected before starts with tabs/combo already enabled against the
            // cached snapshot rather than waiting on this session's PAT test. See the class doc
            // comment on connection state.
            ApplyConnectionState();
            SetBoughtItemTabEnabled(false);
            // Same reasoning as SetBoughtItemTabEnabled above — establishes the initial
            // disabled state rather than leaving the Designer's default Enabled=true showing
            // before the first document load.
            SetAssemblyTabEnabled(false);
            UpdateVisitWeblinkButtonEnabled();
            UpdateReloadTooltip();
            WireDirtyTracking();
            WirePossibleDuplicateLiveCheck();
            WireDataPortalLiveCheck();
            WireMadeItemDirtyTracking();
            WireAssemblyDirtyTracking();
            WireCustomerFields();
            // No document yet — establishes the initial disabled state rather than leaving the
            // Designer's default Enabled=true showing before the first document load.
            UpdateMadeItemCalcButtons();
        }

        /// <summary>Repopulates the pickers whenever ApplicationServices.Lookups.Current is
        /// swapped — a background RefreshAsync landing new data, or LoadCached during
        /// RefreshAuthStateAsync/SetOffline. PopulateLookupControls marshals to the
        /// UI thread itself (via OnUiThread), so this can run from whatever thread the swap
        /// happened on.</summary>
        private void OnLookupsChanged(object sender, EventArgs e)
        {
            PopulateLookupControls();
            UpdateReloadTooltip();
        }

        /// <summary>Maps the Glados backend's Language (see UserResponse.Language — currently
        /// always deAT or enGB) to a CultureInfo and applies it as the UI culture, then
        /// refreshes every localized control. Called once with the default (null → en-GB) at
        /// construction, before we know who's signed in, and again from RefreshAuthStateAsync
        /// once the actual UserResponse.Language is known.</summary>
        /// <summary>Installs a WindowsFormsSynchronizationContext on the calling (UI) thread if
        /// none is current. Needed in the constructor (SolidWorks hosts this control via COM,
        /// with no Application.Run() loop to install one) and again after every modal
        /// ShowDialog: when that nested message loop ends, WinForms can uninstall the context
        /// it thinks it owns, and the next await in the calling method then resumes on a
        /// thread-pool thread instead of here — the cross-thread exception ApplyLanguage used
        /// to throw right after saving the settings dialog.</summary>
        private static void EnsureUiSynchronizationContext()
        {
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(
                    new WindowsFormsSynchronizationContext()
                );
        }

        private void ApplyLanguage(Language? language)
        {
            var culture =
                language == Language.DeAT ? new CultureInfo("de-AT") : new CultureInfo("en-GB");

            // Both the culture and the relabeling belong to the UI thread: setting
            // CurrentUICulture on whatever thread an await happened to resume on would
            // localize nothing visible, and ApplyLocalizedStaticText touches every control.
            OnUiThread(() =>
            {
                Thread.CurrentThread.CurrentUICulture = culture;
                ApplyLocalizedStaticText();
            });
        }

        /// <summary>Refreshes every statically-labeled control (tabs, buttons, field labels,
        /// button tooltips) from Strings.resx for whatever culture ApplyLanguage just set, and
        /// re-captures _baseLabelText for the dirty-tracked field labels so the " *" marker
        /// logic compares against the newly-localized text rather than a stale one from a
        /// previous culture.</summary>
        private void ApplyLocalizedStaticText()
        {
            OnUiThread(() =>
            {
                linkLabelSettings.Text = Strings.SettingsLinkText;
                linkLabelReload.Text = Strings.LinkReloadText;
                tabPageBought.Text = Strings.TabBoughtItemText;
                tabPageMade.Text = Strings.TabMadeItemText;
                tabPageAssembly.Text = Strings.TabAssemblyText;
                tabPageDrawing.Text = Strings.TabDrawingText;
                labelDocumentNotSaved.Text = Strings.LabelDocumentNotSavedText;
                ShowDownloadsReferences(_downloadsReferences);
                buttonInitMade.Text = Strings.ButtonInitMadeText;
                buttonInitBought.Text = Strings.ButtonInitBoughtText;
                labelBoughtItemSync.Text = Strings.LabelSyncText;
                labelBoughtItemState.Text = Strings.LabelStateText;
                buttonBoughtItemLoadFromGlados.Text = Strings.ButtonLoadFromGladosText;
                buttonBoughtItemSaveProperties.Text = Strings.ButtonSaveToFileText;
                buttonBoughtItemReloadProperties.Text = Strings.ButtonReloadFromFileText;
                labelBoughtItemArticleName.Text = Strings.LabelArticleNameText;
                labelBoughtItemOrderCode1.Text = Strings.LabelOrderCode1Text;
                labelBoughtItemOrderCode2.Text = Strings.LabelOrderCode2Text;
                labelBoughtItemGTIN.Text = Strings.LabelGtinText;
                labelBoughtItemEAN.Text = Strings.LabelEanText;
                labelBoughtItemTariffNumber.Text = Strings.LabelTariffNumberText;
                labelBoughtItemNote.Text = Strings.LabelNoteText;
                labelBoughtItemWeblink.Text = Strings.LabelWeblinkText;
                buttonBoughtItemVisitWeblink.Text = Strings.ButtonVisitWeblinkText;
                buttonBoughtItemDataPortal.Text = Strings.ButtonDataPortalText;
                labelBoughtItemManufacturer.Text = Strings.LabelManufacturerText;
                labelBoughtItemGroups.Text = Strings.LabelArticleGroupsText;
                labelPartNumber.Text = Strings.LabelPartNumberText;
                labelRevision.Text = Strings.LabelRevisionText;
                labelConfiguration.Text = Strings.LabelConfigurationText;
                labelSource.Text = Strings.LabelSourceText;

                _boughtItemButtonTooltip.SetToolTip(
                    buttonBoughtItemLoadFromGlados,
                    Strings.TooltipLoadFromGlados
                );
                _boughtItemButtonTooltip.SetToolTip(
                    buttonBoughtItemSaveProperties,
                    Strings.TooltipSaveToFile
                );
                _boughtItemButtonTooltip.SetToolTip(
                    buttonBoughtItemReloadProperties,
                    Strings.TooltipReloadFromFile
                );
                _boughtItemButtonTooltip.SetToolTip(
                    buttonBoughtItemVisitWeblink,
                    Strings.TooltipVisitWeblink
                );
                buttonBoughtItemEmbedImportedComponents.Text = Strings.ButtonEmbedImportedComponentsText;
                _boughtItemButtonTooltip.SetToolTip(
                    buttonBoughtItemEmbedImportedComponents,
                    Strings.TooltipEmbedImportedComponents
                );
                buttonBoughtItemValidate.Text = Strings.ButtonPdmValidateText;
                _boughtItemButtonTooltip.SetToolTip(buttonBoughtItemValidate, Strings.TooltipPdmValidate);
                // Resets to the initial state rather than just the caption (contrast
                // labelBoughtItemSync.Text above, which relies on RefreshBoughtItemLookupAsync
                // repainting it moments later in the same RefreshAuthStateAsync call chain) —
                // nothing here re-runs validation automatically, so leaving a stale icon/color
                // behind a freshly reset caption would look wrong until the next Save or click.
                SetPdmValidationNotChecked(
                    labelBoughtItemPdmValidationMdi, labelBoughtItemPdmValidation, _boughtItemPdmValidationTooltip);

                foreach (
                    var label in new[]
                    {
                        labelBoughtItemArticleName,
                        labelBoughtItemOrderCode1,
                        labelBoughtItemOrderCode2,
                        labelBoughtItemGTIN,
                        labelBoughtItemEAN,
                        labelBoughtItemTariffNumber,
                        labelBoughtItemNote,
                        labelBoughtItemWeblink,
                        labelBoughtItemManufacturer,
                        labelBoughtItemGroups,
                    }
                )
                    _baseLabelText[label] = label.Text;

                labelMadeItemSync.Text = Strings.LabelSyncText;
                labelMadeItemState.Text = Strings.LabelStateText;
                buttonMadeItemLoadFromGlados.Text = Strings.ButtonLoadFromGladosText;
                buttonMadeItemSaveProperties.Text = Strings.ButtonSaveToFileText;
                buttonMadeItemReloadProperties.Text = Strings.ButtonReloadFromFileText;
                labelMadeItemArticleName.Text = Strings.LabelArticleNameText;
                labelMadeItemGroups.Text = Strings.LabelArticleGroupsText;
                labelMadeItemNote.Text = Strings.LabelNoteText;
                labelMadeItemSourceItem.Text = Strings.LabelSourceItemText;
                buttonMadeItemSourceItemSelect.Text = Strings.ButtonSourceItemSelectText;
                buttonMadeItemSourceItemClear.Text = Strings.ButtonSourceItemClearText;
                labelMadeItemMaterial.Text = Strings.LabelMaterialText;
                labelMadeItemMass.Text = Strings.LabelMassText;
                buttonMadeItemMaterialCalc.Text = Strings.ButtonCalcText;
                buttonMadeItemMassCalc.Text = Strings.ButtonCalcText;
                labelMadeItemProcessesUnreadableHint.Text = Strings.MadeItemProcessesUnreadableHint;
                labelMadeItemStepsTitle.Text = Strings.MadeItemStepsTitleText;
                labelAssemblyProcessesUnreadableHint.Text = Strings.MadeItemProcessesUnreadableHint;
                labelAssemblyStepsTitle.Text = Strings.MadeItemStepsTitleText;

                _madeItemButtonTooltip.SetToolTip(
                    buttonMadeItemLoadFromGlados,
                    Strings.TooltipLoadFromGlados
                );
                _madeItemButtonTooltip.SetToolTip(
                    buttonMadeItemSaveProperties,
                    Strings.TooltipSaveToFile
                );
                _madeItemButtonTooltip.SetToolTip(
                    buttonMadeItemReloadProperties,
                    Strings.TooltipReloadFromFile
                );
                buttonMadeItemValidate.Text = Strings.ButtonPdmValidateText;
                _madeItemButtonTooltip.SetToolTip(buttonMadeItemValidate, Strings.TooltipPdmValidate);
                SetPdmValidationNotChecked(
                    labelMadeItemPdmValidationMdi, labelMadeItemPdmValidation, _madeItemPdmValidationTooltip);

                foreach (
                    var label in new[]
                    {
                        labelMadeItemArticleName,
                        labelMadeItemGroups,
                        labelMadeItemNote,
                        labelMadeItemSourceItem,
                        labelMadeItemMaterial,
                        labelMadeItemMass,
                    }
                )
                    _baseLabelText[label] = label.Text;

                labelAssemblySync.Text = Strings.LabelSyncText;
                labelAssemblyState.Text = Strings.LabelStateText;
                buttonAssemblySaveProperties.Text = Strings.ButtonSaveToFileText;
                buttonAssemblyReloadProperties.Text = Strings.ButtonReloadFromFileText;
                labelAssemblyName.Text = Strings.LabelAssemblyNameText;
                labelAssemblyNote.Text = Strings.LabelNoteText;

                _assemblyButtonTooltip.SetToolTip(
                    buttonAssemblySaveProperties,
                    Strings.TooltipSaveToFile
                );
                _assemblyButtonTooltip.SetToolTip(
                    buttonAssemblyReloadProperties,
                    Strings.TooltipReloadFromFile
                );

                foreach (
                    var label in new[]
                    {
                        labelAssemblyName,
                        labelAssemblyNote,
                        labelAssemblyStepsTitle,
                    }
                )
                    _baseLabelText[label] = label.Text;

                ApplyCustomerLocalizedText();
            });

            // Repainted here too (not just from LoadAssemblyAsync) so a language switch while
            // the Assembly tab is already showing doesn't leave the neutral indicators'
            // caption/tooltip behind in the old culture — mirrors SetPdmValidationNotChecked's
            // reset above. Guarded on the tab being enabled because, unlike that one, these
            // indicators have a blanked state of their own: SetAssemblyTabEnabled(false) clears
            // them precisely so a disabled tab stops explaining the last document, and an
            // unguarded repaint here would undo that on the next language switch.
            if (tabPageAssembly.Enabled)
                SetAssemblyStatusIndicatorsNeutral();

            UpdateReloadTooltip();
            ApplyPropertiesChangedOutsideMarker();
        }

        /// <summary>Recolors the header/footer bars to the signed-in user's ThemePrimary
        /// preference (see ThemeColors) instead of the fixed accent color they were hardcoded
        /// to before. Called once with the Glados default before sign-in (ApplyLanguage(null)
        /// above does the same for language), and again from RefreshAuthStateAsync once the
        /// actual UserResponse comes back.</summary>
        private void ApplyTheme(ThemePrimary primary)
        {
            var accent = ThemeColors.GetPrimaryAccent(primary);

            OnUiThread(() =>
            {
                panelHeader.BackColor = accent;
                panelFooter.BackColor = accent;
                processRoutingControl1.AccentColor = accent;
                processRoutingControlAssembly.AccentColor = accent;
            });
        }

        /// <summary>Defensive backstop for async continuations that touch controls after an
        /// await. SolidWorks hosts this control via COM with no Application.Run() loop, and the
        /// WindowsFormsSynchronizationContext installed in the constructor has proven unreliable
        /// at reasserting itself for every continuation in that environment — Invoke against the
        /// control's own handle works regardless of ambient SynchronizationContext state.</summary>
        private void OnUiThread(Action action)
        {
            if (IsHandleCreated && InvokeRequired)
                Invoke(action);
            else
                action();
        }

        /// <summary>Last-resort backstop for every async-void entry point (SolidWorks COM
        /// callbacks, WinForms event handlers). SolidWorks hosts this control via COM rather
        /// than a .NET Application.Run() loop, so nothing else catches an exception that
        /// escapes an async void method — left unguarded, it can propagate into SolidWorks
        /// itself instead of just this addin. Individual handlers may still catch specific,
        /// expected exceptions themselves for a more precise message; this is only the net
        /// underneath.</summary>
        private async void RunGuarded(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Error("Unhandled exception in a task pane handler", ex);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorUnexpectedFormat, ex.Message));
            }
        }

        /// <summary>Centralizes labelRemoteStatus updates. blinking keeps the icon toggling
        /// visibility on _statusBlinkTimer so a real connectivity problem (unreachable server,
        /// invalid PAT) stays attention-grabbing instead of blending into the footer like the
        /// quiet "not connected yet" state does.</summary>
        private void SetRemoteStatusIndicator(string icon, Color color, bool blinking)
        {
            OnUiThread(() =>
            {
                _statusBlinkTimer.Stop();

                labelRemoteStatus.Visible = true;
                labelRemoteStatus.Text = icon;
                labelRemoteStatus.ForeColor = color;

                if (blinking)
                    _statusBlinkTimer.Start();
            });
        }

        private void GladosTaskPaneControl_Load(object sender, EventArgs e)
        {
            RunGuarded(RefreshAuthStateAsync);
        }

        private async Task RefreshAuthStateAsync()
        {
            // Populates the pickers from whatever's on disk for the currently configured
            // server, regardless of what happens below — so a loaded document never sees empty
            // pickers just because the PAT check hasn't finished (or won't succeed) yet. A no-op
            // if there's no cache yet for this server.
            ApplicationServices.Lookups.LoadCached();

            if (
                !_credentialService.HasPat()
                || string.IsNullOrWhiteSpace(_settingsService.Load().ServerUrl)
            )
            {
                SetOffline(OfflineReason.NoCredentials);
                return;
            }

            try
            {
                var user = await ApplicationServices.Api.Auth.TestPersonalAccessTokenAsync();
                ApplyLanguage(user.Language);
                ApplyTheme(user.ThemePrimary);

                Image avatar = null;
                if (user.HasAvatar)
                {
                    avatar = await TryLoadAvatarAsync();
                }

                SetOnline(avatar);

                // Never throws and only ever warns: a backend on another pat contract minor still
                // gets a working pane, just with a footer note that the add-in needs an update.
                await RefreshBackendVersionAsync();

                // Fire-and-forget from the user's perspective: SyncAsync never throws (a 404 —
                // an add-in build talking to a backend that predates this endpoint — or any
                // other error just keeps whatever mapping was already in effect), so a
                // successful connection never depends on this succeeding. See
                // docs/FIELD_OWNERSHIP.md section 2.
                await ApplicationServices.FieldMapping.SyncAsync(ApplicationServices.Api.FieldMapping);

                _dataPortalPermitted = user.Permissions == null
                    || user.Permissions.Contains(DataPortalPermission, StringComparer.Ordinal);
                if (!_dataPortalPermitted)
                    Log.Info($"Data Portal lookup not permitted: the token of {user.Username} lacks "
                        + $"{DataPortalPermission} (has: {string.Join(", ", user.Permissions)})");
                await RefreshDataPortalStatusAsync();

                // The sync above is what makes ServerKnown true the very first time this machine
                // ever connects — re-apply now that it may just have flipped, rather than leaving
                // the tabs/combo in whatever state they were in before it landed.
                ApplyConnectionState();

                // A newly synced mapping can change which fields/rules are required — recompute
                // both tabs' required-field markers against whatever document is already loaded
                // rather than waiting for the next unrelated dirty-tracking event. Same reasoning
                // for the Calc. buttons: a newly synced mapping can also change whether Mass/
                // Material are mapped at all.
                OnUiThread(() =>
                {
                    UpdateDirtyMarkers();
                    UpdateMadeItemDirtyMarkers();
                    UpdateMadeItemCalcButtons();
                });

                // Never awaited: LookupService.RefreshAsync never throws (same reasoning as
                // FieldMapping.SyncAsync above) and runs in parallel with the document lookups
                // below rather than blocking them — the pickers already have the cached snapshot
                // to show in the meantime, and OnLookupsChanged repopulates them once this lands.
                RunGuarded(() => ApplicationServices.Lookups.RefreshAsync(ApplicationServices.Api.Lookups));

                await RefreshBoughtItemLookupAsync();

                // The made lookup was cleared while offline (RefreshMadeItemLookupAsync's
                // !_isConnected guard) and nothing else re-triggers it, so refresh it here too.
                await RefreshMadeItemLookupAsync();
            }
            catch (GladosApiException ex) when (ex.IsTokenRejection)
            {
                SetOffline(OfflineReason.TokenRejected);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorSignInFailedFormat, ex.Message));
            }
            catch (GladosApiException ex)
            {
                SetOffline(OfflineReason.ServerError, (int)ex.StatusCode);
                ShowError(string.Format(
                    CultureInfo.CurrentCulture, Strings.ErrorServerAnsweredFormat, (int)ex.StatusCode, ex.Message));
            }
            catch (HttpRequestException ex)
            {
                SetOffline(OfflineReason.Unreachable);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorCouldNotReachServerFormat, ex.Message));
            }
            catch (ArgumentException ex)
            {
                SetOffline(OfflineReason.Unreachable);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorServerUrlInvalidFormat, ex.Message));
            }
            catch (FormatException ex)
            {
                SetOffline(OfflineReason.Unreachable);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorServerUrlInvalidFormat, ex.Message));
            }
        }

        private static async Task<Image> TryLoadAvatarAsync()
        {
            try
            {
                var bytes = await ApplicationServices.Api.Users.GetMyAvatarAsync();
                using (var stream = new MemoryStream(bytes))
                using (var loaded = Image.FromStream(stream))
                {
                    // Image.FromStream keeps the stream alive for the image's lifetime —
                    // copy into a Bitmap so the avatar survives past this using block.
                    return new Bitmap(loaded);
                }
            }
            catch (GladosApiException)
            {
                return null;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        /// <summary>Asks Glados which pat contract it serves and keeps the warning
        /// PatContract.WarningFor builds from the answer, for ApplyConnectionState to show. Never
        /// throws: a failed call (unreachable, timed out, a 404 from a backend without the
        /// route, an unreadable body) is logged and leaves no warning, since nothing is known
        /// about the contract then.</summary>
        private async Task RefreshBackendVersionAsync()
        {
            try
            {
                var response = await ApplicationServices.Api.Version.GetVersionAsync();
                _backendVersionWarning = PatContract.WarningFor(response, AddinInfo.Version);
                if (_backendVersionWarning != null)
                    Log.Warning($"GLADOS {response.Version} serves pat contract {response.PatContract}, "
                        + $"this add-in {AddinInfo.Version} supports {PatContract.Supported}");
                else if (PatContract.Check(response?.PatContract) == PatContractStatus.Unknown)
                    Log.Warning("GLADOS version response carries no readable pat contract "
                        + $"(\"{response?.PatContract}\"); skipping the compatibility check");
            }
            catch (GladosApiException ex)
            {
                _backendVersionWarning = null;
                Log.Warning($"Version request failed ({(int)ex.StatusCode} {ex.Code})", ex);
            }
            catch (HttpRequestException ex)
            {
                _backendVersionWarning = null;
                Log.Warning("Version request could not reach the server", ex);
            }
            catch (TaskCanceledException ex)
            {
                _backendVersionWarning = null;
                Log.Warning("Version request timed out", ex);
            }
        }

        /// <summary>Records that this session's PAT test succeeded and applies the resulting
        /// enable/disable state. The avatar is set here rather than by the caller since it's
        /// as much a part of "online" as the two facts ApplyConnectionState reads — mirrors the
        /// avatar clear in SetOffline.</summary>
        private void SetOnline(Image avatar)
        {
            _online = true;
            SetAvatarImage(avatar);
            ApplyConnectionState();
        }

        /// <summary>Records why the add-in isn't online this session and applies the resulting
        /// enable/disable state. Falls back to the offline lookup-list cache so the
        /// Manufacturer/Article Group pickers aren't just empty, and clears both tabs' Glados
        /// lookup/state/sync indicators so neither keeps showing a result computed while still
        /// online — see README's "Absence and unreachability are different".</summary>
        private void SetOffline(OfflineReason reason, int statusCode = 0)
        {
            _online = false;
            _offlineReason = reason;
            _offlineStatusCode = statusCode;
            _backendVersionWarning = null;
            SetAvatarImage(null);
            ApplicationServices.Lookups.LoadCached();
            ClearBoughtItemStatusIndicators();
            ClearMadeItemStatusIndicators();
            ApplyConnectionState();
        }

        private void SetAvatarImage(Image avatar)
        {
            OnUiThread(() =>
            {
                var oldImage = pictureBoxUserAvatar.Image;
                pictureBoxUserAvatar.Image = avatar;
                oldImage?.Dispose();
            });
        }

        /// <summary>The only place that turns ServerKnown/_online/_offlineReason into an
        /// enabled/disabled control, a status icon/tooltip, or a disabled-button tooltip — see
        /// the class doc comment. Called whenever either fact changes (SetOnline, SetOffline,
        /// the constructor for whatever ServerKnown already says before this session's first
        /// RefreshAuthStateAsync completes) and once more after a first-connection field-mapping
        /// sync, since that sync is what flips ServerKnown from false to true.
        ///
        /// ServerKnown gates editing itself (tabControlSource, comboBoxSource, and — being
        /// nested inside the tabs — everything on them, Save/Reload-from-file/field-editing
        /// included): with no server ever synced there is nothing cached to edit against.
        /// Two document locks narrow that further: ApplyKindLock locks comboBoxSource once the
        /// document's Kind is stamped, and ApplyUnsavedDocumentLock locks both dropdowns while
        /// the document has never been saved. Both are kept in fields this reads (see
        /// SourceDropdownAllowed), so re-running it — as every Load from Glados / Validate click
        /// does in its finally — never undoes either lock. Online additionally gates server actions only — Load from Glados and Validate
        /// on both tabs, plus comboBoxConfiguration (a document operation, not a Glados one) is
        /// deliberately left out of that narrower gate and follows ServerKnown alone.</summary>
        private void ApplyConnectionState()
        {
            var serverKnown = ServerKnown;
            var online = _online;
            var reason = _offlineReason;
            var offlineStatusCode = _offlineStatusCode;
            var versionWarning = online ? _backendVersionWarning : null;

            string icon;
            Color color;
            bool blinking;
            string statusTooltip;

            if (!serverKnown)
            {
                icon = MaterialIcons.CloudOff;
                color = ConnectionStatusColorError;
                blinking = true;
                statusTooltip = Strings.TooltipNotConnectedYet;
            }
            else if (online)
            {
                icon = versionWarning != null ? MaterialIcons.Warning : MaterialIcons.CloudDone;
                color = versionWarning != null ? ConnectionStatusColorWarning : ConnectionStatusColorNormal;
                blinking = false;
                statusTooltip = string.Format(CultureInfo.CurrentCulture, Strings.TooltipConnectedFormat, _settingsService.Load().ServerUrl);
                if (versionWarning != null)
                    statusTooltip += System.Environment.NewLine + versionWarning;
            }
            else
            {
                switch (reason)
                {
                    case OfflineReason.NoCredentials:
                        icon = MaterialIcons.CloudOff;
                        color = ConnectionStatusColorNormal;
                        blinking = false;
                        statusTooltip = Strings.TooltipOfflineNoCredentials;
                        break;
                    case OfflineReason.TokenRejected:
                        icon = MaterialIcons.ErrorOutline;
                        color = ConnectionStatusColorError;
                        blinking = true;
                        statusTooltip = Strings.TooltipOfflineTokenRejected;
                        break;
                    case OfflineReason.ServerError:
                        icon = MaterialIcons.ErrorOutline;
                        color = ConnectionStatusColorError;
                        blinking = true;
                        statusTooltip = string.Format(
                            CultureInfo.CurrentCulture, Strings.TooltipOfflineServerErrorFormat, offlineStatusCode);
                        break;
                    default: // Unreachable
                        icon = MaterialIcons.CloudOff;
                        color = ConnectionStatusColorError;
                        blinking = true;
                        // The sync timestamp follows the chosen UI language (CurrentUICulture), not
                        // the Windows regional format, so it reads naturally inside the localized
                        // sentence around it.
#pragma warning disable CA1305
                        statusTooltip = string.Format(
                            CultureInfo.CurrentUICulture,
                            Strings.TooltipOfflineUnreachableFormat,
                            ApplicationServices.FieldMapping.SyncedAt.Value.ToLocalTime());
#pragma warning restore CA1305
                        break;
                }
            }

            var actionsEnabled = serverKnown && online;
            var actionTooltip = actionsEnabled ? string.Empty : statusTooltip;

            // The Data Portal button only exists while Glados has the portal switched on; on top
            // of that the lookup needs a working connection and the token's permission, and each
            // missing piece gets its own explanation.
            var dataPortalPermitted = _dataPortalPermitted;
            _dataPortalUsable = actionsEnabled && _dataPortalEnabled && dataPortalPermitted;
            var dataPortalTooltip = !actionsEnabled ? statusTooltip
                : !dataPortalPermitted ? Strings.TooltipDataPortalNoPermission
                : Strings.TooltipDataPortalSearch;

            // A never-saved document keeps the document-scoped dropdowns locked whatever the
            // connection says — see ApplyUnsavedDocumentLock. Source has the Kind lock on top.
            var configurationEnabled = serverKnown && !_activeDocumentUnsaved;
            var sourceEnabled = SourceDropdownAllowed;

            OnUiThread(() =>
            {
                tabControlSource.Enabled = serverKnown;
                SetSourceSelectionEnabled(sourceEnabled);
                comboBoxConfiguration.Enabled = configurationEnabled;

                buttonBoughtItemLoadFromGlados.Enabled = actionsEnabled;
                buttonMadeItemLoadFromGlados.Enabled = actionsEnabled;
                buttonBoughtItemValidate.Enabled = actionsEnabled;
                buttonMadeItemValidate.Enabled = actionsEnabled;
                SetDataPortalButtonVisible(_dataPortalEnabled);
                buttonBoughtItemDataPortal.Enabled = _dataPortalUsable;
                _boughtItemButtonTooltip.SetToolTip(buttonBoughtItemDataPortal, dataPortalTooltip);
                UpdateDataPortalLink();

                _boughtItemButtonTooltip.SetToolTip(buttonBoughtItemLoadFromGlados, actionTooltip);
                _boughtItemButtonTooltip.SetToolTip(buttonBoughtItemValidate, actionTooltip);
                _madeItemButtonTooltip.SetToolTip(buttonMadeItemLoadFromGlados, actionTooltip);
                _madeItemButtonTooltip.SetToolTip(buttonMadeItemValidate, actionTooltip);

                _remoteStatusTooltip.SetToolTip(labelRemoteStatus, statusTooltip);

                labelBackendVersionWarning.Text = Strings.BackendVersionWarningShort;
                labelBackendVersionWarning.Visible = versionWarning != null;
                _remoteStatusTooltip.SetToolTip(labelBackendVersionWarning, versionWarning ?? string.Empty);
            });

            SetRemoteStatusIndicator(icon, color, blinking);
        }

        #region Bought item — lookup lists

        /// <summary>Archived rows are hidden from pickers except when they're one of keepIds —
        /// otherwise loading a document whose manufacturer/article group/method was archived
        /// after the document was saved would silently lose that selection (see
        /// docs/FIELD_OWNERSHIP.md). Id-to-label resolution (ResolveManufacturerLabel,
        /// ResolveArticleGroupLabels, ResolveStepMethodNames) reads
        /// ApplicationServices.Lookups.Current directly instead, since it always needs every
        /// row regardless of what's currently selected. Rows of a hand-ordered lookup pass
        /// sortOrderOf and come in their Glados order (LookupOrder); the rest by name.</summary>
        private static List<T> VisibleRows<T>(
            IEnumerable<T> all,
            Func<T, bool> isArchived,
            Func<T, int> idOf,
            Func<T, string> nameOf,
            IEnumerable<int> keepIds,
            Func<T, int> sortOrderOf = null)
        {
            var keep = new HashSet<int>(keepIds ?? Enumerable.Empty<int>());
            var visible = (all ?? Enumerable.Empty<T>())
                .Where(row => !isArchived(row) || keep.Contains(idOf(row)));
            var ordered = sortOrderOf == null
                ? visible.OrderBy(nameOf)
                : LookupOrder.HandKept(visible, sortOrderOf, nameOf, Comparer<string>.Default);
            return ordered.ToList();
        }

        /// <summary>Rebuilds the manufacturer combo and both article-group checked lists from
        /// ApplicationServices.Lookups.Current — called whenever the snapshot changes
        /// (LookupService.Changed, via OnLookupsChanged) and once from the constructor path via
        /// RefreshAuthStateAsync/SetOffline's LoadCached call. With no document loaded
        /// yet this just seeds the pickers with plain choices (nothing selected); if a document
        /// is loaded, PopulateBoughtItemFields/PopulateMadeItemFields immediately re-derive the
        /// selection from the document's saved baseline.</summary>
        private void PopulateLookupControls()
        {
            var snapshot = ApplicationServices.Lookups.Current;

            OnUiThread(() =>
            {
                _manufacturerChoices.Clear();
                _manufacturerChoices.AddRange(
                    VisibleRows(snapshot.Suppliers, s => s.Archived, s => s.Id, s => s.Name, null));
                BindManufacturerChoices();

                var articleGroups =
                    VisibleRows(
                        snapshot.ArticleGroups, g => g.Archived, g => g.Id, g => g.Name, null, g => g.SortOrder);

                checkedListBoxBoughtItemArticleGroups.DisplayMember = "Name";
                checkedListBoxBoughtItemArticleGroups.Items.Clear();
                foreach (var group in articleGroups)
                    checkedListBoxBoughtItemArticleGroups.Items.Add(group);

                checkedListBoxMadeItemArticleGroups.DisplayMember = "Name";
                checkedListBoxMadeItemArticleGroups.Items.Clear();
                foreach (var group in articleGroups)
                    checkedListBoxMadeItemArticleGroups.Items.Add(group);

                // Keeps the current pick, so a background refresh never drops it while the
                // combo has focus and PopulateMadeItemFields leaves it alone.
                SetCustomerSelection(
                    SelectedCustomerId, (comboBoxCustomer.SelectedItem as CustomerLookupResponse)?.Name);
            });

            if (_boughtItemLocal != null)
                PopulateBoughtItemFields(_boughtItemLocal);

            if (_madeItemLocal != null)
            {
                // The lookup snapshot just changed - a step whose MethodName couldn't be
                // resolved earlier (offline, empty cache) may resolve now, so re-derive it on
                // the baseline before repopulating from it.
                ResolveStepMethodNames(_madeItemLocal.Processes);
                PopulateMadeItemFields(_madeItemLocal);
            }

            if (_assemblyLocal != null)
            {
                ResolveStepMethodNames(_assemblyLocal.Processes);
                PopulateAssemblyFields(_assemblyLocal);
            }
        }

        /// <summary>Reassigns DataSource rather than mutating the bound list in place, because
        /// List&lt;T&gt; raises no change notifications — a placeholder appended to it would
        /// otherwise never show up in the combo.</summary>
        private void BindManufacturerChoices()
        {
            comboBoxBoughtItemManufacturer.DataSource = null;
            comboBoxBoughtItemManufacturer.DisplayMember = "Name";
            comboBoxBoughtItemManufacturer.ValueMember = "Id";
            comboBoxBoughtItemManufacturer.DataSource = _manufacturerChoices;
            comboBoxBoughtItemManufacturer.SelectedIndex = -1;
        }

        #endregion

        #region Bought item — SolidWorks document sync

        /// <summary>Called by SwAddin whenever the active SolidWorks document changes.</summary>
        public void OnActiveDocChanged(ModelDoc2 model)
        {
            RunGuarded(() => OnActiveDocChangedAsync(model));
        }

        private async Task OnActiveDocChangedAsync(ModelDoc2 model)
        {
            // ActiveModelDocChangeNotify fires on rebuilds/regenerates too, not just genuine
            // document switches — ignore redundant re-fires for the same document so an
            // in-progress edit never gets discarded behind the user's back.
            if (ReferenceEquals(model, _activeModel))
                return;

            _activeModel = model;

            // Supersedes any load still in flight for the document we're leaving — see
            // _loadGeneration.
            _loadGeneration++;

            if (model == null)
            {
                SetActiveDocumentUnsaved(false);
                ShowDownloadsReferences(new List<string>());
                ResetToNoDocument();
                return;
            }

            await ReloadAllForModel(model);
        }

        /// <summary>Set by SwAddin.CreateTaskpane right after AddControl, cleared by
        /// DisconnectFromSW — lets timerWatchDocumentChange_Tick read the live active document
        /// without this control owning an ISldWorks reference of its own (SwAddin is the only
        /// class that does). Null until then, and again after disconnect; the tick handler treats
        /// a null provider as "nothing to check yet".</summary>
        public Func<ModelDoc2> ActiveDocumentProvider { get; set; }

        /// <summary>Set and cleared alongside ActiveDocumentProvider. Only Embed Imported
        /// Components reads it, for the one SolidWorks system option that run has to switch
        /// temporarily (see ImportedComponentsService); null means that run skips renaming.</summary>
        public Func<ISldWorks> SolidWorksProvider { get; set; }

        /// <summary>True while a watchdog-triggered OnActiveDocChangedAsync is running — set
        /// immediately before starting it, cleared once it completes. Guards two things at once:
        /// a tick firing again before the previous tick's own call finished (plain re-entrancy),
        /// and this tick racing a load already in flight *for the same change* — the two ticks
        /// can only ever refer to the same call because OnActiveDocChangedAsync sets _activeModel
        /// synchronously before its first await, so a load kicked off by the SolidWorks event
        /// notifies (ActiveDocChangeNotify/FileCloseNotify) is already reflected in _activeModel
        /// by the time any later tick reads it — the ReferenceEquals check below skips it without
        /// needing to inspect _loadGeneration. This flag exists only for the watchdog's own
        /// overlapping ticks.</summary>
        private bool _documentWatchdogTickInProgress;

        /// <summary>Starts the 1 s watchdog that catches what SwAddin's event wiring misses —
        /// see SwAddin.ConnectToSW/OnFileCloseNotify for the event-driven side this backstops.
        /// Safe to call before ActiveDocumentProvider is set (the tick handler just no-ops until
        /// it is) and safe to call more than once (starting an already-running Timer is a
        /// no-op).</summary>
        public void StartDocumentWatchdog()
        {
            OnUiThread(() => timerWatchDocumentChange.Start());
        }

        /// <summary>Stops the watchdog — called from SwAddin.DisconnectFromSW so it never fires
        /// against a disconnected _swApp.</summary>
        public void StopDocumentWatchdog()
        {
            OnUiThread(() => timerWatchDocumentChange.Stop());
        }

        /// <summary>Backstop for whatever SwAddin's event-driven wiring misses: SolidWorks does
        /// not raise ActiveDocChangeNotify/ActiveModelDocChangeNotify when the last open document
        /// closes (there is nothing to change "to"), and FileCloseNotify's own deferred re-check
        /// (see SwAddin.OnFileCloseNotify) is a best-effort timing guess, not a guarantee. Every
        /// second, compare the live active document against what the pane is currently showing
        /// and reconcile through the same OnActiveDocChanged path a real event would use — this
        /// catches missed closes and missed activations alike. ActiveDocumentProvider's COM call
        /// is wrapped since SolidWorks can be mid-transition (e.g. closing its last document) when
        /// this fires; a failure here just skips the tick; the next one tries again.
        ///
        /// It is also what releases ApplyUnsavedDocumentLock: while the active document is held
        /// locked as never saved, each tick re-checks it for a file path and re-runs the full
        /// load once it has one. And it keeps the Configuration dropdown on the active
        /// configuration when that changes without going through the dropdown — see
        /// FollowActiveConfigurationAsync.
        ///
        /// COM identity caveat: ISldWorks.ActiveDoc can hand back a different RCW for the same
        /// underlying document across calls, which would make ReferenceEquals see a "change" that
        /// isn't one. OnActiveDocChangedAsync already compares by ReferenceEquals rather than
        /// path/title, so this stays consistent with it — if that ever proves unreliable in
        /// practice, both comparisons need to move to GetPathName()/GetTitle() together.</summary>
        private void timerWatchDocumentChange_Tick(object sender, EventArgs e)
        {
            if (_documentWatchdogTickInProgress)
                return;

            var provider = ActiveDocumentProvider;
            if (provider == null)
                return;

            ModelDoc2 current;
            bool firstSaved;
            bool configurationChanged;
            try
            {
                current = provider();
                var sameDocument = ReferenceEquals(current, _activeModel);
                // Same document, held locked as never saved, and it has a path now — its first
                // save just happened. SwAddin hooks no save event, and OnActiveDocChangedAsync
                // ignores re-fires for the same document reference, so only this tick sees it.
                firstSaved =
                    sameDocument
                    && _activeDocumentUnsaved
                    && !IsUnsavedDocument(current);
                // Same document, but not on the configuration the dropdown shows — switched in
                // SolidWorks itself, which nothing this add-in hooks reports, or not yet
                // reported when the document was loaded.
                configurationChanged =
                    sameDocument
                    && !firstSaved
                    && ActiveConfigurationDiffers(current);
            }
            catch (COMException)
            {
                return;
            }

            if (ReferenceEquals(current, _activeModel) && !firstSaved && !configurationChanged)
            {
                // Nothing changed about which document or configuration is showing — only
                // then is it worth asking whether its properties changed behind the pane's
                // back (GladosTaskPaneControl.PropertyWatch.cs).
                CheckForOutsidePropertyChange(current);
                return;
            }

            _documentWatchdogTickInProgress = true;
            RunGuarded(async () =>
            {
                try
                {
                    if (firstSaved)
                        await ReloadAllForModel(current);
                    else if (configurationChanged)
                        await FollowActiveConfigurationAsync(current);
                    else
                        await OnActiveDocChangedAsync(current);
                }
                finally
                {
                    _documentWatchdogTickInProgress = false;
                }
            });
        }

        /// <summary>Full re-check pipeline for a document: configurations, Kind, and
        /// (via the source gate) Bought Item properties + the Glados lookup. Used both for
        /// a genuine document switch and for a deliberate manual Reload of the same document
        /// — the latter can't go through OnActiveDocChanged since that ignores re-fires for
        /// a document reference that hasn't actually changed. A document that has never been
        /// saved stops at ApplyUnsavedDocumentLock instead — after the Downloads reference
        /// check, which reads no custom property and applies to a never-saved document too.</summary>
        private async Task ReloadAllForModel(ModelDoc2 model)
        {
            ShowDownloadsReferences(DownloadsReferenceService.FindDownloadsReferences(model));

            if (ApplyUnsavedDocumentLock(model))
                return;

            LoadConfigurationsForActiveModel(model);
            PopulateIdentityFields(model);
            await LoadKindForActiveModel(model);
        }

        /// <summary>Holds the pane locked while <paramref name="model"/> has never been saved
        /// and releases it once it has a file path — returns true while locked, so
        /// ReloadAllForModel stops there and no custom property is read from the document.
        /// Locked looks like no document at all (ResetToNoDocument) except that the
        /// Configuration dropdown still shows the active configuration, plus the Source and
        /// Configuration dropdowns disabled (ApplyConnectionState) and the Default tab saying
        /// why. The release comes from the document watchdog (timerWatchDocumentChange_Tick),
        /// since nothing else reports a document's first save.</summary>
        private bool ApplyUnsavedDocumentLock(ModelDoc2 model)
        {
            var unsaved = IsUnsavedDocument(model);
            SetActiveDocumentUnsaved(unsaved);

            if (!unsaved)
                return false;

            ResetToNoDocument();
            LoadConfigurationsForActiveModel(model);
            tabControlSource.SelectedTab = tabPageDefault;
            return true;
        }

        /// <summary>The only writer of _activeDocumentUnsaved. Re-applies the connection state
        /// only when the flag actually flips, since that's the one place the dropdowns it
        /// affects get their enabled state from.</summary>
        private void SetActiveDocumentUnsaved(bool unsaved)
        {
            labelDocumentNotSaved.Visible = unsaved;

            if (unsaved == _activeDocumentUnsaved)
                return;

            _activeDocumentUnsaved = unsaved;
            ApplyConnectionState();
        }

        /// <summary>Blanks everything document-specific — configurations, the identity boxes,
        /// the Source selection and every item tab — for when there is no document to show:
        /// none open at all (OnActiveDocChangedAsync), or one held locked until its first save
        /// (ApplyUnsavedDocumentLock). Leaves _activeModel alone; callers own that.</summary>
        private void ResetToNoDocument()
        {
            StopPropertyWatch();
            LoadConfigurationsForActiveModel(null);
            // No document, no identity — the only place the shared boxes are ever
            // blanked; see PopulateIdentityFields.
            textBoxPartNumber.Text = string.Empty;
            textBoxRevision.Text = string.Empty;
            _boughtItemLocal = null;
            _madeItemLocal = null;
            _assemblyLocal = null;
            _currentSource = null;
            _suppressSourceChange = true;
            try
            {
                comboBoxSource.SelectedIndex = -1;
            }
            finally
            {
                _suppressSourceChange = false;
            }
            // No Kind to lock against — drop any Kind lock left over from whichever document
            // was showing before, deferring to the rest of SourceDropdownAllowed rather than
            // forcing enabled. See ApplyKindLock.
            _activeDocumentKindLocked = false;
            SetSourceSelectionEnabled(SourceDropdownAllowed);
            _sourceLockTooltip.SetToolTip(comboBoxSource, string.Empty);
            SetBoughtItemTabEnabled(false);
            tabPageMade.Enabled = false;
            ClearMadeItemStatusIndicators();
            SetAssemblyTabEnabled(false);
        }

        /// <summary>Fills the shared Part Number / Revision boxes above the tab strip straight
        /// from the document, on every load and configuration switch and regardless of which
        /// kind (if any) the document has: they are PDM identity, not a tab's field, so they
        /// must never depend on the Source dropdown. Empty only when the properties genuinely
        /// don't exist on the document. The property names come from the field mapping (both
        /// kinds map the same PDM.PartNumber / PDM.Revision by default); the bought entry is
        /// used first, the made one second, so an unmapped kind never hides the identity.
        /// </summary>
        private void PopulateIdentityFields(ModelDoc2 model)
        {
            var mapping = ApplicationServices.FieldMapping;
            string PropertyFor(ItemField field) =>
                mapping.ResolvePropertyName(ItemKind.Bought, field)
                ?? mapping.ResolvePropertyName(ItemKind.Made, field);

            string ReadOrEmpty(string propertyName) =>
                propertyName != null ? SwCustomPropertyService.Read(model, propertyName) : string.Empty;

            var partNumber = ReadOrEmpty(PropertyFor(ItemField.PartNumber));
            var revision = ReadOrEmpty(PropertyFor(ItemField.Revision));

            OnUiThread(() =>
            {
                SetTextIfNotFocused(textBoxPartNumber, partNumber, forceOverwrite: true);
                SetTextIfNotFocused(textBoxRevision, revision, forceOverwrite: true);
            });
        }

        /// <summary>Reads Glados.Kind and applies the Bought-vs-Made tab gate for it — see
        /// ApplyKindLock. A document that doesn't have Glados.Kind yet leaves the dropdown
        /// enabled with no selection (or whatever it already showed), exactly like a brand-new
        /// document — see README's "Kind decides which catalogue". Blank/unrecognized values
        /// (Kind not set, or a stray value matching neither combo item) disable both tabs —
        /// see ApplySourceGateAsync.</summary>
        private async Task LoadKindForActiveModel(ModelDoc2 model)
        {
            // Every load (document switch, Reload, Save, configuration switch) comes through
            // here, before any tab reads a property.
            WatchPropertiesOf(model);
            ApplyKindLock(model);
            await ApplySourceGateAsync();
        }

        /// <summary>Reads Glados.Kind for the active configuration and, if it holds a valid
        /// value, forces the Source dropdown to match it and locks the dropdown against
        /// further edits — a document's kind, once stamped, never changes (see WriteKind and
        /// README's "Kind decides which catalogue"). Unlocks the dropdown (as far as the rest of
        /// SourceDropdownAllowed permits — this only ever narrows, never widens, what that
        /// already allows) and clears the tooltip when Kind isn't set — callers that already
        /// have their own selection in mind (LoadKindForActiveModel, via ApplySourceGateAsync)
        /// reconcile it from there; callers that don't (the post-save refresh in
        /// SaveBoughtItemPropertiesAsync / SaveMadeItemPropertiesAsync) just leave whatever's
        /// currently selected as-is.
        /// </summary>
        private void ApplyKindLock(ModelDoc2 model)
        {
            var kind = SwCustomPropertyService.ReadDocumentKind(model, ApplicationServices.FieldMapping);

            if (!kind.HasValue)
            {
                _activeDocumentKindLocked = false;
                SetSourceSelectionEnabled(SourceDropdownAllowed);
                _sourceLockTooltip.SetToolTip(comboBoxSource, string.Empty);
                return;
            }

            var wireValue = kind.Value.ToWireValue();

            _suppressSourceChange = true;
            try
            {
                comboBoxSource.SelectedIndex = comboBoxSource.FindStringExact(wireValue);
            }
            finally
            {
                _suppressSourceChange = false;
            }

            _currentSource = wireValue;
            _activeDocumentKindLocked = true;
            SetSourceSelectionEnabled(false);
            _sourceLockTooltip.SetToolTip(comboBoxSource, Strings.TooltipKindLocked);
        }

        /// <summary>Populates the configuration dropdown from the document and selects
        /// whichever configuration is currently active. Runs regardless of whether the
        /// document has a PDM part number — configurations aren't Bought-Item-specific — and
        /// for a document held locked as never saved too. The active configuration is always
        /// selectable, even if GetConfigurationNames didn't list it: the document watchdog
        /// compares this selection against SolidWorks's active configuration every second
        /// (ActiveConfigurationDiffers), and a name the dropdown could never select would read
        /// as a fresh switch on every tick.</summary>
        private void LoadConfigurationsForActiveModel(ModelDoc2 model)
        {
            _suppressConfigurationChange = true;
            try
            {
                comboBoxConfiguration.Items.Clear();

                if (model == null)
                    return;

                var configNames = model.GetConfigurationNames() as string[];
                if (configNames != null)
                {
                    foreach (var name in configNames)
                        comboBoxConfiguration.Items.Add(name);
                }

                var activeName = ActiveConfigurationName(model);
                if (activeName == null)
                    return;

                int index = comboBoxConfiguration.FindStringExact(activeName);
                if (index == -1)
                    index = comboBoxConfiguration.Items.Add(activeName);

                comboBoxConfiguration.SelectedIndex = index;
            }
            finally
            {
                _suppressConfigurationChange = false;
            }
        }

        /// <summary>Reads properties fresh from the document, populates the UI, and re-checks
        /// Glados. Called once ApplySourceGateAsync has already enabled the Bought tab, so a
        /// part with no PDM part number yet (e.g. not published) still has its fields editable
        /// here — RefreshBoughtItemLookupAsync still runs regardless, since the possible-
        /// duplicate check (Manufacturer + OrderCode1) doesn't need a part number either.</summary>
        private async Task LoadBoughtItemAsync(ModelDoc2 model)
        {
            SetEmbedImportedComponentsVisible(IsAssemblyDocument(model));
            _boughtItemLocal = SwCustomPropertyService.ReadBoughtItem(model, ApplicationServices.FieldMapping);

            PopulateBoughtItemFields(_boughtItemLocal);

            await RefreshBoughtItemLookupAsync();
        }

        private void SetBoughtItemTabEnabled(bool enabled)
        {
            OnUiThread(() => tabPageBought.Enabled = enabled);
            if (!enabled)
            {
                ClearDivergenceHighlights();
                ClearBoughtItemStatusIndicators();
            }
        }

        #region Bought item — State/Sync indicators

        private static readonly Color StateColorActive = Color.SeaGreen;
        private static readonly Color StateColorInactive = Color.Gray;
        private static readonly Color StateColorReplaced = Color.DarkOrange;

        /// <summary>Dimmed/neutral tone for "no catalogue item" - a completed lookup that
        /// simply found nothing, as opposed to an actual lifecycle status coming back from
        /// the catalogue. Deliberately distinct from StateColorInactive.</summary>
        private static readonly Color StateColorNeutral = SystemColors.GrayText;

        private static readonly Color SyncColorInSync = Color.SeaGreen;
        private static readonly Color SyncColorPending = Color.DarkOrange;
        private static readonly Color SyncColorNotFound = Color.SteelBlue;
        private static readonly Color SyncColorPossibleDuplicate = Color.Goldenrod;
        private static readonly Color SyncColorError = Color.Firebrick;

        /// <summary>Dimmed/neutral tone for "no part number yet" - there's nothing to sync
        /// rather than a problem, so this deliberately stays out of the InSync/Pending/
        /// NotFound/Error palette above.</summary>
        private static readonly Color SyncColorNeutral = SystemColors.GrayText;

        private void ClearBoughtItemStatusIndicators()
        {
            OnUiThread(() =>
            {
                SetIndicator(
                    labelBoughtItemStateMdi,
                    labelBoughtItemState,
                    string.Empty,
                    string.Empty,
                    SystemColors.ControlText
                );
                SetIndicator(
                    labelBoughtItemSyncMdi,
                    labelBoughtItemSync,
                    string.Empty,
                    string.Empty,
                    SystemColors.ControlText
                );
            });
            SetPossibleDuplicateTooltip(null);
            // A validation result belongs to the document it was computed for — never leave a
            // stale one showing once we've switched away from that document (or disabled the
            // tab entirely).
            SetPdmValidationNotChecked(
                labelBoughtItemPdmValidationMdi, labelBoughtItemPdmValidation, _boughtItemPdmValidationTooltip);
        }

        /// <summary>"State" is the catalogue item's own lifecycle status in Glados
        /// (active/inactive/replaced) — only meaningful once a matching remote item exists.</summary>
        private void SetStateIndicator(BoughtItemCatalogueStatus? status)
        {
            OnUiThread(() =>
            {
                switch (status)
                {
                    case BoughtItemCatalogueStatus.Active:
                        SetIndicator(
                            labelBoughtItemStateMdi,
                            labelBoughtItemState,
                            MaterialIcons.CheckCircle,
                            Strings.StateActive,
                            StateColorActive
                        );
                        break;
                    case BoughtItemCatalogueStatus.Inactive:
                        SetIndicator(
                            labelBoughtItemStateMdi,
                            labelBoughtItemState,
                            MaterialIcons.Block,
                            Strings.StateInactive,
                            StateColorInactive
                        );
                        break;
                    case BoughtItemCatalogueStatus.Replaced:
                        SetIndicator(
                            labelBoughtItemStateMdi,
                            labelBoughtItemState,
                            MaterialIcons.Autorenew,
                            Strings.StateReplaced,
                            StateColorReplaced
                        );
                        break;
                    default:
                        SetIndicator(
                            labelBoughtItemStateMdi,
                            labelBoughtItemState,
                            string.Empty,
                            string.Empty,
                            SystemColors.ControlText
                        );
                        break;
                }
            });
        }

        /// <summary>Renders the dimmed "no catalogue item" indicator for a lookup that
        /// completed but matched nothing (BoughtItemMatchState.NoPartNumber or NotFound, i.e.
        /// RemoteItem == null) — distinct from passing null to SetStateIndicator, which still
        /// means "clear to blank" for ClearBoughtItemStatusIndicators and the error paths
        /// below.</summary>
        private void SetStateIndicatorNone()
        {
            OnUiThread(() =>
                SetIndicator(
                    labelBoughtItemStateMdi,
                    labelBoughtItemState,
                    MaterialIcons.Cloud,
                    Strings.StateNoCatalogueItem,
                    StateColorNeutral
                )
            );
        }

        /// <summary>"Sync" is purely informational — whether these document property values
        /// have been ingested into Glados yet, independent of the item's State. This addin
        /// never pushes anything itself, so FoundDiverges just means "not checked in yet,"
        /// not a conflict the user needs to resolve.</summary>
        private void SetSyncIndicator(BoughtItemMatchState? state)
        {
            OnUiThread(() =>
            {
                switch (state)
                {
                    case BoughtItemMatchState.NoPartNumber:
                        SetIndicator(
                            labelBoughtItemSyncMdi,
                            labelBoughtItemSync,
                            MaterialIcons.CloudOff,
                            Strings.SyncNoPartNumber,
                            SyncColorNeutral
                        );
                        break;
                    case BoughtItemMatchState.FoundInSync:
                        SetIndicator(
                            labelBoughtItemSyncMdi,
                            labelBoughtItemSync,
                            MaterialIcons.CloudDone,
                            Strings.SyncInSync,
                            SyncColorInSync
                        );
                        break;
                    case BoughtItemMatchState.FoundDiverges:
                        SetIndicator(
                            labelBoughtItemSyncMdi,
                            labelBoughtItemSync,
                            MaterialIcons.ErrorOutline,
                            Strings.SyncNotCheckedIn,
                            SyncColorPending
                        );
                        break;
                    case BoughtItemMatchState.NotFound:
                        SetIndicator(
                            labelBoughtItemSyncMdi,
                            labelBoughtItemSync,
                            MaterialIcons.CloudUpload,
                            Strings.SyncNotInGlados,
                            SyncColorNotFound
                        );
                        break;
                    case BoughtItemMatchState.PossibleDuplicate:
                        SetIndicator(
                            labelBoughtItemSyncMdi,
                            labelBoughtItemSync,
                            MaterialIcons.ErrorOutline,
                            Strings.SyncPossibleDuplicate,
                            SyncColorPossibleDuplicate
                        );
                        break;
                    default:
                        SetIndicator(
                            labelBoughtItemSyncMdi,
                            labelBoughtItemSync,
                            string.Empty,
                            string.Empty,
                            SystemColors.ControlText
                        );
                        break;
                }
            });
        }

        private void SetSyncError()
        {
            OnUiThread(() =>
                SetIndicator(
                    labelBoughtItemSyncMdi,
                    labelBoughtItemSync,
                    MaterialIcons.ErrorOutline,
                    Strings.SyncLookupFailed,
                    SyncColorError
                )
            );
        }

        /// <summary>The "Possible duplicate" indicator alone doesn't have room to say which
        /// manufacturer/order code matched — a hover tooltip carries that detail so the user can
        /// judge for themselves whether they're duplicating data entry. Purely informational:
        /// this addin never merges or creates anything itself.</summary>
        private void SetPossibleDuplicateTooltip(BoughtItemResponse duplicate)
        {
            var detail =
                duplicate == null
                    ? string.Empty
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PossibleDuplicateDetailFormat,
                        duplicate.ManufacturerName,
                        duplicate.OrderCode1,
                        duplicate.ArticleName
                    );

            OnUiThread(() =>
            {
                _boughtItemSyncTooltip.SetToolTip(labelBoughtItemSync, detail);
                _boughtItemSyncTooltip.SetToolTip(labelBoughtItemSyncMdi, detail);
            });
        }

        private static void SetIndicator(
            Label mdiLabel,
            Label textLabel,
            string icon,
            string text,
            Color color
        )
        {
            mdiLabel.Text = icon;
            mdiLabel.ForeColor = color;
            textLabel.Text = text;
            textLabel.ForeColor = color;
        }

        #endregion

        /// <summary>Disables the Bought Item tab and blanks every bought-tab field — used when
        /// the Source dropdown says this document isn't a bought item ("made" instead of
        /// "bought"). The shared Part Number / Revision boxes are deliberately not touched:
        /// they belong to the document, not the tab — see PopulateIdentityFields.</summary>
        private void ClearBoughtItemFields()
        {
            SetBoughtItemTabEnabled(false);

            OnUiThread(() =>
            {
                textBoxBoughtItemArticleName.Text = string.Empty;
                textBoxBoughtItemOrderCode1.Text = string.Empty;
                textBoxBoughtItemOrderCode2.Text = string.Empty;
                textBoxBoughtItemGTIN.Text = string.Empty;
                textBoxBoughtItemEAN.Text = string.Empty;
                textBoxBoughtItemTarifNumber.Text = string.Empty;
                textBoxBoughtItemNote.Text = string.Empty;
                textBoxBoughtItemWeblink.Text = string.Empty;
                comboBoxBoughtItemManufacturer.SelectedIndex = -1;
                for (int i = 0; i < checkedListBoxBoughtItemArticleGroups.Items.Count; i++)
                    checkedListBoxBoughtItemArticleGroups.SetItemChecked(i, false);
                _boughtItemEdpPartId = null;
                ClearDataPortalHits();

                UpdateDirtyMarkers();
                UpdateVisitWeblinkButtonEnabled();
            });

            _boughtItemLocal = null;
        }

        /// <summary>forceOverwrite skips the "don't clobber a focused control" guard below —
        /// used for Load from Glados, since that's an explicit, deliberate pull of authoritative
        /// server data rather than an incidental/automatic refresh, so it should always show
        /// what the server actually has. Also works around SolidWorks task panes sometimes
        /// reporting a control as still Focused after the panel itself has lost activation,
        /// which otherwise silently skips populating whichever field was focused last —
        /// reported for Weblink, but the same stale-Focused risk applies to every field.</summary>
        private void PopulateBoughtItemFields(
            BoughtItemProperties local,
            bool forceOverwrite = false
        )
        {
            // A reload the property watch started may overwrite a focused control too - see
            // _overwriteFocusedOnPopulate.
            forceOverwrite = forceOverwrite || _overwriteFocusedOnPopulate;
            OnUiThread(() =>
            {
                void SetText(TextBox box, string value) =>
                    SetTextIfNotFocused(box, value, forceOverwrite);

                // Never overwrite a control the user currently has focused/open — e.g. Reload
                // From File shouldn't yank a field out from under an in-progress interaction.
                SetText(textBoxPartNumber, local.PartNumber);
                SetText(textBoxRevision, local.Revision);
                SetText(textBoxBoughtItemArticleName, local.ArticleName);
                SetText(textBoxBoughtItemOrderCode1, local.OrderCode1);
                SetText(textBoxBoughtItemOrderCode2, local.OrderCode2);
                SetText(textBoxBoughtItemGTIN, local.Gtin);
                SetText(textBoxBoughtItemEAN, local.Ean);
                SetText(textBoxBoughtItemTarifNumber, local.TariffNumber);
                SetText(textBoxBoughtItemNote, local.Note);
                SetText(textBoxBoughtItemWeblink, local.Weblink);

                if (forceOverwrite || !comboBoxBoughtItemManufacturer.Focused)
                    SetManufacturerSelection(local.ManufacturerId, local.ManufacturerName);
                if (forceOverwrite || !checkedListBoxBoughtItemArticleGroups.Focused)
                    SetArticleGroupSelection(local.ArticleGroupIds, local.ArticleGroupNames);

                // No control to keep focus on, so the link always follows what is populated.
                _boughtItemEdpPartId = local.EdpPartId;
                ClearDataPortalHits();

                ClearDivergenceHighlights();

                // Explicit call rather than relying solely on the TextChanged/etc. cascade above:
                // WinForms doesn't raise those events when a control is set to its current value,
                // which would otherwise leave a stale marker in place (e.g. Reload From File
                // setting a field back to text that already matches what's on screen).
                UpdateDirtyMarkers();
                UpdateVisitWeblinkButtonEnabled();
            });
        }

        /// <summary>The Visit button only makes sense once there's actually something typed.</summary>
        private void UpdateVisitWeblinkButtonEnabled()
        {
            buttonBoughtItemVisitWeblink.Enabled = !string.IsNullOrWhiteSpace(
                textBoxBoughtItemWeblink.Text
            );
        }

        private static void SetTextIfNotFocused(
            TextBox box,
            string value,
            bool forceOverwrite = false
        )
        {
            if (forceOverwrite || !box.Focused)
                box.Text = value;
        }

        /// <summary>Selects the document's manufacturer. The visible choices are the current
        /// lookup snapshot's non-archived suppliers, plus manufacturerId itself if it names an
        /// archived-but-still-known supplier (VisibleRows keeps it, with its real current
        /// label). A synthetic placeholder entry is added only as a last resort, when
        /// manufacturerId isn't in the snapshot at all — the cache can be stale, empty, or
        /// unreachable. Without it the combo blanks, ReadBoughtItemFromUi reads null, and the
        /// next Save wipes the document's manufacturer link. The stored label is what makes
        /// that placeholder readable; "#id" is the last resort.</summary>
        private void SetManufacturerSelection(int? manufacturerId, string manufacturerName)
        {
            var keep = manufacturerId.HasValue ? new[] { manufacturerId.Value } : Array.Empty<int>();

            _manufacturerChoices.Clear();
            _manufacturerChoices.AddRange(VisibleRows(
                ApplicationServices.Lookups.Current.Suppliers, s => s.Archived, s => s.Id, s => s.Name, keep));

            if (manufacturerId.HasValue && !_manufacturerChoices.Any(s => s.Id == manufacturerId.Value))
            {
                _manufacturerChoices.Add(
                    new SupplierResponse
                    {
                        Id = manufacturerId.Value,
                        Name = PlaceholderLabel(manufacturerName, manufacturerId.Value),
                    }
                );
            }

            BindManufacturerChoices();

            if (manufacturerId.HasValue)
                comboBoxBoughtItemManufacturer.SelectedValue = manufacturerId.Value;
            else
                comboBoxBoughtItemManufacturer.SelectedIndex = -1;
        }

        /// <summary>Same reasoning as SetManufacturerSelection, against
        /// checkedListBoxBoughtItemArticleGroups: the visible choices are the current lookup
        /// snapshot's non-archived article groups plus any of articleGroupIds that name an
        /// archived-but-still-known group; a synthetic placeholder covers only the ids not in
        /// the snapshot at all. The list is rebuilt from scratch each time so placeholders from
        /// a previously active document don't accumulate.</summary>
        private void SetArticleGroupSelection(
            List<int> articleGroupIds,
            List<string> articleGroupNames
        )
        {
            var wanted = articleGroupIds ?? new List<int>();

            var visible = VisibleRows(
                ApplicationServices.Lookups.Current.ArticleGroups,
                g => g.Archived, g => g.Id, g => g.Name, wanted, g => g.SortOrder);

            checkedListBoxBoughtItemArticleGroups.Items.Clear();
            foreach (var group in visible)
                checkedListBoxBoughtItemArticleGroups.Items.Add(group);

            for (int i = 0; i < wanted.Count; i++)
            {
                if (visible.Any(g => g.Id == wanted[i]))
                    continue;

                var storedName =
                    articleGroupNames != null && i < articleGroupNames.Count
                        ? articleGroupNames[i]
                        : null;

                checkedListBoxBoughtItemArticleGroups.Items.Add(
                    new ArticleGroupResponse
                    {
                        Id = wanted[i],
                        Name = PlaceholderLabel(storedName, wanted[i]),
                    }
                );
            }

            var ids = new HashSet<int>(wanted);
            for (int i = 0; i < checkedListBoxBoughtItemArticleGroups.Items.Count; i++)
            {
                var group = (ArticleGroupResponse)checkedListBoxBoughtItemArticleGroups.Items[i];
                checkedListBoxBoughtItemArticleGroups.SetItemChecked(i, ids.Contains(group.Id));
            }
        }

        /// <summary>The stored label if the document carries one, otherwise the bare id — which
        /// at least says "this references something" instead of showing nothing at all.</summary>
        private static string PlaceholderLabel(string storedName, int id)
        {
            return string.IsNullOrWhiteSpace(storedName)
                ? "#" + id.ToString(CultureInfo.InvariantCulture)
                : storedName;
        }

        /// <summary>Empty string when there's no manufacturer (clear the label), the supplier's
        /// name when the current lookup snapshot can resolve it (checked against every row,
        /// including archived ones — see docs/FIELD_OWNERSHIP.md), null when it can't — see
        /// WriteManufacturer for what null means.</summary>
        private string ResolveManufacturerLabel(int? manufacturerId)
        {
            if (manufacturerId == null)
                return string.Empty;

            return ApplicationServices.Lookups.Current.Suppliers
                .FirstOrDefault(s => s.Id == manufacturerId.Value)?.Name;
        }

        /// <summary>Labels for the checked article groups, or null if even one id can't be
        /// resolved against the current lookup snapshot (every row, including archived) — a
        /// partial list would silently drop a group's label while keeping its id.</summary>
        private List<string> ResolveArticleGroupLabels(List<int> articleGroupIds)
        {
            var ids = articleGroupIds ?? new List<int>();
            var labels = new List<string>();
            var articleGroups = ApplicationServices.Lookups.Current.ArticleGroups;

            foreach (var id in ids)
            {
                var group = articleGroups.FirstOrDefault(g => g.Id == id);
                if (group == null)
                    return null;

                labels.Add(group.Name);
            }

            return labels;
        }

        #region Bought item — unsaved-change markers

        /// <summary>Live-marks each field's label with " *" whenever its current UI value
        /// differs from _boughtItemLocal (the last-saved-to-document baseline).</summary>
        private void WireDirtyTracking()
        {
            CaptureBaseLabelText(labelBoughtItemArticleName);
            CaptureBaseLabelText(labelBoughtItemOrderCode1);
            CaptureBaseLabelText(labelBoughtItemOrderCode2);
            CaptureBaseLabelText(labelBoughtItemGTIN);
            CaptureBaseLabelText(labelBoughtItemEAN);
            CaptureBaseLabelText(labelBoughtItemTariffNumber);
            CaptureBaseLabelText(labelBoughtItemNote);
            CaptureBaseLabelText(labelBoughtItemWeblink);
            CaptureBaseLabelText(labelBoughtItemManufacturer);
            CaptureBaseLabelText(labelBoughtItemGroups);

            textBoxBoughtItemArticleName.TextChanged += (s, e) => UpdateDirtyMarkers();
            textBoxBoughtItemOrderCode1.TextChanged += (s, e) => UpdateDirtyMarkers();
            textBoxBoughtItemOrderCode2.TextChanged += (s, e) => UpdateDirtyMarkers();
            textBoxBoughtItemGTIN.TextChanged += (s, e) => UpdateDirtyMarkers();
            textBoxBoughtItemEAN.TextChanged += (s, e) => UpdateDirtyMarkers();
            textBoxBoughtItemTarifNumber.TextChanged += (s, e) => UpdateDirtyMarkers();
            textBoxBoughtItemNote.TextChanged += (s, e) => UpdateDirtyMarkers();
            textBoxBoughtItemWeblink.TextChanged += (s, e) =>
            {
                UpdateDirtyMarkers();
                UpdateVisitWeblinkButtonEnabled();
            };
            comboBoxBoughtItemManufacturer.SelectedIndexChanged += (s, e) => UpdateDirtyMarkers();

            // ItemCheck fires before the check state is actually applied, so defer to
            // after this handler returns to read the settled selection.
            checkedListBoxBoughtItemArticleGroups.ItemCheck += (s, e) =>
                BeginInvoke(new Action(UpdateDirtyMarkers));
        }

        private void CaptureBaseLabelText(Label label)
        {
            _baseLabelText[label] = label.Text;
        }

        private void UpdateDirtyMarkers()
        {
            var saved = _boughtItemLocal;

            SetDirtyMarker(
                labelBoughtItemArticleName,
                !TextMatches(textBoxBoughtItemArticleName.Text, saved?.ArticleName)
            );
            SetDirtyMarker(
                labelBoughtItemOrderCode1,
                !TextMatches(textBoxBoughtItemOrderCode1.Text, saved?.OrderCode1)
            );
            SetDirtyMarker(
                labelBoughtItemOrderCode2,
                !TextMatches(textBoxBoughtItemOrderCode2.Text, saved?.OrderCode2)
            );
            SetDirtyMarker(
                labelBoughtItemGTIN,
                !TextMatches(textBoxBoughtItemGTIN.Text, saved?.Gtin)
            );
            SetDirtyMarker(labelBoughtItemEAN, !TextMatches(textBoxBoughtItemEAN.Text, saved?.Ean));
            SetDirtyMarker(
                labelBoughtItemTariffNumber,
                !TextMatches(textBoxBoughtItemTarifNumber.Text, saved?.TariffNumber)
            );
            SetDirtyMarker(
                labelBoughtItemNote,
                !TextMatches(textBoxBoughtItemNote.Text, saved?.Note)
            );
            SetDirtyMarker(
                labelBoughtItemWeblink,
                !TextMatches(textBoxBoughtItemWeblink.Text, saved?.Weblink)
            );

            var currentManufacturerId = comboBoxBoughtItemManufacturer.SelectedValue as int?;
            SetDirtyMarker(
                labelBoughtItemManufacturer,
                currentManufacturerId != saved?.ManufacturerId
            );

            var currentGroupIds = new HashSet<int>(GetCheckedArticleGroupIds());
            var savedGroupIds = new HashSet<int>(saved?.ArticleGroupIds ?? new List<int>());
            SetDirtyMarker(labelBoughtItemGroups, !currentGroupIds.SetEquals(savedGroupIds));

            UpdateDataPortalLink();
            UpdateBoughtItemRequiredMarkers();
        }

        private void SetDirtyMarker(Label label, bool dirty)
        {
            var baseText = _baseLabelText.TryGetValue(label, out var text) ? text : label.Text;
            label.Text = dirty ? baseText + " *" : baseText;
        }

        private static bool TextMatches(string uiValue, string savedValue)
        {
            return string.Equals(uiValue ?? string.Empty, savedValue ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>Made-item Mass display text for a loaded/saved grams value — plain integer,
        /// invariant separators, since grams never needs a fraction. The counterpart on read is
        /// SwCustomPropertyService.ParseMassGrams, which tolerates whatever the user actually
        /// typed (comma or dot, current or invariant culture) rather than requiring this exact
        /// format back.</summary>
        private static string FormatMassText(int? massGrams)
        {
            return massGrams.HasValue ? massGrams.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        #endregion

        #region Required-field markers

        private static bool IsBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>Colors <paramref name="label"/> red and attaches
        /// <paramref name="tooltipText"/> when <paramref name="missing"/> is true — the
        /// required-field marker (docs/FIELD_OWNERSHIP.md "required fields"). Independent of,
        /// and combinable with, SetDirtyMarker's " *" suffix on the same label's Text: one
        /// channel is color, the other is text, so a field can be both dirty and
        /// required-missing at once without either marker clobbering the other. Resets to the
        /// label's default ControlText color and clears the tooltip once satisfied — none of
        /// the labels this is called on carry a custom ForeColor of their own.</summary>
        private void SetRequiredMarker(Label label, bool missing, string tooltipText)
        {
            label.ForeColor = missing ? RequiredFieldMissingColor : SystemColors.ControlText;
            _requiredFieldTooltip.SetToolTip(label, missing ? tooltipText : string.Empty);
        }

        /// <summary>Part Number and Revision are locked-required for both kinds
        /// (docs/PDM_INTEGRATION.md section 4.4) and shown on the shared textBoxPartNumber/
        /// textBoxRevision controls above the tab strip rather than per-tab, so this is called
        /// from both UpdateBoughtItemRequiredMarkers and UpdateMadeItemRequiredMarkers with
        /// whichever kind is actually loaded — the two never disagree on whether either field
        /// is required, only Resolve's per-kind entry differs, and both are always true.</summary>
        private void UpdateIdentityRequiredMarkers(ItemKind kind)
        {
            var mapping = ApplicationServices.FieldMapping;

            UpdateIdentityFieldMarker(
                labelPartNumber, textBoxPartNumber,
                mapping.Resolve(kind, ItemField.PartNumber),
                Strings.PlaceholderPartNumberAssignedByPdm);
            UpdateIdentityFieldMarker(
                labelRevision, textBoxRevision,
                mapping.Resolve(kind, ItemField.Revision),
                Strings.PlaceholderRevisionAssignedByPdm);
        }

        /// <summary>Shared by UpdateIdentityRequiredMarkers for Part Number and Revision. Both
        /// are Pdm-owned by default (FieldMapping.Defaults): PDM's own release workflow fills
        /// them in, not the engineer, so an empty box is the expected pre-release state, not a
        /// missing-field problem — no red marker, a cue-banner placeholder on the (read-only)
        /// box instead (Helpers/TextBoxCueBanner; cleared automatically the moment
        /// PopulateIdentityFields/SetTextIfNotFocused sets real text). Required-but-not-PDM-owned
        /// — an admin could remap part_number/revision onto an add-in-owned property — keeps the
        /// ordinary marker behaviour, so remapping never silently drops the required check.
        /// <paramref name="entry"/> is null when the field has no mapping entry at all, the same
        /// "unmapped, no opinion" reading every other required-marker call gives.</summary>
        private void UpdateIdentityFieldMarker(
            Label label, TextBox box, FieldMappingEntry entry, string placeholderText)
        {
            bool required = entry?.Required ?? false;
            bool blank = IsBlank(box.Text);
            bool pendingPdm = required && blank && entry.Owner == FieldOwner.Pdm;

            TextBoxCueBanner.SetCueBanner(box, pendingPdm ? placeholderText : string.Empty);
            SetRequiredMarker(label, required && blank && !pendingPdm, Strings.TooltipRequiredField);
        }

        /// <summary>Marks every bought-item field the pane exposes an edit control for that the
        /// current field mapping says is required and whose UI value is currently empty. Fields
        /// with no edit control at all on this tab (there are none for bought items — every
        /// AddIn-owned field in FieldMapping.Defaults' bought table has one) simply can't be
        /// marked and are silently skipped, the same reasoning UpdateMadeItemRequiredMarkers
        /// applies to made-item fields like `base_size` that have no control yet.</summary>
        private void UpdateBoughtItemRequiredMarkers()
        {
            const ItemKind kind = ItemKind.Bought;
            var mapping = ApplicationServices.FieldMapping;

            UpdateIdentityRequiredMarkers(kind);

            SetRequiredMarker(
                labelBoughtItemArticleName,
                mapping.IsRequired(kind, ItemField.ArticleName) && IsBlank(textBoxBoughtItemArticleName.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelBoughtItemOrderCode1,
                mapping.IsRequired(kind, ItemField.OrderCode1) && IsBlank(textBoxBoughtItemOrderCode1.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelBoughtItemOrderCode2,
                mapping.IsRequired(kind, ItemField.OrderCode2) && IsBlank(textBoxBoughtItemOrderCode2.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelBoughtItemGTIN,
                mapping.IsRequired(kind, ItemField.Gtin) && IsBlank(textBoxBoughtItemGTIN.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelBoughtItemEAN,
                mapping.IsRequired(kind, ItemField.Ean) && IsBlank(textBoxBoughtItemEAN.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelBoughtItemTariffNumber,
                mapping.IsRequired(kind, ItemField.TariffNumber) && IsBlank(textBoxBoughtItemTarifNumber.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelBoughtItemNote,
                mapping.IsRequired(kind, ItemField.Note) && IsBlank(textBoxBoughtItemNote.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelBoughtItemWeblink,
                mapping.IsRequired(kind, ItemField.Weblink) && IsBlank(textBoxBoughtItemWeblink.Text),
                Strings.TooltipRequiredField);

            var manufacturerId = comboBoxBoughtItemManufacturer.SelectedValue as int?;
            SetRequiredMarker(
                labelBoughtItemManufacturer,
                mapping.IsRequired(kind, ItemField.ManufacturerId) && manufacturerId == null,
                Strings.TooltipRequiredField);

            SetRequiredMarker(
                labelBoughtItemGroups,
                mapping.IsRequired(kind, ItemField.ArticleGroupIds) && GetCheckedArticleGroupIds().Count == 0,
                Strings.TooltipRequiredField);
        }

        #endregion

        /// <summary>Builds a BoughtItemProperties from whatever is currently shown in the UI
        /// (i.e. possibly-unsaved edits), for the Save button to write out.</summary>
        private BoughtItemProperties ReadBoughtItemFromUi()
        {
            var manufacturerId = comboBoxBoughtItemManufacturer.SelectedValue as int?;
            var articleGroupIds = GetCheckedArticleGroupIds();

            return new BoughtItemProperties
            {
                PartNumber = _boughtItemLocal?.PartNumber,
                Revision = _boughtItemLocal?.Revision,
                ArticleName = textBoxBoughtItemArticleName.Text,
                OrderCode1 = textBoxBoughtItemOrderCode1.Text,
                OrderCode2 = textBoxBoughtItemOrderCode2.Text,
                Gtin = textBoxBoughtItemGTIN.Text,
                Ean = textBoxBoughtItemEAN.Text,
                TariffNumber = textBoxBoughtItemTarifNumber.Text,
                Note = textBoxBoughtItemNote.Text,
                Weblink = textBoxBoughtItemWeblink.Text,
                Mass = _boughtItemLocal?.Mass,
                ManufacturerId = manufacturerId,
                // Mirrors exactly what the save path writes, so the snapshot stays faithful to
                // the document: a resolved label, or the one already stored when the lookup
                // can't resolve the id. Taking the label off the control instead would put a
                // "#id" placeholder in the snapshot while the document still holds a real name.
                ManufacturerName =
                    ResolveManufacturerLabel(manufacturerId) ?? _boughtItemLocal?.ManufacturerName,
                ArticleGroupIds = articleGroupIds,
                ArticleGroupNames =
                    ResolveArticleGroupLabels(articleGroupIds)
                    ?? _boughtItemLocal?.ArticleGroupNames
                    ?? new List<string>(),
                EdpPartId = _boughtItemEdpPartId,
            };
        }

        private List<ArticleGroupResponse> GetCheckedArticleGroups()
        {
            var groups = new List<ArticleGroupResponse>();
            for (int i = 0; i < checkedListBoxBoughtItemArticleGroups.Items.Count; i++)
            {
                if (checkedListBoxBoughtItemArticleGroups.GetItemChecked(i))
                    groups.Add(
                        (ArticleGroupResponse)checkedListBoxBoughtItemArticleGroups.Items[i]
                    );
            }
            return groups;
        }

        private List<int> GetCheckedArticleGroupIds()
        {
            return GetCheckedArticleGroups().Select(g => g.Id).ToList();
        }

        /// <summary>Maps a Glados catalogue item onto the UI-editable fields. PartNumber/Revision
        /// are PDM-controlled and always kept from the current local snapshot, never from Glados.</summary>
        private BoughtItemProperties MapFromRemote(BoughtItemResponse remote)
        {
            return new BoughtItemProperties
            {
                PartNumber = _boughtItemLocal?.PartNumber,
                Revision = _boughtItemLocal?.Revision,
                ArticleName = remote.ArticleName,
                OrderCode1 = remote.OrderCode1,
                OrderCode2 = remote.OrderCode2,
                Gtin = remote.Gtin,
                Ean = remote.Ean,
                TariffNumber = remote.TariffNumber,
                Note = remote.Note,
                Weblink = remote.Weblink,
                Mass = remote.Mass,
                ManufacturerId = remote.ManufacturerId,
                // Labels come from the response rather than the local lookup cache: it's the
                // authoritative source and it can't be stale the way the cache can.
                ManufacturerName = remote.ManufacturerName,
                ArticleGroupIds = (remote.ArticleGroups ?? new List<ArticleGroupResponse>())
                    .Select(g => g.Id)
                    .ToList(),
                ArticleGroupNames = (remote.ArticleGroups ?? new List<ArticleGroupResponse>())
                    .Select(g => g.Name)
                    .ToList(),
                EdpPartId = remote.EdpPartId,
            };
        }

        /// <summary>Fires the possible-duplicate check as soon as Manufacturer/OrderCode1 are sane,
        /// without waiting for Save — Leave (focus loss) on either field, checked against
        /// whatever's currently typed/selected rather than the last-saved _boughtItemLocal
        /// baseline, since the user hasn't necessarily written these edits to the document yet.</summary>
        private void WirePossibleDuplicateLiveCheck()
        {
            textBoxBoughtItemOrderCode1.Leave += (s, e) =>
                RunGuarded(CheckPossibleDuplicateFromUiAsync);
            comboBoxBoughtItemManufacturer.Leave += (s, e) =>
                RunGuarded(CheckPossibleDuplicateFromUiAsync);
        }

        private async Task CheckPossibleDuplicateFromUiAsync()
        {
            if (_activeModel == null || !_isConnected)
                return;

            var manufacturerId = comboBoxBoughtItemManufacturer.SelectedValue as int?;
            var orderCode1 = textBoxBoughtItemOrderCode1.Text;

            if (manufacturerId == null || string.IsNullOrWhiteSpace(orderCode1))
            {
                SetPossibleDuplicateTooltip(null);
                return;
            }

            int generation = _loadGeneration;
            var syncService = new BoughtItemSyncService(ApplicationServices.Api.BoughtItems);
            BoughtItemResponse duplicate;
            try
            {
                duplicate = await syncService.FindPossibleDuplicateAsync(
                    new BoughtItemProperties
                    {
                        ManufacturerId = manufacturerId,
                        OrderCode1 = orderCode1,
                    }
                );
            }
            catch (GladosApiException)
            {
                return;
            }
            catch (HttpRequestException)
            {
                return;
            }

            if (generation != _loadGeneration)
                // A document/configuration/source load has since started — this result no
                // longer describes what's on screen, so discard it rather than paint it over
                // whatever's actually active now.
                return;

            if (duplicate == null)
            {
                SetPossibleDuplicateTooltip(null);
                return;
            }

            SetSyncIndicator(BoughtItemMatchState.PossibleDuplicate);
            SetStateIndicator(duplicate.Status);
            SetPossibleDuplicateTooltip(duplicate);
        }

        #region Bought item — EPLAN Data Portal

        /// <summary>Asks Glados whether its Data Portal is switched on, then lets
        /// ApplyConnectionState decide what that means for the controls. Never throws: a
        /// failure, or a backend that predates the route (404), reads as "off".</summary>
        private async Task RefreshDataPortalStatusAsync()
        {
            try
            {
                var status = await ApplicationServices.Api.DataPortal.GetStatusAsync();
                _dataPortalEnabled = status?.Enabled == true;
                if (!_dataPortalEnabled)
                    Log.Info("Glados reports the EPLAN Data Portal as switched off or without a token");
            }
            catch (GladosApiException ex)
            {
                _dataPortalEnabled = false;
                Log.Warning($"Data Portal status request failed ({(int)ex.StatusCode} {ex.Code})", ex);
            }
            catch (HttpRequestException ex)
            {
                _dataPortalEnabled = false;
                Log.Warning("Data Portal status request could not reach the server", ex);
            }
        }

        /// <summary>Shows or hides the Portal button and gives Order Code 1 the room the button
        /// leaves free. Both are anchored right, so the button's current edges already follow
        /// the pane's width.</summary>
        private void SetDataPortalButtonVisible(bool visible)
        {
            if (buttonBoughtItemDataPortal.Visible == visible)
                return;

            buttonBoughtItemDataPortal.Visible = visible;
            var right = visible ? buttonBoughtItemDataPortal.Left - 6 : buttonBoughtItemDataPortal.Right;
            textBoxBoughtItemOrderCode1.Width = right - textBoxBoughtItemOrderCode1.Left;
        }

        /// <summary>Looks Order Code 1 up as soon as the user leaves it, like the
        /// possible-duplicate check, so a known part announces itself next to the field without
        /// a click. Typing a different code drops the hits of the previous one at once.</summary>
        private void WireDataPortalLiveCheck()
        {
            textBoxBoughtItemOrderCode1.Leave += (s, e) => RunGuarded(CheckDataPortalMatchFromUiAsync);
            textBoxBoughtItemOrderCode1.TextChanged += (s, e) =>
            {
                if (!string.Equals(
                        textBoxBoughtItemOrderCode1.Text.Trim(), _dataPortalMatchedNumber, StringComparison.Ordinal))
                {
                    ClearDataPortalHits();
                    UpdateDataPortalLink();
                }
            };
        }

        private async Task CheckDataPortalMatchFromUiAsync()
        {
            var number = textBoxBoughtItemOrderCode1.Text.Trim();
            if (_activeModel == null || !_dataPortalUsable || number.Length < 2)
                return;
            if (string.Equals(number, _dataPortalMatchedNumber, StringComparison.Ordinal))
                return;

            int generation = _loadGeneration;
            var manufacturerId = comboBoxBoughtItemManufacturer.SelectedValue as int?;
            EdpSearchResponse result;
            try
            {
                result = await ApplicationServices.Api.DataPortal.MatchAsync(number, manufacturerId);
            }
            catch (GladosApiException)
            {
                return;
            }
            catch (HttpRequestException)
            {
                return;
            }

            OnUiThread(() =>
            {
                // A newer load, or a code typed while this was in flight, makes the answer
                // describe something no longer on screen.
                if (generation != _loadGeneration
                    || !string.Equals(textBoxBoughtItemOrderCode1.Text.Trim(), number, StringComparison.Ordinal))
                    return;

                _dataPortalMatchedNumber = number;
                _dataPortalHits = (IReadOnlyList<EdpSearchHitResponse>)result?.Hits
                    ?? new List<EdpSearchHitResponse>();
                UpdateDataPortalLink();
            });
        }

        private void ClearDataPortalHits()
        {
            _dataPortalMatchedNumber = null;
            _dataPortalHits = new List<EdpSearchHitResponse>();
        }

        /// <summary>The line next to Order Code 1: a link to the parts the background match
        /// found, or, when it found nothing new, the part the tab is already linked to (" *"
        /// while that link is unsaved). A single hit that is the linked part itself is not news,
        /// so it shows the link, still clickable to reopen the part.</summary>
        private void UpdateDataPortalLink()
        {
            var hits = _dataPortalUsable ? _dataPortalHits : new List<EdpSearchHitResponse>();
            bool onlyTheLinkedPart = hits.Count == 1 && hits[0].Part?.PartId == _boughtItemEdpPartId;
            string text;
            if (hits.Count > 0 && !onlyTheLinkedPart)
                text = string.Format(CultureInfo.CurrentCulture, Strings.LinkDataPortalFoundFormat, hits.Count);
            else if (_boughtItemEdpPartId.HasValue)
            {
                text = string.Format(CultureInfo.CurrentCulture, Strings.LabelDataPortalLinkedFormat, _boughtItemEdpPartId.Value);
                if (_boughtItemLocal != null && _boughtItemLocal.EdpPartId != _boughtItemEdpPartId)
                    text += " *";
            }
            else
                text = string.Empty;

            bool clickable = hits.Count > 0;
            linkLabelBoughtItemDataPortal.Text = text;
            linkLabelBoughtItemDataPortal.LinkArea = clickable ? new LinkArea(0, text.Length) : new LinkArea(0, 0);
            _dataPortalLinkTooltip.SetToolTip(
                linkLabelBoughtItemDataPortal, clickable ? Strings.TooltipDataPortalFound : string.Empty);
        }

        private void buttonBoughtItemDataPortal_Click(object sender, EventArgs e)
        {
            RunGuarded(() => OpenDataPortalAsync(null));
        }

        private void linkLabelBoughtItemDataPortal_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            RunGuarded(() => OpenDataPortalAsync(_dataPortalHits));
        }

        /// <summary>Opens the Data Portal dialog on Order Code 1, with the background match's
        /// hits when there are any. What the engineer takes is shown as unsaved edits, the same
        /// as Load from Glados: nothing reaches the document before Save, and nothing reaches
        /// Glados at all (README's "The invariant"). Not actually async, see
        /// SelectSourceItemAsync.</summary>
        private Task OpenDataPortalAsync(IReadOnlyList<EdpSearchHitResponse> hits)
        {
            if (_activeModel == null || _boughtItemLocal == null || !_dataPortalUsable)
                return Task.CompletedTask;

            using (var form = new DataPortalPartForm(
                ReadBoughtItemFromUi(),
                textBoxBoughtItemOrderCode1.Text.Trim(),
                hits != null && hits.Count > 0 ? hits : null,
                id => ResolveManufacturerLabel(id)
                    ?? (id.HasValue ? "#" + id.Value.ToString(CultureInfo.InvariantCulture) : null),
                ids => string.Join(
                    "; ",
                    ResolveArticleGroupLabels(ids.ToList())
                        ?? ids.Select(id => "#" + id.ToString(CultureInfo.InvariantCulture)).ToList())))
            {
                var dialogResult = form.ShowDialog(this);
                EnsureUiSynchronizationContext();
                if (dialogResult != DialogResult.OK || form.Result == null)
                    return Task.CompletedTask;

                var taken = form.Result;
                if (taken.ManufacturerName == null)
                    taken.ManufacturerName = ResolveManufacturerLabel(taken.ManufacturerId);
                if (taken.ArticleGroupNames.Count != taken.ArticleGroupIds.Count)
                    taken.ArticleGroupNames =
                        ResolveArticleGroupLabels(taken.ArticleGroupIds) ?? new List<string>();

                PopulateBoughtItemFields(taken, forceOverwrite: true);
            }

            // The taken manufacturer and order code may name a web item waiting to be claimed.
            RunGuarded(CheckPossibleDuplicateFromUiAsync);
            return Task.CompletedTask;
        }

        #endregion

        /// <summary>Runs even when _boughtItemLocal has no PartNumber yet — the possible-duplicate
        /// check (Manufacturer + OrderCode1) doesn't need one, and PDM/SolidWorks may not have assigned
        /// this part a number at the point the user is filling these fields in.</summary>
        private async Task RefreshBoughtItemLookupAsync()
        {
            if (_activeModel == null || _boughtItemLocal == null)
                return;

            if (!_isConnected)
            {
                ClearBoughtItemStatusIndicators();
                return;
            }

            int generation = _loadGeneration;
            var syncService = new BoughtItemSyncService(ApplicationServices.Api.BoughtItems);
            BoughtItemLookupResult lookup;

            try
            {
                lookup = await syncService.LookupAsync(_boughtItemLocal);
            }
            catch (GladosApiException ex)
            {
                if (generation == _loadGeneration)
                {
                    SetStateIndicator(null);
                    SetSyncError();
                    ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorGladosLookupFailedFormat, ex.Message));
                }
                return;
            }
            catch (HttpRequestException)
            {
                if (generation == _loadGeneration)
                {
                    SetStateIndicator(null);
                    SetSyncError();
                    ShowError(Strings.ErrorGladosLookupFailedNoConnection);
                }
                return;
            }

            if (generation != _loadGeneration)
                // A newer document/configuration/source load has since started — discard this
                // stale lookup rather than paint it over whatever's actually active now.
                return;

            HighlightDivergentFields(lookup);
            SetSyncIndicator(lookup.State);
            if (lookup.RemoteItem != null)
                SetStateIndicator(lookup.RemoteItem.Status);
            else
                SetStateIndicatorNone();
            SetPossibleDuplicateTooltip(
                lookup.State == BoughtItemMatchState.PossibleDuplicate ? lookup.RemoteItem : null
            );
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(
                message,
                Strings.MessageBoxTitleGlados,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        private static readonly Color DivergentFieldColor = Color.MistyRose;

        private void HighlightDivergentFields(BoughtItemLookupResult lookup)
        {
            ClearDivergenceHighlights();

            if (lookup.State != BoughtItemMatchState.FoundDiverges)
                return;

            OnUiThread(() =>
            {
                foreach (var field in lookup.DivergentFields)
                {
                    var control = GetControlForField(field);
                    if (control != null)
                        control.BackColor = DivergentFieldColor;
                }
            });
        }

        private void ClearDivergenceHighlights()
        {
            OnUiThread(() =>
            {
                foreach (var control in GetAllBoughtItemFieldControls())
                    control.BackColor = SystemColors.Window;
            });
        }

        private Control GetControlForField(string field)
        {
            switch (field)
            {
                case nameof(BoughtItemProperties.ArticleName):
                    return textBoxBoughtItemArticleName;
                case nameof(BoughtItemProperties.OrderCode1):
                    return textBoxBoughtItemOrderCode1;
                case nameof(BoughtItemProperties.OrderCode2):
                    return textBoxBoughtItemOrderCode2;
                case nameof(BoughtItemProperties.Gtin):
                    return textBoxBoughtItemGTIN;
                case nameof(BoughtItemProperties.Ean):
                    return textBoxBoughtItemEAN;
                case nameof(BoughtItemProperties.TariffNumber):
                    return textBoxBoughtItemTarifNumber;
                case nameof(BoughtItemProperties.Note):
                    return textBoxBoughtItemNote;
                case nameof(BoughtItemProperties.Weblink):
                    return textBoxBoughtItemWeblink;
                case nameof(BoughtItemProperties.ManufacturerId):
                    return comboBoxBoughtItemManufacturer;
                case nameof(BoughtItemProperties.ArticleGroupIds):
                    return checkedListBoxBoughtItemArticleGroups;
                default:
                    return null;
            }
        }

        private IEnumerable<Control> GetAllBoughtItemFieldControls()
        {
            yield return textBoxBoughtItemArticleName;
            yield return textBoxBoughtItemOrderCode1;
            yield return textBoxBoughtItemOrderCode2;
            yield return textBoxBoughtItemGTIN;
            yield return textBoxBoughtItemEAN;
            yield return textBoxBoughtItemTarifNumber;
            yield return textBoxBoughtItemNote;
            yield return textBoxBoughtItemWeblink;
            yield return comboBoxBoughtItemManufacturer;
            yield return checkedListBoxBoughtItemArticleGroups;
        }

        #endregion

        #region Made item

        /// <summary>Reads Glados.Processes (tolerantly - see MadeItemProperties.ProcessesUnreadable),
        /// populates every Made Item field, and re-checks Glados. Called once ApplySourceGateAsync
        /// has already enabled the Made tab. Mirrors LoadBoughtItemAsync.</summary>
        private async Task LoadMadeItemAsync(ModelDoc2 model)
        {
            var local = SwCustomPropertyService.ReadMadeItem(model, ApplicationServices.FieldMapping);
            ResolveStepMethodNames(local.Processes);
            _madeItemLocal = local;

            PopulateMadeItemFields(_madeItemLocal);

            await RefreshMadeItemLookupAsync();
        }

        /// <summary>Resolves ProcessStep.MethodName from the current lookup snapshot's
        /// fabrication/inspection method lists (every row, including archived — see
        /// docs/FIELD_OWNERSHIP.md) for steps that were just read back from Glados.Processes -
        /// the encoding stores ids only (plan section 3.2), so a freshly-read step never has a
        /// label until this runs. Leaves MethodName null (chips/list fall back to
        /// "#&lt;id&gt;") when the id isn't in the snapshot at all - offline with an empty or
        /// stale cache.</summary>
        private void ResolveStepMethodNames(IEnumerable<ProcessStep> steps)
        {
            var snapshot = ApplicationServices.Lookups.Current;

            foreach (var step in steps ?? Enumerable.Empty<ProcessStep>())
            {
                step.MethodName = step.Role == StepRole.Fabrication
                    ? step.FabricationMethodId.HasValue
                        ? snapshot.FabricationMethods.FirstOrDefault(m => m.Id == step.FabricationMethodId.Value)?.Name
                        : null
                    : step.InspectionMethodId.HasValue
                        ? snapshot.InspectionMethods.FirstOrDefault(m => m.Id == step.InspectionMethodId.Value)?.Name
                        : null;
            }
        }

        /// <summary>Independent copies of each step (including MethodName, which
        /// NormalizeProcessGroups also preserves but plain assignment/AsList wouldn't clone) - used
        /// whenever _madeItemSteps is (re)seeded from a baseline (_madeItemLocal.Processes), so
        /// editing the working list in the step editor / grouping control never mutates the
        /// baseline objects that the dirty markers compare against.</summary>
        private static List<ProcessStep> CloneSteps(IEnumerable<ProcessStep> steps)
        {
            return (steps ?? Enumerable.Empty<ProcessStep>()).Select(step => step.Clone()).ToList();
        }

        #region Made item - State/Sync indicators

        private void ClearMadeItemStatusIndicators()
        {
            OnUiThread(() =>
            {
                SetIndicator(
                    labelMadeItemStateMdi,
                    labelMadeItemState,
                    string.Empty,
                    string.Empty,
                    SystemColors.ControlText
                );
                SetIndicator(
                    labelMadeItemSyncMdi,
                    labelMadeItemSync,
                    string.Empty,
                    string.Empty,
                    SystemColors.ControlText
                );
            });
            SetPdmValidationNotChecked(
                labelMadeItemPdmValidationMdi, labelMadeItemPdmValidation, _madeItemPdmValidationTooltip);
        }

        /// <summary>Mirrors SetStateIndicator - MadeItemCatalogueStatus has the same
        /// active/inactive/replaced members as BoughtItemCatalogueStatus, so the same
        /// Strings.State* text and color constants apply unchanged.</summary>
        private void SetMadeItemStateIndicator(MadeItemCatalogueStatus? status)
        {
            OnUiThread(() =>
            {
                switch (status)
                {
                    case MadeItemCatalogueStatus.Active:
                        SetIndicator(
                            labelMadeItemStateMdi,
                            labelMadeItemState,
                            MaterialIcons.CheckCircle,
                            Strings.StateActive,
                            StateColorActive
                        );
                        break;
                    case MadeItemCatalogueStatus.Inactive:
                        SetIndicator(
                            labelMadeItemStateMdi,
                            labelMadeItemState,
                            MaterialIcons.Block,
                            Strings.StateInactive,
                            StateColorInactive
                        );
                        break;
                    case MadeItemCatalogueStatus.Replaced:
                        SetIndicator(
                            labelMadeItemStateMdi,
                            labelMadeItemState,
                            MaterialIcons.Autorenew,
                            Strings.StateReplaced,
                            StateColorReplaced
                        );
                        break;
                    default:
                        SetIndicator(
                            labelMadeItemStateMdi,
                            labelMadeItemState,
                            string.Empty,
                            string.Empty,
                            SystemColors.ControlText
                        );
                        break;
                }
            });
        }

        /// <summary>Mirrors SetStateIndicatorNone - dimmed "no catalogue item" for a completed
        /// lookup that matched nothing (MadeItemMatchState.NoPartNumber or NotFound, i.e.
        /// RemoteItem == null). Passing null to SetMadeItemStateIndicator still means "clear
        /// to blank" for ClearMadeItemStatusIndicators and the error paths below.</summary>
        private void SetMadeItemStateIndicatorNone()
        {
            OnUiThread(() =>
                SetIndicator(
                    labelMadeItemStateMdi,
                    labelMadeItemState,
                    MaterialIcons.Cloud,
                    Strings.StateNoCatalogueItem,
                    StateColorNeutral
                )
            );
        }

        /// <summary>Mirrors SetSyncIndicator - MadeItemMatchState has no PossibleDuplicate tier
        /// (made items have no adoption match key, see MadeItemLookupResult's doc comment), so
        /// this switch has one fewer case and no duplicate-tooltip branch.</summary>
        private void SetMadeItemSyncIndicator(MadeItemMatchState? state)
        {
            OnUiThread(() =>
            {
                switch (state)
                {
                    case MadeItemMatchState.NoPartNumber:
                        SetIndicator(
                            labelMadeItemSyncMdi,
                            labelMadeItemSync,
                            MaterialIcons.CloudOff,
                            Strings.SyncNoPartNumber,
                            SyncColorNeutral
                        );
                        break;
                    case MadeItemMatchState.FoundInSync:
                        SetIndicator(
                            labelMadeItemSyncMdi,
                            labelMadeItemSync,
                            MaterialIcons.CloudDone,
                            Strings.SyncInSync,
                            SyncColorInSync
                        );
                        break;
                    case MadeItemMatchState.FoundDiverges:
                        SetIndicator(
                            labelMadeItemSyncMdi,
                            labelMadeItemSync,
                            MaterialIcons.ErrorOutline,
                            Strings.SyncNotCheckedIn,
                            SyncColorPending
                        );
                        break;
                    case MadeItemMatchState.NotFound:
                        SetIndicator(
                            labelMadeItemSyncMdi,
                            labelMadeItemSync,
                            MaterialIcons.CloudUpload,
                            Strings.SyncNotInGlados,
                            SyncColorNotFound
                        );
                        break;
                    default:
                        SetIndicator(
                            labelMadeItemSyncMdi,
                            labelMadeItemSync,
                            string.Empty,
                            string.Empty,
                            SystemColors.ControlText
                        );
                        break;
                }
            });
        }

        private void SetMadeItemSyncError()
        {
            OnUiThread(() =>
                SetIndicator(
                    labelMadeItemSyncMdi,
                    labelMadeItemSync,
                    MaterialIcons.ErrorOutline,
                    Strings.SyncLookupFailed,
                    SyncColorError
                )
            );
        }

        #endregion

        private async Task RefreshMadeItemLookupAsync()
        {
            if (_activeModel == null || _madeItemLocal == null)
                return;

            if (!_isConnected)
            {
                ClearMadeItemStatusIndicators();
                return;
            }

            int generation = _loadGeneration;
            var syncService = new MadeItemSyncService(ApplicationServices.Api.MadeItems);
            MadeItemLookupResult lookup;

            try
            {
                lookup = await syncService.LookupAsync(_madeItemLocal);
            }
            catch (GladosApiException ex)
            {
                if (generation == _loadGeneration)
                {
                    SetMadeItemStateIndicator(null);
                    SetMadeItemSyncError();
                    ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorGladosLookupFailedFormat, ex.Message));
                }
                return;
            }
            catch (HttpRequestException)
            {
                if (generation == _loadGeneration)
                {
                    SetMadeItemStateIndicator(null);
                    SetMadeItemSyncError();
                    ShowError(Strings.ErrorGladosLookupFailedNoConnection);
                }
                return;
            }

            if (generation != _loadGeneration)
                // A newer document/configuration/source load has since started - discard this
                // stale lookup rather than paint it over whatever's actually active now.
                return;

            SetMadeItemSyncIndicator(lookup.State);
            if (lookup.RemoteItem != null)
                SetMadeItemStateIndicator(lookup.RemoteItem.Status);
            else
                SetMadeItemStateIndicatorNone();
        }

        /// <summary>forceOverwrite mirrors PopulateBoughtItemFields - used for Load from Glados,
        /// an explicit pull of authoritative server data that should always show what the server
        /// actually has, and also works around SolidWorks task panes sometimes reporting a
        /// control as still Focused after the panel itself has lost activation.</summary>
        private void PopulateMadeItemFields(MadeItemProperties local, bool forceOverwrite = false)
        {
            // A reload the property watch started may overwrite a focused control too - see
            // _overwriteFocusedOnPopulate.
            forceOverwrite = forceOverwrite || _overwriteFocusedOnPopulate;
            OnUiThread(() =>
            {
                SetTextIfNotFocused(textBoxMadeItemArticleName, local.ArticleName, forceOverwrite);
                SetTextIfNotFocused(textBoxMadeItemNote, local.Note, forceOverwrite);
                SetTextIfNotFocused(textBoxMadeItemMaterial, local.Material, forceOverwrite);
                SetTextIfNotFocused(textBoxMadeItemMass, FormatMassText(local.Mass), forceOverwrite);

                if (forceOverwrite || !checkedListBoxMadeItemArticleGroups.Focused)
                    SetMadeItemArticleGroupSelection(local.ArticleGroupIds, local.ArticleGroupNames);

                _madeItemSourceItemId = local.SourceItemId;
                _madeItemSourceItemName = local.SourceItemName;
                UpdateSourceItemDisplay();

                PopulateCustomerFields(local, forceOverwrite);

                _madeItemSteps = CloneSteps(local.Processes);
                processRoutingControl1.SetMethodChoices(PickableFabricationMethods(), PickableInspectionMethods());
                processRoutingControl1.SetSteps(_madeItemSteps);

                labelMadeItemProcessesUnreadableHint.Visible = local.ProcessesUnreadable;

                UpdateMadeItemDirtyMarkers();
                UpdateMadeItemCalcButtons();
            });
        }

        /// <summary>Disables the Made tab and blanks every field on it — the Made counterpart of
        /// ClearBoughtItemFields. Goes through PopulateMadeItemFields with an empty
        /// MadeItemProperties rather than resetting each control by hand, so the routing, source
        /// item display, dirty markers and calc buttons are reset by the same code that fills
        /// them. _madeItemLocal is cleared and the tab disabled first so the dirty markers compare
        /// against nothing and the calc buttons see an inactive tab.</summary>
        private void ClearMadeItemFields()
        {
            _madeItemLocal = null;
            _customerItemRequestedFor = null;
            OnUiThread(() => tabPageMade.Enabled = false);
            ClearMadeItemStatusIndicators();
            PopulateMadeItemFields(new MadeItemProperties(), forceOverwrite: true);
        }

        /// <summary>Enables buttonMadeItemMassCalc/buttonMadeItemMaterialCalc only when there is
        /// something for them to do: the made tab itself enabled (ServerKnown — these are editing
        /// actions like Save/Reload, so they follow the tab's own enable state rather than
        /// _isConnected/Online, see ApplyConnectionState's class doc comment), a document active,
        /// the button's field mapped under the current field mapping, and
        /// SwCustomPropertyService.IsCalculated saying the document-level property is currently a
        /// plain value rather than a live SolidWorks link — restoring a link that's already
        /// intact would be a no-op. Called whenever the made tab is (re)populated
        /// (PopulateMadeItemFields, which Save and Reload both reach via ReloadAllForModel) so the
        /// buttons always reflect what is actually on the document rather than a stale guess, and
        /// once from the constructor for the correct initial disabled state before any document
        /// has loaded. Reads the raw property value through the service (IsCalculated), never off
        /// a UI control — the Mass/Material text boxes hold the *resolved* value, which can't
        /// tell a link from a plain value that happens to equal the calculated one.</summary>
        private void UpdateMadeItemCalcButtons()
        {
            var mapping = ApplicationServices.FieldMapping;
            var model = _activeModel;
            bool tabActive = tabPageMade.Enabled && model != null;

            UpdateMadeItemCalcButton(
                buttonMadeItemMassCalc,
                model,
                tabActive ? mapping.ResolvePropertyName(ItemKind.Made, ItemField.Mass) : null,
                Strings.TooltipCalcRestoreMass);

            UpdateMadeItemCalcButton(
                buttonMadeItemMaterialCalc,
                model,
                tabActive ? mapping.ResolvePropertyName(ItemKind.Made, ItemField.Material) : null,
                Strings.TooltipCalcRestoreMaterial);
        }

        /// <summary>One button's half of UpdateMadeItemCalcButtons. propertyName is already null
        /// here whenever the tab-active/document-active preconditions aren't met (see the caller),
        /// so this only has to decide between "mapped and already calculated" (disabled, "already
        /// calculated" tooltip), "mapped and overridden" (enabled, restore tooltip), and "unmapped
        /// or nothing to check against" (disabled, no tooltip).</summary>
        private void UpdateMadeItemCalcButton(Button button, ModelDoc2 model, string propertyName, string restoreTooltip)
        {
            bool calculated = propertyName != null && SwCustomPropertyService.IsCalculated(model, propertyName);
            bool enabled = propertyName != null && !calculated;

            button.Enabled = enabled;
            _madeItemButtonTooltip.SetToolTip(
                button,
                enabled ? restoreTooltip : calculated ? Strings.TooltipCalcAlreadyCalculated : string.Empty);
        }

        /// <summary>Same reasoning as SetArticleGroupSelection (bought), against
        /// checkedListBoxMadeItemArticleGroups - kept as its own method since it targets a
        /// different control, but ResolveArticleGroupLabels is shared as-is: it only ever reads
        /// the (kind-independent) current lookup snapshot's ArticleGroups.</summary>
        private void SetMadeItemArticleGroupSelection(List<int> articleGroupIds, List<string> articleGroupNames)
        {
            var wanted = articleGroupIds ?? new List<int>();

            var visible = VisibleRows(
                ApplicationServices.Lookups.Current.ArticleGroups,
                g => g.Archived, g => g.Id, g => g.Name, wanted, g => g.SortOrder);

            checkedListBoxMadeItemArticleGroups.Items.Clear();
            foreach (var group in visible)
                checkedListBoxMadeItemArticleGroups.Items.Add(group);

            for (int i = 0; i < wanted.Count; i++)
            {
                if (visible.Any(g => g.Id == wanted[i]))
                    continue;

                var storedName =
                    articleGroupNames != null && i < articleGroupNames.Count
                        ? articleGroupNames[i]
                        : null;

                checkedListBoxMadeItemArticleGroups.Items.Add(
                    new ArticleGroupResponse
                    {
                        Id = wanted[i],
                        Name = PlaceholderLabel(storedName, wanted[i]),
                    }
                );
            }

            var ids = new HashSet<int>(wanted);
            for (int i = 0; i < checkedListBoxMadeItemArticleGroups.Items.Count; i++)
            {
                var group = (ArticleGroupResponse)checkedListBoxMadeItemArticleGroups.Items[i];
                checkedListBoxMadeItemArticleGroups.SetItemChecked(i, ids.Contains(group.Id));
            }
        }

        private List<ArticleGroupResponse> GetCheckedMadeItemArticleGroups()
        {
            var groups = new List<ArticleGroupResponse>();
            for (int i = 0; i < checkedListBoxMadeItemArticleGroups.Items.Count; i++)
            {
                if (checkedListBoxMadeItemArticleGroups.GetItemChecked(i))
                    groups.Add((ArticleGroupResponse)checkedListBoxMadeItemArticleGroups.Items[i]);
            }
            return groups;
        }

        private List<int> GetCheckedMadeItemArticleGroupIds()
        {
            return GetCheckedMadeItemArticleGroups().Select(g => g.Id).ToList();
        }

        #region Made item - source item picker

        /// <summary>Repaints textBoxMadeItemSourceItem and the Clear button's enabled state from
        /// the current working selection (_madeItemSourceItemId/_madeItemSourceItemName) —
        /// never re-resolves against the lookup snapshot itself, so a spec that's since become
        /// archived or unresolvable still shows its stored label rather than blanking (README's
        /// derived-property rules). The Select button is left alone here: it stays enabled
        /// whenever the made tab itself is enabled — picking works from the offline snapshot,
        /// so it doesn't depend on _isConnected the way buttonMadeItemLoadFromGlados does.
        /// </summary>
        private void UpdateSourceItemDisplay()
        {
            OnUiThread(() =>
            {
                textBoxMadeItemSourceItem.Text = _madeItemSourceItemId.HasValue
                    ? PlaceholderLabel(_madeItemSourceItemName, _madeItemSourceItemId.Value)
                    : string.Empty;
                buttonMadeItemSourceItemClear.Enabled = _madeItemSourceItemId.HasValue;
            });
        }

        /// <summary>Empty string when there's no source item (Clear), the composed spec label
        /// (SourceItemLabelComposer) when the current lookup snapshot can resolve sourceItemId
        /// (checked against every row, including archived — see docs/FIELD_OWNERSHIP.md), null
        /// when it can't — mirrors ResolveManufacturerLabel; see WriteSourceItem for what null
        /// means (leave the stored label untouched).</summary>
        private string ResolveSourceItemLabel(int? sourceItemId)
        {
            if (sourceItemId == null)
                return string.Empty;

            var item = ApplicationServices.Lookups.Current.SourceItems
                .FirstOrDefault(s => s.Id == sourceItemId.Value);
            return item != null ? SourceItemLabelComposer.Compose(item) : null;
        }

        private void buttonMadeItemSourceItemSelect_Click(object sender, EventArgs e)
        {
            RunGuarded(SelectSourceItemAsync);
        }

        /// <summary>Opens SourceItemPickerForm modally (the "never writes to Glados" invariant's
        /// one documented exception lives inside the picker's own Create panel, not here — see
        /// README's "The invariant"). Not actually async — ShowDialog blocks synchronously like
        /// every other modal in this add-in (OpenSettingsAsync) — kept as a Task-returning method
        /// only so buttonMadeItemSourceItemSelect_Click can route it through RunGuarded like every
        /// other click handler.</summary>
        private Task SelectSourceItemAsync()
        {
            if (_madeItemLocal == null)
                return Task.CompletedTask;

            using (var picker = new SourceItemPickerForm(
                _isConnected, _madeItemSourceItemId, _madeItemLocal.Material, _madeItemLocal.BaseSize))
            {
                var result = picker.ShowDialog(this);
                EnsureUiSynchronizationContext();
                if (result == DialogResult.OK && picker.SelectedSourceItem != null)
                {
                    _madeItemSourceItemId = picker.SelectedSourceItem.Id;
                    _madeItemSourceItemName = SourceItemLabelComposer.Compose(picker.SelectedSourceItem);
                    UpdateSourceItemDisplay();
                    UpdateMadeItemDirtyMarkers();
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>Clears both the id and the label together, mirroring how an explicit Clear
        /// also blanks the resolved label at Save (see ResolveSourceItemLabel) — plain
        /// synchronous UI state, so unlike Select this doesn't go through RunGuarded.</summary>
        private void buttonMadeItemSourceItemClear_Click(object sender, EventArgs e)
        {
            _madeItemSourceItemId = null;
            _madeItemSourceItemName = string.Empty;
            UpdateSourceItemDisplay();
            UpdateMadeItemDirtyMarkers();
        }

        #endregion

        #region Made item - unsaved-change markers

        private void WireMadeItemDirtyTracking()
        {
            CaptureBaseLabelText(labelMadeItemArticleName);
            CaptureBaseLabelText(labelMadeItemGroups);
            CaptureBaseLabelText(labelMadeItemNote);
            CaptureBaseLabelText(labelMadeItemSourceItem);
            CaptureBaseLabelText(labelMadeItemMaterial);
            CaptureBaseLabelText(labelMadeItemMass);

            textBoxMadeItemArticleName.TextChanged += (s, e) => UpdateMadeItemDirtyMarkers();
            textBoxMadeItemNote.TextChanged += (s, e) => UpdateMadeItemDirtyMarkers();
            textBoxMadeItemMaterial.TextChanged += (s, e) => UpdateMadeItemDirtyMarkers();
            textBoxMadeItemMass.TextChanged += (s, e) => UpdateMadeItemDirtyMarkers();
            checkedListBoxMadeItemArticleGroups.ItemCheck += (s, e) =>
                BeginInvoke(new Action(UpdateMadeItemDirtyMarkers));

            processRoutingControl1.RoutingChanged += (s, e) => UpdateMadeItemDirtyMarkers();
        }

        private void UpdateMadeItemDirtyMarkers()
        {
            var saved = _madeItemLocal;

            SetDirtyMarker(
                labelMadeItemArticleName,
                !TextMatches(textBoxMadeItemArticleName.Text, saved?.ArticleName)
            );
            SetDirtyMarker(
                labelMadeItemNote,
                !TextMatches(textBoxMadeItemNote.Text, saved?.Note)
            );
            SetDirtyMarker(
                labelMadeItemMaterial,
                !TextMatches(textBoxMadeItemMaterial.Text, saved?.Material)
            );
            // Compared as parsed grams, not raw text: "1000" and "1000.0" both parse to the
            // same value WriteMass would decide is unchanged (see SaveMadeItemPropertiesAsync),
            // so the marker agrees with what Save actually does and clears itself afterwards
            // regardless of exactly how the user formatted the number.
            SetDirtyMarker(
                labelMadeItemMass,
                SwCustomPropertyService.ParseMassGrams(textBoxMadeItemMass.Text) != saved?.Mass
            );

            var currentGroupIds = new HashSet<int>(GetCheckedMadeItemArticleGroupIds());
            var savedGroupIds = new HashSet<int>(saved?.ArticleGroupIds ?? new List<int>());
            SetDirtyMarker(labelMadeItemGroups, !currentGroupIds.SetEquals(savedGroupIds));

            SetDirtyMarker(labelMadeItemSourceItem, _madeItemSourceItemId != saved?.SourceItemId);

            UpdateMadeItemRequiredMarkers();
            UpdateCustomerDirtyMarkers();
        }

        /// <summary>Marks every made-item field the pane exposes an edit control for that the
        /// current field mapping says is required and whose UI value is currently empty, plus
        /// the routing area for the fabrication_step pseudo-rule (docs/FIELD_OWNERSHIP.md
        /// "required fields") — satisfied once _madeItemSteps has a fabrication step with a
        /// method assigned (RequiredFieldRuleEvaluator.HasFabricationStep). `base_size` and
        /// `weblink` have no edit control on this tab at all (see ReadMadeItemFromUi's doc
        /// comment) and so can never be marked even if the admin ticks one of them required —
        /// the same "nothing to mark" gap UpdateBoughtItemRequiredMarkers doesn't have, since
        /// every AddIn-owned bought field does have a control.</summary>
        private void UpdateMadeItemRequiredMarkers()
        {
            const ItemKind kind = ItemKind.Made;
            var mapping = ApplicationServices.FieldMapping;

            UpdateIdentityRequiredMarkers(kind);

            SetRequiredMarker(
                labelMadeItemArticleName,
                mapping.IsRequired(kind, ItemField.ArticleName) && IsBlank(textBoxMadeItemArticleName.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelMadeItemNote,
                mapping.IsRequired(kind, ItemField.Note) && IsBlank(textBoxMadeItemNote.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelMadeItemMaterial,
                mapping.IsRequired(kind, ItemField.Material) && IsBlank(textBoxMadeItemMaterial.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelMadeItemMass,
                mapping.IsRequired(kind, ItemField.Mass) && IsBlank(textBoxMadeItemMass.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelMadeItemGroups,
                mapping.IsRequired(kind, ItemField.ArticleGroupIds) && GetCheckedMadeItemArticleGroupIds().Count == 0,
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelMadeItemSourceItem,
                mapping.IsRequired(kind, ItemField.SourceItemId) && !_madeItemSourceItemId.HasValue,
                Strings.TooltipRequiredField);

            var fabricationStepRequired = mapping.IsRuleRequired(kind, RequiredFieldRuleName.FabricationStep);
            SetRequiredMarker(
                labelMadeItemStepsTitle,
                fabricationStepRequired && !RequiredFieldRuleEvaluator.HasFabricationStep(_madeItemSteps),
                Strings.TooltipRequiredFabricationStep);
        }

        #endregion

        /// <summary>Builds a MadeItemProperties from whatever is currently shown in the UI
        /// (i.e. possibly-unsaved edits), for the Save button to write out. PartNumber/Revision
        /// are PDM-owned and read-only (the shared textBoxPartNumber/textBoxRevision above the
        /// tab strip, never edited here); BaseSize/Weblink have no edit control on this tab at
        /// all. All four are always carried through unchanged from the last-loaded baseline.
        /// Material and Mass do have controls: Material is read verbatim like ArticleName/Note,
        /// Mass is parsed from free text via SwCustomPropertyService.ParseMassGrams (trimmed,
        /// comma or dot) — for both, SaveMadeItemPropertiesAsync's WriteMaterial/WriteMass calls
        /// compare the result against the value loaded from the document (baseline) to decide
        /// whether the user overrode a template link (see docs/FIELD_OWNERSHIP.md / README
        /// "Write scopes and timing").
        /// </summary>
        private MadeItemProperties ReadMadeItemFromUi()
        {
            var articleGroupIds = GetCheckedMadeItemArticleGroupIds();

            return new MadeItemProperties
            {
                PartNumber = _madeItemLocal?.PartNumber,
                Revision = _madeItemLocal?.Revision,
                ArticleName = textBoxMadeItemArticleName.Text,
                Material = textBoxMadeItemMaterial.Text,
                Mass = SwCustomPropertyService.ParseMassGrams(textBoxMadeItemMass.Text?.Trim()),
                BaseSize = _madeItemLocal?.BaseSize,
                Note = textBoxMadeItemNote.Text,
                Weblink = _madeItemLocal?.Weblink,
                ArticleGroupIds = articleGroupIds,
                ArticleGroupNames =
                    ResolveArticleGroupLabels(articleGroupIds)
                    ?? _madeItemLocal?.ArticleGroupNames
                    ?? new List<string>(),
                SourceItemId = _madeItemSourceItemId,
                // Mirrors ManufacturerName's ReadBoughtItemFromUi comment: a resolved label, or
                // the one already stored when the lookup can't resolve the id — never the "#id"
                // placeholder the display text box may currently be showing.
                SourceItemName =
                    ResolveSourceItemLabel(_madeItemSourceItemId) ?? _madeItemLocal?.SourceItemName,
                // Already fully normalized - processRoutingControl1 commits every structural
                // edit (including GroupIndex) onto this same list synchronously as it happens,
                // unlike the old grouping control's separate pending-toggle/ApplyGroupIndexes
                // step.
                Processes = _madeItemSteps,
                ProcessesUnreadable = false,
                ProcessesStored = false,
                ProcessNames = _madeItemLocal?.ProcessNames,
                // The customer fields, see GladosTaskPaneControl.Customer.cs. Same label
                // rule as SourceItemName above.
                CustomerId = SelectedCustomerId,
                CustomerName = ResolveCustomerLabel(SelectedCustomerId) ?? _madeItemLocal?.CustomerName,
                CustomerPartNumber = textBoxCustomerPartNumber.Text,
                CustomerRevision = textBoxCustomerRevision.Text,
            };
        }

        /// <summary>Maps a Glados catalogue item onto the UI-editable fields. PartNumber/Revision
        /// are PDM-controlled and always kept from the current local snapshot, never from Glados -
        /// mirrors the bought-item MapFromRemote.</summary>
        private MadeItemProperties MapFromRemote(MadeItemResponse remote)
        {
            return new MadeItemProperties
            {
                PartNumber = _madeItemLocal?.PartNumber,
                Revision = _madeItemLocal?.Revision,
                ArticleName = remote.ArticleName,
                Material = remote.Material,
                Mass = remote.Mass,
                BaseSize = remote.BaseSize,
                Note = remote.Note,
                Weblink = remote.Weblink,
                ArticleGroupIds = (remote.ArticleGroups ?? new List<ArticleGroupResponse>())
                    .Select(g => g.Id)
                    .ToList(),
                ArticleGroupNames = (remote.ArticleGroups ?? new List<ArticleGroupResponse>())
                    .Select(g => g.Name)
                    .ToList(),
                // MadeItemResponse carries no resolved source-item label of its own (unlike
                // ArticleGroups, which nests its Name) — resolve one from the current lookup
                // snapshot the same way Save does, so a Load-from-Glados preview doesn't show a
                // bare "#id" for a spec the snapshot can actually name.
                SourceItemId = remote.SourceItemId,
                SourceItemName = ResolveSourceItemLabel(remote.SourceItemId),
                Processes = MapFromRemoteProcesses(remote.Processes),
                ProcessesUnreadable = false,
                ProcessesStored = false,
                ProcessNames = null,
                CustomerId = remote.CustomerId,
                CustomerName = remote.CustomerName,
                CustomerPartNumber = remote.CustomerPartNumber,
                CustomerRevision = remote.CustomerRevision,
            };
        }

        /// <summary>Steps in position order, with MethodName taken from the response's own
        /// resolved *_method_name (authoritative and can't be stale, unlike the local lookup
        /// cache) - see ProcessStep.MethodName's doc comment on "or the remote response on
        /// Load-from-Glados".</summary>
        private static List<ProcessStep> MapFromRemoteProcesses(IEnumerable<MadeItemProcessResponse> remote)
        {
            return (remote ?? Enumerable.Empty<MadeItemProcessResponse>())
                .OrderBy(process => process.Position)
                .Select(process => new ProcessStep
                {
                    Role = process.Role,
                    FabricationMethodId = process.FabricationMethodId,
                    InspectionMethodId = process.InspectionMethodId,
                    GroupIndex = process.GroupIndex,
                    EstimatedSetupMinutes = process.EstimatedSetupMinutes,
                    EstimatedMinutesPerPiece = process.EstimatedMinutesPerPiece,
                    Note = process.Note,
                    MethodName = process.Role == StepRole.Fabrication
                        ? process.FabricationMethodName
                        : process.InspectionMethodName,
                })
                .ToList();
        }

        #region Made item - routing method choices

        /// <summary>The current lookup snapshot's non-archived fabrication methods, in their
        /// Glados order - what a new step (or a step switching role) may default its method to. Reads
        /// live off ApplicationServices.Lookups.Current rather than caching, since the snapshot
        /// can change underneath the task pane (background refresh, Reload).</summary>
        private List<FabricationMethodResponse> PickableFabricationMethods()
        {
            return VisibleRows(
                ApplicationServices.Lookups.Current.FabricationMethods,
                m => m.Archived, m => m.Id, m => m.Name, null, m => m.SortOrder);
        }

        /// <summary>Same as PickableFabricationMethods, for inspection methods.</summary>
        private List<InspectionMethodResponse> PickableInspectionMethods()
        {
            return VisibleRows(
                ApplicationServices.Lookups.Current.InspectionMethods,
                m => m.Archived, m => m.Id, m => m.Name, null, m => m.SortOrder);
        }

        #endregion

        private void buttonMadeItemLoadFromGlados_Click(object sender, EventArgs e)
        {
            RunGuarded(LoadMadeItemFromGladosAsync);
        }

        private async Task LoadMadeItemFromGladosAsync()
        {
            if (_activeModel == null || _madeItemLocal == null || !_madeItemLocal.HasPartNumber)
                return;

            buttonMadeItemLoadFromGlados.Enabled = false;

            try
            {
                var remote = await ApplicationServices.Api.MadeItems.GetMadeItemByPartNumberAsync(
                    _madeItemLocal.PartNumber
                );

                // Populates the UI only - _madeItemLocal (the last-saved-to-document baseline)
                // is untouched, so Reload From File still reverts to what's actually on disk
                // rather than to these unsaved, just-loaded values. forceOverwrite: true because
                // this is an explicit pull of authoritative server data.
                PopulateMadeItemFields(MapFromRemote(remote), forceOverwrite: true);
            }
            catch (GladosApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Not in Glados yet - not an error, nothing to load.
            }
            catch (GladosApiException ex)
            {
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorLoadFailedFormat, ex.Message));
            }
            catch (HttpRequestException)
            {
                ShowError(Strings.ErrorLoadFailedNoConnection);
            }
            finally
            {
                // Re-derives Enabled from the current facts rather than hardcoding true — if
                // the connection dropped while this call was in flight, it must stay disabled.
                ApplyConnectionState();
            }
        }

        private void buttonMadeItemSaveProperties_Click(object sender, EventArgs e)
        {
            RunGuarded(() => WritingProperties(SaveMadeItemPropertiesAsync));
        }

        private async Task SaveMadeItemPropertiesAsync()
        {
            if (_activeModel == null || _madeItemLocal == null)
                return;

            var model = _activeModel;
            var fieldMapping = ApplicationServices.FieldMapping;
            var failures = new List<string>();

            // No separate "commit the pending grouping edit" step needed here - unlike the old
            // ProcessGroupingControl, processRoutingControl1 normalizes and writes GroupIndex
            // onto _madeItemSteps synchronously on every edit (see its class doc comment), so
            // _madeItemSteps is already current.
            // What this Save writes: the pane's edits on top of the file as it is now, not as
            // it was loaded — see GladosTaskPaneControl.SaveMerge.cs.
            MadeItemProperties edited;
            if (!TryMergeMadeSave(model, fieldMapping, _madeItemLocal, ReadMadeItemFromUi(), assembly: false, out edited))
                return;

            try
            {
                void WriteField(string label, ItemField field, string value)
                {
                    // Unmapped = never written - a field the current server mapping doesn't
                    // cover is left alone rather than guessed at. See docs/FIELD_OWNERSHIP.md
                    // section 2.
                    var propertyName = fieldMapping.ResolvePropertyName(ItemKind.Made, field);
                    if (propertyName == null)
                        return;

                    if (!SwCustomPropertyService.WriteText(model, propertyName, value))
                        failures.Add(label);
                }

                WriteField("Article Name", ItemField.ArticleName, edited.ArticleName);
                WriteField("Note", ItemField.Note, edited.Note);

                // Preserve the template's linked SW-Mass / SW-Material / bounding-box
                // expressions: these three are authored by the add-in only on an explicit user
                // override, detected against the values loaded from the document
                // (_madeItemLocal) — never against the live resolved values, which a rebuild can
                // change between load and Save. See WriteMass / WriteMaterial / WriteBaseSize and
                // README's "Linked expressions on Save".
                var baseline = _madeItemLocal;
                bool TextChanged(string current, string loaded) =>
                    !string.Equals(current ?? string.Empty, loaded ?? string.Empty, StringComparison.Ordinal);

                if (!SwCustomPropertyService.WriteMass(
                        model, fieldMapping, ItemKind.Made, edited.Mass,
                        baseline != null && edited.Mass != baseline.Mass))
                    failures.Add("Mass");
                if (!SwCustomPropertyService.WriteMaterial(
                        model, fieldMapping, edited.Material,
                        baseline != null && TextChanged(edited.Material, baseline.Material)))
                    failures.Add("Material");
                if (!SwCustomPropertyService.WriteBaseSize(
                        model, fieldMapping, edited.BaseSize,
                        baseline != null && TextChanged(edited.BaseSize, baseline.BaseSize)))
                    failures.Add("Base Size");

                if (!SwCustomPropertyService.WriteKind(model, fieldMapping, ItemKind.Made))
                    failures.Add("Kind");

                if (!SwCustomPropertyService.WriteMadeItemArticleGroups(
                        model, fieldMapping, edited.ArticleGroupIds, ResolveArticleGroupLabels(edited.ArticleGroupIds)))
                    failures.Add("Article Groups");

                // Same "resolve fresh at save time, never trust the control" reasoning as
                // Manufacturer/Article Groups above — edited.SourceItemName already carries this
                // same value (ReadMadeItemFromUi), recomputed here rather than reused so the
                // write always reflects the lookup snapshot at the moment of Save.
                if (!SwCustomPropertyService.WriteSourceItem(
                        model, fieldMapping, edited.SourceItemId, ResolveSourceItemLabel(edited.SourceItemId)))
                    failures.Add("Source Item");

                // The customer fields (see GladosTaskPaneControl.Customer.cs). The label is
                // resolved fresh, the same reasoning as the source item above.
                failures.AddRange(SwCustomPropertyService.WriteCustomer(
                    model,
                    fieldMapping,
                    edited.CustomerId,
                    ResolveCustomerLabel(edited.CustomerId),
                    edited.CustomerPartNumber,
                    edited.CustomerRevision));

                // Unreadable here only when the merge took the file's routing, which became
                // unreadable after load: that text is the outside change being kept, so it is
                // left alone rather than overwritten with an empty routing.
                if (!edited.ProcessesUnreadable)
                {
                    List<ProcessStep> normalizedSteps;
                    if (!SwCustomPropertyService.WriteProcesses(model, fieldMapping, edited.Processes, out normalizedSteps))
                        failures.Add("Processes");

                    edited.Processes = normalizedSteps;
                }

                // WriteProcesses just wrote this into the file (WriteTextInScopeOf, anchored on
                // Glados.Processes) - read it back rather than re-deriving the label ourselves,
                // so the baseline stays faithful to what's actually on disk. Left as whatever
                // was already on edited when the label has no resolved property of its own
                // (unmapped process_names) - same "unmapped = never written" reasoning as
                // WriteProcesses itself, so there is nothing on disk to read back here either.
                var processNamesProperty = fieldMapping.ResolvePropertyName(ItemKind.Made, ItemField.ProcessNames);
                if (processNamesProperty != null)
                    edited.ProcessNames = SwCustomPropertyService.Read(model, processNamesProperty);

                if (!SwCustomPropertyService.WriteFeatureTreeRepresentation(model, fieldMapping, ItemKind.Made))
                    failures.Add("Feature Tree Representation");
            }
            catch (InvalidOperationException ex)
            {
                // A structurally invalid grouping (e.g. an inspection sharing a group) never
                // reaches here in normal use - every step-editor mutation and the grouping
                // control's own commit path keep the shared list valid - but WriteProcesses
                // validates again right before writing, so surface it rather than let it escape
                // as an unhandled exception into SolidWorks.
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorFailedToSaveDocumentFormat, ex.Message));
                return;
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Error("Saving made item properties failed", ex);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorFailedToSaveDocumentFormat, ex.Message));
                return;
            }

            if (failures.Count > 0)
                ShowError(
                    string.Format(CultureInfo.CurrentCulture, Strings.ErrorSavedButFailedFormat, string.Join(", ", failures))
                );

            // Re-read the document instead of adopting `edited` as the new baseline: what is on
            // the file after a Save is the truth, and it differs from what was typed — the
            // template's linked mass/material/base-size expressions resolve to fresh values,
            // labels are re-resolved, configurations are mirrored. Same path as Reload From
            // File (configurations, Kind, tab fields, the Glados lookup), so the pane shows the
            // file exactly as SolidWorks now holds it and the dirty markers reset against it.
            await ReloadAllForModel(model);

            // Advisory-only, per README's "Validation before release" — runs after the save
            // above has already fully committed, so a validation failure (unreachable server,
            // disabled integration, whatever) can never affect what Save just did.
            await ValidateMadeItemPdmAsync();
        }

        private void buttonMadeItemReloadProperties_Click(object sender, EventArgs e)
        {
            RunGuarded(ReloadMadeItemPropertiesAsync);
        }

        private async Task ReloadMadeItemPropertiesAsync()
        {
            if (_activeModel == null)
                return;

            await ReloadAllForModel(_activeModel);
        }

        private void buttonMadeItemMassCalc_Click(object sender, EventArgs e)
        {
            RunGuarded(() => WritingProperties(RestoreMadeItemMassAsync));
        }

        /// <summary>Discards whatever plain value is currently in Mass and re-links it to
        /// SolidWorks' own SW-Mass calculation — the made tab's "restore calculated value"
        /// action for Mass. Guarded the same way SaveMadeItemPropertiesAsync/
        /// ReloadMadeItemPropertiesAsync are: _madeItemLocal is only non-null once the made tab
        /// is the document's loaded kind, so this is also this method's "made tab is active"
        /// check. No confirmation dialog — the button is only ever enabled
        /// (UpdateMadeItemCalcButtons) when there is a plain value to discard in the first place.
        /// Ends with the same re-read every Save does (ReloadAllForModel) so the pane shows the
        /// freshly resolved mass and the dirty/Calc-button state reset against it.</summary>
        private async Task RestoreMadeItemMassAsync()
        {
            if (_activeModel == null || _madeItemLocal == null)
                return;

            var model = _activeModel;

            if (!SwCustomPropertyService.RestoreCalculatedMass(model, ApplicationServices.FieldMapping, ItemKind.Made))
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorRestoreFailedFormat, Strings.LabelMassText));

            await ReloadAllForModel(model);
        }

        private void buttonMadeItemMaterialCalc_Click(object sender, EventArgs e)
        {
            RunGuarded(() => WritingProperties(RestoreMadeItemMaterialAsync));
        }

        /// <summary>Material analogue of RestoreMadeItemMassAsync. See that method's doc
        /// comment.</summary>
        private async Task RestoreMadeItemMaterialAsync()
        {
            if (_activeModel == null || _madeItemLocal == null)
                return;

            var model = _activeModel;

            if (!SwCustomPropertyService.RestoreCalculatedMaterial(model, ApplicationServices.FieldMapping))
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorRestoreFailedFormat, Strings.LabelMaterialText));

            await ReloadAllForModel(model);
        }

        #endregion

        #region Assembly item

        /// <summary>Whether <paramref name="model"/> is a SolidWorks assembly (.sldasm) rather
        /// than a part — the document-type check ApplySourceGateAsync uses to route a "made"
        /// document to this tab instead of the Made tab. A made assembly is deliberately not a
        /// Glados catalogue item: the backend's made_item_extensions only lists sldprt, so PDM
        /// ingest already skips a made .sldasm (see glados/docs/PDM_INTEGRATION.md). This tab
        /// is groundwork — it lets an engineer stamp the same two add-in-owned fields a made
        /// part has (ArticleName, Note, both resolved through ItemKind.Made — the Made tab's
        /// own mapping and property names) onto the assembly file, so a future product/BOM
        /// feature has something to build on. It has no "Load from Glados" action and never
        /// runs PDM validation for the same reason: there is no catalogue record to load or
        /// validate against, and validating one would always fail the backend's extension
        /// check.</summary>
        private static bool IsAssemblyDocument(ModelDoc2 model)
        {
            if (model == null)
                return false;

            // ModelDoc2 doesn't redeclare GetType() itself — it inherits IModelDoc2.GetType(),
            // which returns the document type as an int (swDocumentTypes_e), not
            // System.Object.GetType(). Casting through IModelDoc2 makes that resolution
            // explicit rather than relying on the interface's own member-hiding rules.
            return ((IModelDoc2)model).GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
        }

        /// <summary>True for a .slddrw. A drawing is never a Glados item of its own, whatever
        /// Glados.Kind it may carry, so ApplySourceGateAsync routes it to the Drawing tab ahead
        /// of the Source dropdown and disables the item tabs. Casts through IModelDoc2 for the
        /// same reason IsAssemblyDocument does.</summary>
        private static bool IsDrawingDocument(ModelDoc2 model)
        {
            if (model == null)
                return false;

            return ((IModelDoc2)model).GetType() == (int)swDocumentTypes_e.swDocDRAWING;
        }

        /// <summary>GetPathName() returns "" for a document that has never been saved — a new
        /// part, assembly or drawing before its first Save As.</summary>
        private static bool IsUnsavedDocument(ModelDoc2 model)
        {
            return model != null && string.IsNullOrEmpty(model.GetPathName());
        }

        /// <summary>The name of <paramref name="model"/>'s active configuration, or null for a
        /// document that has none (a drawing) or doesn't report one yet.</summary>
        private static string ActiveConfigurationName(ModelDoc2 model)
        {
            var configuration = model?.GetActiveConfiguration() as Configuration;
            return configuration?.Name;
        }

        /// <summary>True when <paramref name="model"/> reports an active configuration other
        /// than the one comboBoxConfiguration shows. A document reporting none (a drawing)
        /// never differs.</summary>
        private bool ActiveConfigurationDiffers(ModelDoc2 model)
        {
            var activeName = ActiveConfigurationName(model);
            return activeName != null
                && !string.Equals(
                    activeName, comboBoxConfiguration.SelectedItem as string, StringComparison.Ordinal);
        }

        /// <summary>Reads the two fields this tab edits straight off the document, populates
        /// the UI, and paints the permanent "not tracked" indicators — no remote lookup, since
        /// a made assembly is never in the Glados catalogue (see IsAssemblyDocument). Not
        /// actually async — kept as a Task-returning method, like SelectSourceItemAsync, only
        /// so ApplySourceGateAsync's "await LoadAssemblyAsync(...)" reads the same as its
        /// Bought/Made counterparts; nothing here crosses an await, so unlike
        /// RefreshMadeItemLookupAsync there is no stale-lookup race for _loadGeneration to
        /// guard against.</summary>
        private Task LoadAssemblyAsync(ModelDoc2 model)
        {
            var local = SwCustomPropertyService.ReadMadeItem(model, ApplicationServices.FieldMapping);
            ResolveStepMethodNames(local.Processes);
            _assemblyLocal = local;

            PopulateAssemblyFields(_assemblyLocal);
            SetAssemblyStatusIndicatorsNeutral();

            return Task.CompletedTask;
        }

        /// <summary>Enables/disables the Assembly tab and, when disabling, blanks its status
        /// indicators — mirrors SetBoughtItemTabEnabled. There is no divergence highlighting to
        /// clear here (no lookup ever runs against this tab), so this is the one difference
        /// from that method's shape.</summary>
        private void SetAssemblyTabEnabled(bool enabled)
        {
            OnUiThread(() => tabPageAssembly.Enabled = enabled);
            if (!enabled)
                ClearAssemblyStatusIndicators();
        }

        /// <summary>Blanks both indicator pairs back to empty — mirrors
        /// ClearBoughtItemStatusIndicators/ClearMadeItemStatusIndicators, minus the
        /// possible-duplicate/PDM-validation pieces neither applies to this tab (see
        /// IsAssemblyDocument). Called whenever the tab is disabled (SetAssemblyTabEnabled) so
        /// a document switch never leaves the "not tracked" explanation showing behind a
        /// grayed-out tab for the wrong document.</summary>
        private void ClearAssemblyStatusIndicators()
        {
            OnUiThread(() =>
            {
                SetIndicator(
                    labelAssemblyStateMdi,
                    labelAssemblyState,
                    string.Empty,
                    string.Empty,
                    SystemColors.ControlText
                );
                SetIndicator(
                    labelAssemblySyncMdi,
                    labelAssemblySync,
                    string.Empty,
                    string.Empty,
                    SystemColors.ControlText
                );
            });
        }

        /// <summary>Paints both indicator pairs to a fixed, neutral "not tracked in Glados"
        /// state — unlike SetMadeItemStateIndicator/SetMadeItemSyncIndicator, this never varies
        /// with a lookup result (there is none; see IsAssemblyDocument), so it is just one
        /// icon/text/color repainted onto both label pairs, with a tooltip on all four labels
        /// spelling out why. Called from LoadAssemblyAsync (every load) and from
        /// ApplyLocalizedStaticText (so a language switch repaints the caption/tooltip too),
        /// mirroring how SetPdmValidationNotChecked is called from both places.</summary>
        private void SetAssemblyStatusIndicatorsNeutral()
        {
            OnUiThread(() =>
            {
                SetIndicator(
                    labelAssemblyStateMdi,
                    labelAssemblyState,
                    MaterialIcons.CloudOff,
                    Strings.AssemblyNotTrackedText,
                    SyncColorNeutral
                );
                SetIndicator(
                    labelAssemblySyncMdi,
                    labelAssemblySync,
                    MaterialIcons.CloudOff,
                    Strings.AssemblyNotTrackedText,
                    SyncColorNeutral
                );

                _assemblyIndicatorTooltip.SetToolTip(labelAssemblyStateMdi, Strings.TooltipAssemblyNotTracked);
                _assemblyIndicatorTooltip.SetToolTip(labelAssemblyState, Strings.TooltipAssemblyNotTracked);
                _assemblyIndicatorTooltip.SetToolTip(labelAssemblySyncMdi, Strings.TooltipAssemblyNotTracked);
                _assemblyIndicatorTooltip.SetToolTip(labelAssemblySync, Strings.TooltipAssemblyNotTracked);
            });
        }

        /// <summary>Binds the assembly tab's editable fields and its routing, seeding the routing
        /// with the backend's default assembly method when the file has none yet (see
        /// AssemblyRoutingDefaults). Mirrors PopulateMadeItemFields'
        /// forceOverwrite contract (via SetTextIfNotFocused) even though nothing on this tab
        /// currently calls it with forceOverwrite: true — there is no "Load from Glados" action
        /// to force a refresh against (see IsAssemblyDocument) — kept for the same signature
        /// shape as its Bought/Made counterparts.</summary>
        private void PopulateAssemblyFields(MadeItemProperties local, bool forceOverwrite = false)
        {
            // A reload the property watch started may overwrite a focused control too - see
            // _overwriteFocusedOnPopulate.
            forceOverwrite = forceOverwrite || _overwriteFocusedOnPopulate;
            OnUiThread(() =>
            {
                SetTextIfNotFocused(textBoxAssemblyName, local.ArticleName, forceOverwrite);
                SetTextIfNotFocused(textBoxAssemblyNote, local.Note, forceOverwrite);

                _assemblySteps = CloneSteps(local.Processes);

                // A made assembly starts from the backend's default assembly method only when
                // its file has never stored a routing: an explicitly saved empty one stays
                // empty, an unreadable one keeps its hint, and a stored routing is never added
                // to. UI only - it reaches the file on Save.
                if (AssemblyRoutingDefaults.ShouldSeed(local))
                {
                    var defaultStep = AssemblyRoutingDefaults.CreateDefaultStep(
                        ApplicationServices.Lookups.Current.FabricationMethods);
                    if (defaultStep != null)
                        _assemblySteps.Add(defaultStep);
                }

                processRoutingControlAssembly.SetMethodChoices(PickableFabricationMethods(), PickableInspectionMethods());
                processRoutingControlAssembly.SetSteps(_assemblySteps);

                labelAssemblyProcessesUnreadableHint.Visible = local.ProcessesUnreadable;

                UpdateAssemblyDirtyMarkers();
            });
        }

        #region Assembly item - unsaved-change markers

        /// <summary>Live-marks each of the tab's two field labels and the routing title with
        /// " *" whenever its current UI value differs from _assemblyLocal, and re-evaluates the
        /// required markers when the routing changes. Mirrors WireMadeItemDirtyTracking, minus
        /// the controls (article groups, source item) this tab doesn't expose. The routing
        /// title is marked only on this tab, so a seeded default step doesn't look as if it
        /// were already in the file.</summary>
        private void WireAssemblyDirtyTracking()
        {
            CaptureBaseLabelText(labelAssemblyName);
            CaptureBaseLabelText(labelAssemblyNote);
            CaptureBaseLabelText(labelAssemblyStepsTitle);

            textBoxAssemblyName.TextChanged += (s, e) => UpdateAssemblyDirtyMarkers();
            textBoxAssemblyNote.TextChanged += (s, e) => UpdateAssemblyDirtyMarkers();

            processRoutingControlAssembly.RoutingChanged += (s, e) => UpdateAssemblyDirtyMarkers();
        }

        /// <summary>Re-evaluates the " *" markers of the two text labels and the routing
        /// title. The routing is compared the way Save would persist it (normalized groups, by
        /// MadeItemSyncService.SameRouting's signature), not by object identity.</summary>
        private void UpdateAssemblyDirtyMarkers()
        {
            var saved = _assemblyLocal;

            SetDirtyMarker(labelAssemblyName, !TextMatches(textBoxAssemblyName.Text, saved?.ArticleName));
            SetDirtyMarker(labelAssemblyNote, !TextMatches(textBoxAssemblyNote.Text, saved?.Note));
            SetDirtyMarker(
                labelAssemblyStepsTitle, !MadeItemSyncService.SameRouting(_assemblySteps, saved?.Processes));

            UpdateAssemblyRequiredMarkers();
        }

        /// <summary>Mirrors UpdateMadeItemRequiredMarkers, but only for the fields this tab
        /// actually has controls for: the two text fields and the routing's fabrication-step
        /// rule, evaluated against _assemblySteps.</summary>
        private void UpdateAssemblyRequiredMarkers()
        {
            const ItemKind kind = ItemKind.Made;
            var mapping = ApplicationServices.FieldMapping;

            UpdateIdentityRequiredMarkers(kind);

            SetRequiredMarker(
                labelAssemblyName,
                mapping.IsRequired(kind, ItemField.ArticleName) && IsBlank(textBoxAssemblyName.Text),
                Strings.TooltipRequiredField);
            SetRequiredMarker(
                labelAssemblyNote,
                mapping.IsRequired(kind, ItemField.Note) && IsBlank(textBoxAssemblyNote.Text),
                Strings.TooltipRequiredField);

            var fabricationStepRequired = mapping.IsRuleRequired(kind, RequiredFieldRuleName.FabricationStep);
            SetRequiredMarker(
                labelAssemblyStepsTitle,
                fabricationStepRequired && !RequiredFieldRuleEvaluator.HasFabricationStep(_assemblySteps),
                Strings.TooltipRequiredFabricationStep);
        }

        #endregion

        /// <summary>Disables the Assembly tab and blanks its two fields — used when the Source
        /// dropdown or the active document's type says this document isn't a made assembly.
        /// Mirrors ClearBoughtItemFields; the shared Part Number / Revision boxes are
        /// deliberately not touched, for the same reason as there.</summary>
        private void ClearAssemblyFields()
        {
            SetAssemblyTabEnabled(false);

            OnUiThread(() =>
            {
                textBoxAssemblyName.Text = string.Empty;
                textBoxAssemblyNote.Text = string.Empty;

                _assemblySteps = new List<ProcessStep>();
                processRoutingControlAssembly.SetSteps(_assemblySteps);
                labelAssemblyProcessesUnreadableHint.Visible = false;

                UpdateAssemblyDirtyMarkers();
            });

            _assemblyLocal = null;
        }

        /// <summary>Builds the MadeItemProperties that Save writes out: every field carried
        /// through from _assemblyLocal unchanged except the fields this tab actually edits
        /// (ArticleName, Note and the routing, taken from _assemblySteps). Same
        /// contract as ReadMadeItemFromUi's carry-through of PartNumber/Revision/BaseSize/
        /// Weblink — a field with no control on the tab keeps its last-loaded value rather than
        /// going blank — except that here it covers the whole model bar those fields.
        /// That is what keeps Material, Mass, BaseSize, ArticleGroupIds and
        /// SourceItemId untouched on a document saved from this tab, and what lets the tab grow
        /// controls for them later without the model having to change.</summary>
        private MadeItemProperties ReadAssemblyFromUi()
        {
            var saved = _assemblyLocal;

            return new MadeItemProperties
            {
                PartNumber = saved?.PartNumber,
                Revision = saved?.Revision,
                ArticleName = textBoxAssemblyName.Text,
                Material = saved?.Material,
                Mass = saved?.Mass,
                BaseSize = saved?.BaseSize,
                Note = textBoxAssemblyNote.Text,
                Weblink = saved?.Weblink,
                ArticleGroupIds = saved?.ArticleGroupIds ?? new List<int>(),
                ArticleGroupNames = saved?.ArticleGroupNames ?? new List<string>(),
                SourceItemId = saved?.SourceItemId,
                SourceItemName = saved?.SourceItemName,
                Processes = _assemblySteps,
                // The flag describes the read, not the value: whatever the routing control now
                // holds is what Save writes, so it is never carried forward. Matches
                // ReadMadeItemFromUi.
                ProcessesUnreadable = false,
                ProcessesStored = false,
                ProcessNames = saved?.ProcessNames,
            };
        }

        private void buttonAssemblySaveProperties_Click(object sender, EventArgs e)
        {
            RunGuarded(() => WritingProperties(SaveAssemblyPropertiesAsync));
        }

        /// <summary>Writes only what this tab edits — ArticleName, Note, the routing
        /// (Glados.Processes plus its derived process-names label), the write-once
        /// Glados.Kind stamp (ItemKind.Made — an assembly is still a "made" document for Kind
        /// purposes, see IsAssemblyDocument), and the rendered feature-tree label — and nothing
        /// else. Deliberately does not call WriteMass/WriteMaterial/WriteBaseSize/
        /// WriteMadeItemArticleGroups/WriteSourceItem: this tab has no controls
        /// for any of those fields, so there is nothing a save here could legitimately have
        /// changed, and calling them anyway would risk clobbering values a template link or a
        /// future feature already set. The feature-tree render uses the baseline's own
        /// ArticleGroupNames/SourceItemName (this save never touches those fields, so the
        /// baseline's values are exactly what a fresh resolve would produce anyway) rather than
        /// re-resolving them the way SaveMadeItemPropertiesAsync does; the process names it
        /// renders are the ones WriteProcesses has just stored in the document.
        /// Finishes with the same re-read every Save does (ReloadAllForModel) but skips PDM
        /// validation entirely — a made assembly always fails the backend's made-item
        /// extension check, so running it here would just paint the tab permanently
        /// Blocked.</summary>
        private async Task SaveAssemblyPropertiesAsync()
        {
            if (_activeModel == null || _assemblyLocal == null)
                return;

            var model = _activeModel;
            var fieldMapping = ApplicationServices.FieldMapping;
            var failures = new List<string>();

            // Same merge as the made tab, over the three fields this tab edits.
            MadeItemProperties edited;
            if (!TryMergeMadeSave(model, fieldMapping, _assemblyLocal, ReadAssemblyFromUi(), assembly: true, out edited))
                return;

            try
            {
                void WriteField(string label, ItemField field, string value)
                {
                    // Unmapped = never written - same "no opinion" reasoning as
                    // SaveMadeItemPropertiesAsync's own WriteField.
                    var propertyName = fieldMapping.ResolvePropertyName(ItemKind.Made, field);
                    if (propertyName == null)
                        return;

                    if (!SwCustomPropertyService.WriteText(model, propertyName, value))
                        failures.Add(label);
                }

                WriteField("Article Name", ItemField.ArticleName, edited.ArticleName);
                WriteField("Note", ItemField.Note, edited.Note);

                if (!SwCustomPropertyService.WriteKind(model, fieldMapping, ItemKind.Made))
                    failures.Add("Kind");

                // See SaveMadeItemPropertiesAsync: unreadable only when the merge kept the
                // file's routing, which is then left alone.
                if (!edited.ProcessesUnreadable
                    && !SwCustomPropertyService.WriteProcesses(model, fieldMapping, edited.Processes, out _))
                    failures.Add("Processes");

                if (!SwCustomPropertyService.WriteFeatureTreeRepresentation(model, fieldMapping, ItemKind.Made))
                    failures.Add("Feature Tree Representation");
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Error("Saving assembly properties failed", ex);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorFailedToSaveDocumentFormat, ex.Message));
                return;
            }

            if (failures.Count > 0)
                ShowError(
                    string.Format(CultureInfo.CurrentCulture, Strings.ErrorSavedButFailedFormat, string.Join(", ", failures))
                );

            // Re-read the document instead of adopting `edited` as the new baseline - same
            // "the file is the truth" reasoning as SaveMadeItemPropertiesAsync's own comment
            // on this. No PDM validation follows (see this method's doc comment).
            await ReloadAllForModel(model);
        }

        private void buttonAssemblyReloadProperties_Click(object sender, EventArgs e)
        {
            RunGuarded(ReloadAssemblyPropertiesAsync);
        }

        private async Task ReloadAssemblyPropertiesAsync()
        {
            if (_activeModel == null)
                return;

            await ReloadAllForModel(_activeModel);
        }

        #endregion

        #region PDM validation

        /// <summary>Resets the indicator to its initial state — see ApplyLocalizedStaticText
        /// (constructor/reconnect) and ClearBoughtItemStatusIndicators/ClearMadeItemStatusIndicators
        /// (document/tab switch). A validation result belongs to the document and connection it
        /// was computed against, so anything that invalidates either clears it back to this
        /// rather than leaving a stale result on screen.</summary>
        private void SetPdmValidationNotChecked(Label mdiLabel, Label textLabel, ToolTip tooltip)
        {
            SetPdmValidationIndicator(
                mdiLabel, textLabel, tooltip,
                MaterialIcons.CloudOff, Strings.PdmValidationNotChecked, PdmValidationColorNeutral,
                string.Empty);
        }

        private void SetPdmValidationIndicator(
            Label mdiLabel,
            Label textLabel,
            ToolTip tooltip,
            string icon,
            string text,
            Color color,
            string tooltipText)
        {
            OnUiThread(() =>
            {
                SetIndicator(mdiLabel, textLabel, icon, text, color);
                // AutoEllipsis on textLabel can truncate a long status — the tooltip always
                // carries the full text (or the backend's reasons, when blocked) so nothing is
                // lost, mirroring SetPossibleDuplicateTooltip's hover-for-detail pattern rather
                // than a second, space-hungry multiline control.
                tooltip.SetToolTip(textLabel, tooltipText);
                tooltip.SetToolTip(mdiLabel, tooltipText);
            });
        }

        /// <summary>Paints a finished validation run as blocked when the document references
        /// files in a Downloads folder, and returns false (painting nothing) when it doesn't, so
        /// the caller paints the server's verdict as usual. The one local rule that overrides the
        /// backend: whatever the server says — ready, blocked, unreachable or disabled — such a
        /// link breaks for everyone else once released, so the line never claims "ready" over
        /// it. <paramref name="serverDetail"/> (the backend's reasons or the failure, null when
        /// it said ready) follows the local reason in the tooltip.</summary>
        private bool TrySetPdmValidationDownloadsBlocked(
            IList<string> downloadsReferences,
            Label mdiLabel,
            Label textLabel,
            ToolTip tooltip,
            string serverDetail)
        {
            if (downloadsReferences.Count == 0)
                return false;

            var lines = DescribeDownloadsReferences(downloadsReferences);
            if (!string.IsNullOrEmpty(serverDetail))
            {
                lines.Add(string.Empty);
                lines.Add(serverDetail);
            }

            SetPdmValidationIndicator(
                mdiLabel, textLabel, tooltip,
                MaterialIcons.Block, Strings.PdmValidationBlocked, PdmValidationColorBlocked,
                string.Join(System.Environment.NewLine, lines));
            return true;
        }

        /// <summary>Shows or hides the document-wide warning above the tabs for the active
        /// document's references into a Downloads folder, found on every document load (see
        /// ReloadAllForModel) and again on every validation run, so fixing the link and
        /// validating clears it without reopening the document. Purely informational — it
        /// blocks nothing by itself; the validation line is where it counts.</summary>
        private void ShowDownloadsReferences(IList<string> references)
        {
            _downloadsReferences = references;
            OnUiThread(() =>
            {
                var found = references.Count > 0;
                labelDownloadsReferenceMdi.Visible = found;
                labelDownloadsReference.Visible = found;
                SetIndicator(
                    labelDownloadsReferenceMdi, labelDownloadsReference,
                    MaterialIcons.Warning, Strings.DownloadsReferenceWarning, PdmValidationColorBlocked);

                var tooltipText = found
                    ? string.Join(System.Environment.NewLine, DescribeDownloadsReferences(references))
                    : string.Empty;
                _downloadsReferenceTooltip.SetToolTip(labelDownloadsReference, tooltipText);
                _downloadsReferenceTooltip.SetToolTip(labelDownloadsReferenceMdi, tooltipText);
            });
        }

        /// <summary>What's wrong, which files, and how to fix it — one line each, shared by the
        /// warning's tooltip and a blocked validation's.</summary>
        private static List<string> DescribeDownloadsReferences(IEnumerable<string> references)
        {
            var lines = new List<string> { Strings.DownloadsReferenceHeader };
            lines.AddRange(references);
            lines.Add(Strings.DownloadsReferenceAdvice);
            return lines;
        }

        private static string DescribePdmOutcome(PdmValidationOutcome outcome)
        {
            switch (outcome)
            {
                case PdmValidationOutcome.Created: return Strings.PdmValidationOutcomeCreated;
                case PdmValidationOutcome.Revised: return Strings.PdmValidationOutcomeRevised;
                case PdmValidationOutcome.Updated: return Strings.PdmValidationOutcomeUpdated;
                case PdmValidationOutcome.Claimed: return Strings.PdmValidationOutcomeClaimed;
                case PdmValidationOutcome.Status: return Strings.PdmValidationOutcomeStatus;
                default: return Strings.PdmValidationOutcomeUnknown;
            }
        }

        /// <summary>Localized label for a pending_pdm wire field name — the same lookup
        /// UpdateIdentityRequiredMarkers uses for the identity boxes' own labels, since
        /// pending_pdm today only ever names part_number/revision. Falls back to the raw wire
        /// name for a field this build has no label for, rather than hiding it.</summary>
        private static string DescribePendingPdmField(string wireFieldName)
        {
            switch (wireFieldName)
            {
                case "part_number": return Strings.LabelPartNumberText;
                case "revision": return Strings.LabelRevisionText;
                default: return wireFieldName;
            }
        }

        private static string JoinPendingPdmFields(IEnumerable<string> pendingPdm)
        {
            return string.Join(", ", pendingPdm.Select(DescribePendingPdmField));
        }

        /// <summary>Builds the tooltip for a validation result: <paramref name="baseText"/> (the
        /// "Ready for PDM (…)" text or the joined Reasons, whichever the caller already has),
        /// then <paramref name="response"/>'s Notes appended below it, then — when FileIdentity
        /// is Unresolved — a short local hint that the check ran without a file_path match in
        /// PDM, then — when PendingPdm is non-empty — an informational line naming the
        /// PDM-owned fields the workflow will assign at release. Like Notes, this line is purely
        /// informational: it never turns pending_pdm into a reason, and it appears whether Ok is
        /// true or false. The status line itself stays single-line (AutoEllipsis), so everything
        /// beyond the headline text lives here rather than in the label, matching how Reasons
        /// already work. Never affects Ok/Would, so it can never turn an advisory check into
        /// something that blocks or warns more than the backend already said.</summary>
        private static string BuildPdmValidationTooltip(string baseText, PdmValidationResponse response)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(baseText))
                lines.Add(baseText);

            if (response?.Notes != null)
                lines.AddRange(response.Notes.Where(note => !string.IsNullOrWhiteSpace(note)));

            if (response?.FileIdentity == PdmFileIdentity.Unresolved)
                lines.Add(Strings.PdmValidationFileIdentityUnresolved);

            if (response?.PendingPdm != null && response.PendingPdm.Count > 0)
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PdmValidationPendingTooltipFormat, JoinPendingPdmFields(response.PendingPdm)));

            return string.Join(System.Environment.NewLine, lines);
        }

        /// <summary>The wire "kind" for the validate request: the Source dropdown's current
        /// selection, independent of which tab's Save/Validate triggered the check — see
        /// README's "Kind decides which catalogue". Null mirrors an unset dropdown.</summary>
        private ItemKind? GetSelectedSourceKind()
        {
            return ItemKindExtensions.TryParseWireValue(comboBoxSource.SelectedItem as string, out var kind)
                ? (ItemKind?)kind
                : null;
        }

        private Task ValidateBoughtItemPdmAsync()
        {
            return RunPdmValidationAsync(
                ItemKind.Bought,
                labelBoughtItemPdmValidationMdi,
                labelBoughtItemPdmValidation,
                buttonBoughtItemValidate,
                _boughtItemPdmValidationTooltip);
        }

        private Task ValidateMadeItemPdmAsync()
        {
            return RunPdmValidationAsync(
                ItemKind.Made,
                labelMadeItemPdmValidationMdi,
                labelMadeItemPdmValidation,
                buttonMadeItemValidate,
                _madeItemPdmValidationTooltip);
        }

        /// <summary>
        /// Runs one PDM validation dry run for the active document and paints the result onto
        /// <paramref name="mdiLabel"/>/<paramref name="textLabel"/> — shared between the Bought
        /// and Made tabs since the request/response handling is identical, parameterized only by
        /// which tab's field mapping (<paramref name="tabKind"/>) and controls apply. Advisory
        /// only: every failure path (unreachable server, rejected auth, an unexpected exception)
        /// paints "check not possible" rather than showing a MessageBox or throwing, so a
        /// validation run — automatic after Save or from the button — can never interfere with
        /// Save itself or interrupt the user. The one local addition to the server's verdict is
        /// the Downloads reference check (TrySetPdmValidationDownloadsBlocked), which turns any
        /// outcome into "not ready". See README's "Validation before release".
        /// </summary>
        private async Task RunPdmValidationAsync(
            ItemKind tabKind,
            Label mdiLabel,
            Label textLabel,
            Button button,
            ToolTip tooltip)
        {
            if (_activeModel == null)
                return;

            var model = _activeModel;
            int generation = _loadGeneration;

            button.Enabled = false;
            SetPdmValidationIndicator(
                mdiLabel, textLabel, tooltip,
                MaterialIcons.Autorenew, Strings.PdmValidationChecking, PdmValidationColorNeutral,
                string.Empty);

            // Read first, before anything that can fail, so even a failed request still reports
            // it — see TrySetPdmValidationDownloadsBlocked.
            var downloadsReferences = DownloadsReferenceService.FindDownloadsReferences(model);
            ShowDownloadsReferences(downloadsReferences);

            try
            {
                string fileName;
                string filePath;
                try
                {
                    var pathName = model.GetPathName();
                    // GetPathName() returns "" for a document that has never been saved —
                    // treat that the same as null rather than sending an empty file_path.
                    filePath = string.IsNullOrEmpty(pathName) ? null : pathName;
                    fileName = string.IsNullOrEmpty(pathName) ? null : Path.GetFileName(pathName);
                }
                catch (Exception ex)
                    when (!(ex is OutOfMemoryException || ex is StackOverflowException))
                {
                    fileName = null;
                    filePath = null;
                }

                var variables = SwCustomPropertyService.BuildPdmValidationVariables(
                    model, ApplicationServices.FieldMapping, tabKind);
                var requestKind = GetSelectedSourceKind();

                var result = await ApplicationServices.Api.PdmValidation
                    .ValidateAsync(requestKind, fileName, filePath, variables)
                    .ConfigureAwait(false);

                if (generation != _loadGeneration)
                    // A newer document/tab load has since started — discard this stale result
                    // rather than paint it over whatever's actually active now. See
                    // _loadGeneration.
                    return;

                if (result.Status == PdmValidationCallStatus.IntegrationDisabled)
                {
                    if (TrySetPdmValidationDownloadsBlocked(
                            downloadsReferences, mdiLabel, textLabel, tooltip,
                            Strings.PdmValidationIntegrationDisabled))
                        return;

                    SetPdmValidationIndicator(
                        mdiLabel, textLabel, tooltip,
                        MaterialIcons.CloudOff, Strings.PdmValidationIntegrationDisabled,
                        PdmValidationColorDisabled, string.Empty);
                    return;
                }

                var response = result.Response;
                var reasons = response?.Reasons ?? new List<string>();
                var serverDetail = response != null && response.Ok
                    ? null
                    : string.Join(System.Environment.NewLine, reasons);
                if (TrySetPdmValidationDownloadsBlocked(
                        downloadsReferences, mdiLabel, textLabel, tooltip, serverDetail))
                    return;

                if (response != null && response.Ok)
                {
                    // The label carries only the verdict; the predicted outcome (create / new
                    // revision / ...) and any PDM-owned fields still to be assigned live in the
                    // tooltip, where BuildPdmValidationTooltip already lists them — the label is
                    // too narrow for either without truncating.
                    var detail = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PdmValidationReadyFormat, DescribePdmOutcome(response.Would));
                    SetPdmValidationIndicator(
                        mdiLabel, textLabel, tooltip,
                        MaterialIcons.CheckCircle, Strings.PdmValidationReady, PdmValidationColorReady,
                        BuildPdmValidationTooltip(detail, response));
                }
                else
                {
                    SetPdmValidationIndicator(
                        mdiLabel, textLabel, tooltip,
                        MaterialIcons.Block, Strings.PdmValidationBlocked, PdmValidationColorBlocked,
                        BuildPdmValidationTooltip(serverDetail, response));
                }
            }
            catch (GladosApiException ex)
            {
                Log.Warning("PDM validation request failed", ex);
                if (generation == _loadGeneration
                    && !TrySetPdmValidationDownloadsBlocked(
                        downloadsReferences, mdiLabel, textLabel, tooltip, ex.Message))
                    SetPdmValidationIndicator(
                        mdiLabel, textLabel, tooltip,
                        MaterialIcons.ErrorOutline, Strings.PdmValidationUnreachable,
                        PdmValidationColorError, ex.Message);
            }
            catch (HttpRequestException ex)
            {
                Log.Warning("PDM validation request failed", ex);
                if (generation == _loadGeneration
                    && !TrySetPdmValidationDownloadsBlocked(
                        downloadsReferences, mdiLabel, textLabel, tooltip, ex.Message))
                    SetPdmValidationIndicator(
                        mdiLabel, textLabel, tooltip,
                        MaterialIcons.ErrorOutline, Strings.PdmValidationUnreachable,
                        PdmValidationColorError, ex.Message);
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                // Defensive backstop — see RunGuarded's doc comment. A validation failure must
                // never surface as a blocking error or interfere with the Save it may have just
                // followed.
                Log.Error("PDM validation failed unexpectedly", ex);
                if (generation == _loadGeneration
                    && !TrySetPdmValidationDownloadsBlocked(
                        downloadsReferences, mdiLabel, textLabel, tooltip, ex.Message))
                    SetPdmValidationIndicator(
                        mdiLabel, textLabel, tooltip,
                        MaterialIcons.ErrorOutline, Strings.PdmValidationUnreachable,
                        PdmValidationColorError, ex.Message);
            }
            finally
            {
                // Re-derives Enabled from the current facts rather than hardcoding true — if
                // the connection dropped while this call was in flight, it must stay disabled.
                ApplyConnectionState();
            }
        }

        private void buttonBoughtItemValidate_Click(object sender, EventArgs e)
        {
            RunGuarded(ValidateBoughtItemPdmAsync);
        }

        private void buttonMadeItemValidate_Click(object sender, EventArgs e)
        {
            RunGuarded(ValidateMadeItemPdmAsync);
        }

        #endregion

        #region Callbacks

        private void linkLabelSettings_LinkClicked(
            object sender,
            LinkLabelLinkClickedEventArgs e
        )
        {
            RunGuarded(OpenSettingsAsync);
        }

        private async Task OpenSettingsAsync()
        {
            using (var settingsForm = new SettingsForm())
            {
                var result = settingsForm.ShowDialog(this);
                EnsureUiSynchronizationContext();
                if (result == DialogResult.OK)
                {
                    await RefreshAuthStateAsync();
                }
            }
        }

        private void linkLabelReload_LinkClicked(
            object sender,
            LinkLabelLinkClickedEventArgs e
        )
        {
            RunGuarded(OnReloadClickedAsync);
        }

        /// <summary>Reload semantics: connected re-fetches the lookup snapshot and re-syncs the
        /// field mapping, then repopulates the pickers preserving whatever's currently selected
        /// (even a since-archived value) rather than snapping back to the loaded document's
        /// saved baseline — see RepopulateLookupControlsPreservingSelection. Disconnected just
        /// re-runs the same auth refresh GladosTaskPaneControl_Load does, which already falls
        /// back to the cached snapshot. Disabled for the duration via linkLabelReload.Enabled so
        /// a second click can't overlap the first.</summary>
        private async Task OnReloadClickedAsync()
        {
            OnUiThread(() => linkLabelReload.Enabled = false);
            try
            {
                if (!_isConnected)
                {
                    await RefreshAuthStateAsync();
                    return;
                }

                await ApplicationServices.Lookups.RefreshAsync(ApplicationServices.Api.Lookups);
                await ApplicationServices.FieldMapping.SyncAsync(ApplicationServices.Api.FieldMapping);
                // Any sync can flip ServerKnown — most plausibly on this machine's very first
                // connection — so re-apply the same way RefreshAuthStateAsync does after its own
                // sync.
                ApplyConnectionState();
                RepopulateLookupControlsPreservingSelection();
            }
            finally
            {
                OnUiThread(() => linkLabelReload.Enabled = true);
                UpdateReloadTooltip();
            }
        }

        /// <summary>Rebuilds the manufacturer combo, both article-group checked lists, and every
        /// routing row's method choices from whatever the lookup snapshot now holds, while
        /// keeping each control's current selection/checks by id — even a value that just became
        /// archived. Unlike PopulateLookupControls (which re-derives from the loaded document's
        /// saved baseline), this reads the live UI state first: the point of Reload is not to
        /// discard whatever the user has picked but not yet saved.</summary>
        private void RepopulateLookupControlsPreservingSelection()
        {
            OnUiThread(() =>
            {
                var manufacturerId = comboBoxBoughtItemManufacturer.SelectedValue as int?;
                var manufacturerName = (comboBoxBoughtItemManufacturer.SelectedItem as SupplierResponse)?.Name;

                var boughtGroups = GetCheckedArticleGroups();
                var madeGroups = GetCheckedMadeItemArticleGroups();

                SetManufacturerSelection(manufacturerId, ResolveManufacturerLabel(manufacturerId) ?? manufacturerName);
                SetArticleGroupSelection(
                    boughtGroups.Select(g => g.Id).ToList(), boughtGroups.Select(g => g.Name).ToList());
                SetMadeItemArticleGroupSelection(
                    madeGroups.Select(g => g.Id).ToList(), madeGroups.Select(g => g.Name).ToList());

                processRoutingControl1.SetMethodChoices(PickableFabricationMethods(), PickableInspectionMethods());
                processRoutingControlAssembly.SetMethodChoices(PickableFabricationMethods(), PickableInspectionMethods());

                UpdateDirtyMarkers();
                UpdateMadeItemDirtyMarkers();
                UpdateAssemblyDirtyMarkers();
            });
        }

        /// <summary>Sets linkLabelReload's tooltip to the last sync time (or "never synced") for
        /// the currently configured server — called from the constructor, ApplyLocalizedStaticText
        /// (a language switch needs the format re-localized), OnLookupsChanged (a background
        /// refresh landing bumps the time even without a Reload click) and OnReloadClickedAsync.</summary>
        private void UpdateReloadTooltip()
        {
            var snapshot = ApplicationServices.Lookups.Current;

            // Timestamp formatted in the UI language, like the offline tooltip in
            // ApplyConnectionState.
#pragma warning disable CA1305
            var text = snapshot.SyncedAt.HasValue
                ? string.Format(
                    CultureInfo.CurrentUICulture, Strings.TooltipReloadFormat, snapshot.SyncedAt.Value.LocalDateTime)
                : Strings.TooltipReloadNeverSynced;
#pragma warning restore CA1305

            OnUiThread(() => _reloadTooltip.SetToolTip(linkLabelReload, text));
        }

        private static bool IsBoughtSource(string source)
        {
            return source == "bought";
        }

        private static bool IsMadeSource(string source)
        {
            return source == "made";
        }

        private void comboBoxSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            RunGuarded(ComboBoxSourceSelectedIndexChangedAsync);
        }

        private async Task ComboBoxSourceSelectedIndexChangedAsync()
        {
            if (_suppressSourceChange)
                return;

            var newSource = comboBoxSource.SelectedItem as string;
            if (newSource == _currentSource)
                return;

            // Only warn about losing tab data when the prior selection was itself valid — an
            // invalid/blank starting value never had a tab enabled, so there's nothing to lose.
            var currentWasValid = IsBoughtSource(_currentSource) || IsMadeSource(_currentSource);

            if (currentWasValid)
            {
                var confirmed =
                    MessageBox.Show(
                        Strings.ChangeSourceWarningMessage,
                        Strings.ChangeSourceWarningTitle,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    ) == DialogResult.Yes;

                if (!confirmed)
                {
                    RevertSourceSelection();
                    return;
                }
            }

            // Nothing is written to the document here — picking a kind is only persisted
            // (as Glados.Kind, write-once) the next time the user actually saves item
            // properties on the tab it switches to. See WriteKind and README's "Kind decides
            // which catalogue".
            _currentSource = newSource;
            await ApplySourceGateAsync();
        }

        private void buttonInitMade_Click(object sender, EventArgs e)
        {
            SelectSource(ItemKind.Made);
        }

        private void buttonInitBought_Click(object sender, EventArgs e)
        {
            SelectSource(ItemKind.Bought);
        }

        /// <summary>The Default tab's shortcut buttons pick a Source exactly as the dropdown
        /// does: setting SelectedIndex raises comboBoxSource_SelectedIndexChanged, so the usual
        /// ComboBoxSourceSelectedIndexChangedAsync → ApplySourceGateAsync path runs (a made
        /// .sldasm still lands on the Assembly tab). Nothing is written to the document here
        /// either — Kind is only stamped by the first Save on the tab this switches to.</summary>
        private void SelectSource(ItemKind kind)
        {
            comboBoxSource.SelectedIndex = comboBoxSource.FindStringExact(kind.ToWireValue());
        }

        /// <summary>Enables or disables the Source dropdown and the Default tab's shortcut
        /// buttons together, so the buttons are locked by exactly what locks the dropdown (see
        /// SourceDropdownAllowed). The buttons additionally need an open document: with none
        /// there is no kind to pick, and a pick would only enable an empty tab.</summary>
        private void SetSourceSelectionEnabled(bool enabled)
        {
            var buttonsEnabled = enabled && _activeModel != null;

            comboBoxSource.Enabled = enabled;
            buttonInitMade.Enabled = buttonsEnabled;
            buttonInitBought.Enabled = buttonsEnabled;
            // A made assembly is no catalogue item, so it is never a customer item.
            buttonInitCustomer.Enabled = buttonsEnabled && !IsAssemblyDocument(_activeModel);
        }

        private void RevertSourceSelection()
        {
            _suppressSourceChange = true;
            try
            {
                comboBoxSource.SelectedIndex = comboBoxSource.FindStringExact(_currentSource);
            }
            finally
            {
                _suppressSourceChange = false;
            }
        }

        /// <summary>The Source dropdown's current selection decides which of Bought Item /
        /// Made Item / Assembly applies to this document: "bought" uploads as a bought item,
        /// "made" as a made item — except a "made" .sldasm, which routes to the Assembly tab
        /// instead (see IsAssemblyDocument and the Assembly item region below: a made assembly
        /// is deliberately not a Glados catalogue item, so it gets a metadata-only tab of its
        /// own rather than the Made tab's catalogue plumbing). Switches to the matching tab and
        /// disables + clears the other two. The selection is only ever "bought" or "made" —
        /// anything else (blank, or a stray value matching neither combo item) clears and
        /// disables all three tabs and shows the Default tab. Nothing is written to the
        /// document from here; the dropdown reflects Glados.Kind as loaded by
        /// LoadKindForActiveModel/ApplyKindLock, and the only write path is WriteKind on Save. A drawing bypasses the dropdown entirely and always lands
        /// on the Drawing tab (see IsDrawingDocument).</summary>
        private async Task ApplySourceGateAsync()
        {
            // Every path that re-checks Kind/Bought-Item state for the current document
            // converges here (ReloadAllForModel, a configuration switch, a dropdown change) —
            // treat each call as the start of a fresh load, superseding whatever's still in
            // flight from the last one. See _loadGeneration.
            _loadGeneration++;

            var source = comboBoxSource.SelectedItem as string;
            var isAssembly = IsAssemblyDocument(_activeModel);

            if (IsDrawingDocument(_activeModel))
            {
                ClearBoughtItemFields();
                tabPageMade.Enabled = false;
                ClearMadeItemStatusIndicators();
                ClearAssemblyFields();
                tabControlSource.SelectedTab = tabPageDrawing;
            }
            else if (IsBoughtSource(source))
            {
                tabPageMade.Enabled = false;
                ClearMadeItemStatusIndicators();
                ClearAssemblyFields();
                SetBoughtItemTabEnabled(true);
                tabControlSource.SelectedTab = tabPageBought;

                if (_activeModel != null)
                    await LoadBoughtItemAsync(_activeModel);
            }
            else if (IsMadeSource(source) && isAssembly)
            {
                SetAssemblyTabEnabled(true);
                tabControlSource.SelectedTab = tabPageAssembly;

                tabPageMade.Enabled = false;
                ClearMadeItemStatusIndicators();

                ClearBoughtItemFields();

                if (_activeModel != null)
                    await LoadAssemblyAsync(_activeModel);
            }
            else if (IsMadeSource(source))
            {
                ClearBoughtItemFields();
                ClearAssemblyFields();
                tabPageMade.Enabled = true;
                tabControlSource.SelectedTab = tabPageMade;

                // Clear stale indicator values from whatever document was previously shown
                // before loading the new one (the bought branch above clears these too, when
                // disabling the tab instead).
                ClearMadeItemStatusIndicators();

                if (_activeModel != null)
                    await LoadMadeItemAsync(_activeModel);
            }
            else
            {
                ClearBoughtItemFields();
                ClearMadeItemFields();
                ClearAssemblyFields();
                tabControlSource.SelectedTab = tabPageDefault;

                _currentSource = string.Empty;
            }
        }

        private void comboBoxConfiguration_SelectedIndexChanged(object sender, EventArgs e)
        {
            RunGuarded(ComboBoxConfigurationSelectedIndexChangedAsync);
        }

        private async Task ComboBoxConfigurationSelectedIndexChangedAsync()
        {
            if (
                _suppressConfigurationChange
                || _activeModel == null
                || comboBoxConfiguration.SelectedItem == null
            )
                return;

            var model = _activeModel;
            var configName = comboBoxConfiguration.SelectedItem.ToString();

            model.ShowConfiguration2(configName);
            model.EditRebuild3();

            // Custom properties (PartNumber/Revision/Kind included) can be configuration-
            // specific, so re-read everything — Kind included — for the newly active
            // configuration. This doesn't come back through OnActiveDocChanged — the
            // document reference hasn't changed, only which configuration within it is active.
            // A configuration switch can change which copy of PDM.PartNumber / PDM.Revision
            // is active — re-read the identity boxes before the kind gate reloads the tabs.
            PopulateIdentityFields(model);
            await LoadKindForActiveModel(model);
        }

        /// <summary>Brings the pane back in line with a configuration SolidWorks became active
        /// on without the dropdown (see timerWatchDocumentChange_Tick): re-lists and re-selects
        /// the dropdown, then re-reads the same way a pick in the dropdown does
        /// (ComboBoxConfigurationSelectedIndexChangedAsync) minus ShowConfiguration2, since
        /// SolidWorks is already showing it. Like that pick, the re-read replaces any unsaved
        /// edits in the tabs. A document held locked as never saved only gets the dropdown —
        /// nothing else is read from it until its first save.</summary>
        private async Task FollowActiveConfigurationAsync(ModelDoc2 model)
        {
            LoadConfigurationsForActiveModel(model);

            if (_activeDocumentUnsaved)
                return;

            PopulateIdentityFields(model);
            await LoadKindForActiveModel(model);
        }

        private void buttonBoughtItemSaveProperties_Click(object sender, EventArgs e)
        {
            RunGuarded(() => WritingProperties(SaveBoughtItemPropertiesAsync));
        }

        private async Task SaveBoughtItemPropertiesAsync()
        {
            if (_activeModel == null || _boughtItemLocal == null)
                return;

            var model = _activeModel;
            var failures = new List<string>();
            var fieldMapping = ApplicationServices.FieldMapping;

            // What this Save writes: the pane's edits on top of the file as it is now, not as
            // it was loaded — see GladosTaskPaneControl.SaveMerge.cs.
            BoughtItemProperties edited;
            if (!TryMergeBoughtSave(model, fieldMapping, ReadBoughtItemFromUi(), out edited))
                return;

            try
            {
                void WriteField(string label, ItemField field, string value)
                {
                    // Unmapped = never written — a field the current server mapping doesn't
                    // cover is left alone rather than guessed at. See
                    // docs/FIELD_OWNERSHIP.md section 2.
                    var propertyName = fieldMapping.ResolvePropertyName(ItemKind.Bought, field);
                    if (propertyName == null)
                        return;

                    if (!SwCustomPropertyService.WriteText(model, propertyName, value))
                        failures.Add(label);
                }

                WriteField("Article Name", ItemField.ArticleName, edited.ArticleName);
                WriteField("Order Code 1", ItemField.OrderCode1, edited.OrderCode1);
                WriteField("Order Code 2", ItemField.OrderCode2, edited.OrderCode2);
                WriteField("GTIN", ItemField.Gtin, edited.Gtin);
                WriteField("EAN", ItemField.Ean, edited.Ean);
                WriteField("Tariff Number", ItemField.TariffNumber, edited.TariffNumber);
                WriteField("Note", ItemField.Note, edited.Note);
                WriteField("Weblink", ItemField.Weblink, edited.Weblink);
                WriteField(
                    "Data Portal Part",
                    ItemField.EdpPartId,
                    edited.EdpPartId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

                // Preserves the template's linked SW-Mass expression: mass is authored by the
                // add-in only on an explicit user override, detected against the value loaded
                // from the document (_boughtItemLocal) — never against the live resolved value,
                // which a rebuild can change between load and Save. See WriteMass and README's
                // "Linked expressions on Save".
                if (!SwCustomPropertyService.WriteMass(
                        model, fieldMapping, ItemKind.Bought, edited.Mass,
                        _boughtItemLocal != null && edited.Mass != _boughtItemLocal.Mass))
                    failures.Add("Mass");

                if (!SwCustomPropertyService.WriteKind(model, fieldMapping, ItemKind.Bought))
                    failures.Add("Kind");

                // Re-applied on every Save, so a manual switch back to Show/Promote in the
                // configuration's BOM options doesn't outlive the next one.
                if (IsAssemblyDocument(model)
                    && !SwCustomPropertyService.WriteHideChildComponentsInBom(model))
                    failures.Add("BOM Child Components");

                // Labels are resolved from the lookup lists, never from the combo/list entries
                // themselves: a placeholder entry stands in for an id the lookup couldn't
                // resolve, and null tells the writer to leave the stored label alone rather
                // than overwrite a real name with a "#id" stand-in.
                if (
                    !SwCustomPropertyService.WriteManufacturer(
                        model,
                        fieldMapping,
                        edited.ManufacturerId,
                        ResolveManufacturerLabel(edited.ManufacturerId)
                    )
                )
                    failures.Add("Manufacturer");
                if (
                    !SwCustomPropertyService.WriteArticleGroups(
                        model,
                        fieldMapping,
                        edited.ArticleGroupIds,
                        ResolveArticleGroupLabels(edited.ArticleGroupIds)
                    )
                )
                    failures.Add("Article Groups");

                if (!SwCustomPropertyService.WriteFeatureTreeRepresentation(model, fieldMapping, ItemKind.Bought))
                    failures.Add("Feature Tree Representation");
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Error("Saving bought item properties failed", ex);
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorFailedToSaveDocumentFormat, ex.Message));
                return;
            }

            if (failures.Count > 0)
                ShowError(
                    string.Format(CultureInfo.CurrentCulture, Strings.ErrorSavedButFailedFormat, string.Join(", ", failures))
                );

            // Re-read the document rather than adopting `edited` as the baseline — see the
            // same step in SaveMadeItemPropertiesAsync: the file is the truth after a Save
            // (linked mass resolves afresh, labels re-resolve, configurations are mirrored),
            // and Reload From File's path brings the pane in line with it.
            await ReloadAllForModel(model);

            // Advisory-only, per README's "Validation before release" — runs after the save
            // above has already fully committed, so a validation failure (unreachable server,
            // disabled integration, whatever) can never affect what Save just did.
            await ValidateBoughtItemPdmAsync();
        }

        private void buttonBoughtItemReloadProperties_Click(object sender, EventArgs e)
        {
            RunGuarded(ReloadBoughtItemPropertiesAsync);
        }

        private async Task ReloadBoughtItemPropertiesAsync()
        {
            if (_activeModel == null)
                return;

            await ReloadAllForModel(_activeModel);
        }

        /// <summary>Shows the Embed Imported Components row for a bought assembly and hides it
        /// for a part, moving the indicator lines and the property panel down by one button row
        /// while it shows. The designer lays the tab out without the row, as every bought part
        /// sees it; the offset is measured from the laid-out buttons so it follows DPI scaling.
        /// Shown for every bought assembly rather than only one that still holds a 3D
        /// Interconnect subassembly: inserting one doesn't reload the pane, so the click checks
        /// instead (ImportedComponentsService reports NothingToEmbed).</summary>
        private void SetEmbedImportedComponentsVisible(bool visible)
        {
            OnUiThread(() =>
            {
                if (visible == _embedImportedComponentsShown)
                    return;

                _embedImportedComponentsShown = visible;
                var rowHeight = buttonBoughtItemEmbedImportedComponents.Bottom - buttonBoughtItemLoadFromGlados.Bottom;
                var offset = visible ? rowHeight : -rowHeight;

                tabPageBought.SuspendLayout();
                buttonBoughtItemEmbedImportedComponents.Visible = visible;
                foreach (
                    var control in new Control[]
                    {
                        labelBoughtItemSyncMdi,
                        labelBoughtItemSync,
                        labelBoughtItemStateMdi,
                        labelBoughtItemState,
                        labelBoughtItemPdmValidationMdi,
                        labelBoughtItemPdmValidation,
                    }
                )
                    control.Top += offset;
                panelBoughItemProperties.Top += offset;
                panelBoughItemProperties.Height -= offset;
                tabPageBought.ResumeLayout();
            });
        }

        private void buttonBoughtItemEmbedImportedComponents_Click(object sender, EventArgs e)
        {
            RunGuarded(EmbedImportedComponentsAsync);
        }

        /// <summary>Turns the active bought assembly's 3D Interconnect subassemblies into virtual
        /// components after a confirmation, since breaking the link can't be undone — see
        /// ImportedComponentsService for the steps. Runs synchronously on the UI thread like
        /// every other SolidWorks call here. The assembly's own unsaved changes are saved before
        /// the link is broken, the result never: it is left modified for the user to check and
        /// save, or close without saving to get the state from just before. The imported file
        /// is checked in to PDM by the run, and any it couldn't check in is listed whatever the
        /// outcome, since the link may already be broken when a later step fails. The Downloads warning is
        /// re-checked afterwards, as the broken link usually was the reference it was about.</summary>
        private Task EmbedImportedComponentsAsync()
        {
            var model = _activeModel;
            if (model == null)
                return Task.CompletedTask;

            var confirmed =
                MessageBox.Show(
                    Strings.EmbedImportedComponentsConfirmMessage,
                    Strings.EmbedImportedComponentsConfirmTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                ) == DialogResult.Yes;
            if (!confirmed)
                return Task.CompletedTask;

            EmbedImportedComponentsResult result;
            var previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                var checkInComment = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.EmbedImportedComponentsCheckInCommentFormat,
                    Path.GetFileName(model.GetPathName()));
                result = ImportedComponentsService.EmbedImportedSubassemblies(
                    SolidWorksProvider?.Invoke(), model, checkInComment, Handle.ToInt32());
            }
            finally
            {
                Cursor.Current = previousCursor;
            }

            ShowDownloadsReferences(DownloadsReferenceService.FindDownloadsReferences(model));

            // Reported with any outcome: the link may be broken even when a later step failed.
            var notCheckedIn = result.NotCheckedIn.Count == 0
                ? string.Empty
                : System.Environment.NewLine + System.Environment.NewLine + string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.EmbedImportedComponentsNotCheckedInFormat,
                    string.Join(System.Environment.NewLine, result.NotCheckedIn));

            switch (result.Outcome)
            {
                case EmbedImportedComponentsOutcome.Embedded:
                    MessageBox.Show(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.EmbedImportedComponentsDoneFormat,
                            result.EmbeddedCount) + notCheckedIn,
                        Strings.MessageBoxTitleGlados,
                        MessageBoxButtons.OK,
                        result.NotCheckedIn.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    break;
                case EmbedImportedComponentsOutcome.NothingToEmbed:
                    MessageBox.Show(
                        Strings.EmbedImportedComponentsNothingFound,
                        Strings.MessageBoxTitleGlados,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;
                case EmbedImportedComponentsOutcome.ReadOnly:
                    ShowError(Strings.EmbedImportedComponentsReadOnly);
                    break;
                case EmbedImportedComponentsOutcome.Failed
                    when result.FailedStep == EmbedImportedComponentsStep.SaveAssembly:
                    // Nothing changed yet, so "close without saving" would only lose the
                    // user's own unsaved work — this failure gets its own message.
                    ShowError(Strings.EmbedImportedComponentsSaveFailed);
                    break;
                default:
                    ShowError(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.EmbedImportedComponentsFailedFormat,
                        DescribeEmbedStep(result.FailedStep)) + notCheckedIn);
                    break;
            }

            return Task.CompletedTask;
        }

        private static string DescribeEmbedStep(EmbedImportedComponentsStep step)
        {
            switch (step)
            {
                case EmbedImportedComponentsStep.SaveToWorkFolder:
                    return Strings.EmbedImportedComponentsStepSaveToWorkFolder;
                case EmbedImportedComponentsStep.Dissolve:
                    return Strings.EmbedImportedComponentsStepDissolve;
                case EmbedImportedComponentsStep.MakeVirtual:
                    return Strings.EmbedImportedComponentsStepMakeVirtual;
                default:
                    return Strings.EmbedImportedComponentsStepBreakLink;
            }
        }

        private void buttonBoughtItemVisitWeblink_Click(object sender, EventArgs e)
        {
            var url = textBoxBoughtItemWeblink.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (
                !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            )
            {
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorWeblinkInvalidFormat, url));
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(uri.AbsoluteUri);
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorWeblinkOpenFailedFormat, ex.Message));
            }
        }

        private void buttonBoughtItemLoadFromGlados_Click(object sender, EventArgs e)
        {
            RunGuarded(LoadBoughtItemFromGladosAsync);
        }

        private async Task LoadBoughtItemFromGladosAsync()
        {
            if (_activeModel == null || _boughtItemLocal == null || !_boughtItemLocal.HasPartNumber)
                return;

            buttonBoughtItemLoadFromGlados.Enabled = false;

            try
            {
                var remote =
                    await ApplicationServices.Api.BoughtItems.GetBoughtItemByPartNumberAsync(
                        _boughtItemLocal.PartNumber
                    );

                // Populates the UI only — _boughtItemLocal (the last-saved-to-document
                // baseline) is untouched, so Reload From File still reverts to what's actually
                // on disk rather than to these unsaved, just-loaded values. forceOverwrite: true
                // because this is an explicit pull of authoritative server data — it should
                // always show what the server actually has, not silently skip a field.
                PopulateBoughtItemFields(MapFromRemote(remote), forceOverwrite: true);
            }
            catch (GladosApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Not in Glados yet — not an error, nothing to load.
            }
            catch (GladosApiException ex)
            {
                ShowError(string.Format(CultureInfo.CurrentCulture, Strings.ErrorLoadFailedFormat, ex.Message));
            }
            catch (HttpRequestException)
            {
                ShowError(Strings.ErrorLoadFailedNoConnection);
            }
            finally
            {
                // Re-derives Enabled from the current facts rather than hardcoding true — if
                // the connection dropped while this call was in flight, it must stay disabled.
                ApplyConnectionState();
            }
        }

        #endregion

        private void tabPageBought_Click(object sender, EventArgs e) { }

        private void panelMadeItemProperties_Paint(object sender, PaintEventArgs e)
        {

        }

        private void processRoutingControl1_Load(object sender, EventArgs e)
        {

        }
    }
}

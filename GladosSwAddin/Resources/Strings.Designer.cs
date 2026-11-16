using System.Globalization;
using System.Resources;

namespace GladosSwAddin.Resources
{
    /// <summary>
    /// Strongly-typed accessor for Resources/Strings.resx (+ Strings.de-AT.resx). Hand-written
    /// rather than tool-generated — there's no Visual Studio designer pass in this environment —
    /// but follows the same ResXFileCodeGenerator pattern so it can be regenerated normally later.
    /// Resolution follows Thread.CurrentThread.CurrentUICulture via ResourceManager as usual.
    /// </summary>
    internal static class Strings
    {
        private static readonly ResourceManager ResourceManager =
            new ResourceManager("GladosSwAddin.Resources.Strings", typeof(Strings).Assembly);

        private static string Get(string name) =>
            ResourceManager.GetString(name, CultureInfo.CurrentUICulture);

        public static string SettingsLinkText => Get(nameof(SettingsLinkText));
        public static string LinkReloadText => Get(nameof(LinkReloadText));
        public static string TooltipReloadFormat => Get(nameof(TooltipReloadFormat));
        public static string TooltipReloadNeverSynced => Get(nameof(TooltipReloadNeverSynced));
        public static string TabBoughtItemText => Get(nameof(TabBoughtItemText));
        public static string TabMadeItemText => Get(nameof(TabMadeItemText));
        public static string TabAssemblyText => Get(nameof(TabAssemblyText));
        public static string TabDrawingText => Get(nameof(TabDrawingText));
        public static string LabelDocumentNotSavedText => Get(nameof(LabelDocumentNotSavedText));
        public static string ButtonInitMadeText => Get(nameof(ButtonInitMadeText));
        public static string ButtonInitBoughtText => Get(nameof(ButtonInitBoughtText));
        public static string ButtonInitCustomerText => Get(nameof(ButtonInitCustomerText));
        public static string LabelCustomerText => Get(nameof(LabelCustomerText));
        public static string LabelCustomerPartNumberText => Get(nameof(LabelCustomerPartNumberText));
        public static string LabelCustomerRevisionText => Get(nameof(LabelCustomerRevisionText));
        public static string ButtonCustomerClearText => Get(nameof(ButtonCustomerClearText));
        public static string TooltipCustomerPartNumberRequired => Get(nameof(TooltipCustomerPartNumberRequired));
        public static string TooltipCustomerRequired => Get(nameof(TooltipCustomerRequired));
        public static string LabelSyncText => Get(nameof(LabelSyncText));
        public static string LabelStateText => Get(nameof(LabelStateText));
        public static string ButtonLoadFromGladosText => Get(nameof(ButtonLoadFromGladosText));
        public static string ButtonSaveToFileText => Get(nameof(ButtonSaveToFileText));
        public static string ButtonReloadFromFileText => Get(nameof(ButtonReloadFromFileText));
        public static string LabelArticleNameText => Get(nameof(LabelArticleNameText));
        public static string LabelOrderCode1Text => Get(nameof(LabelOrderCode1Text));
        public static string LabelOrderCode2Text => Get(nameof(LabelOrderCode2Text));
        public static string LabelGtinText => Get(nameof(LabelGtinText));
        public static string LabelEanText => Get(nameof(LabelEanText));
        public static string LabelTariffNumberText => Get(nameof(LabelTariffNumberText));
        public static string LabelNoteText => Get(nameof(LabelNoteText));
        public static string LabelAssemblyNameText => Get(nameof(LabelAssemblyNameText));
        public static string LabelWeblinkText => Get(nameof(LabelWeblinkText));
        public static string ButtonVisitWeblinkText => Get(nameof(ButtonVisitWeblinkText));
        public static string LabelManufacturerText => Get(nameof(LabelManufacturerText));
        public static string LabelArticleGroupsText => Get(nameof(LabelArticleGroupsText));
        public static string LabelMaterialText => Get(nameof(LabelMaterialText));
        public static string LabelMassText => Get(nameof(LabelMassText));
        public static string LabelPartNumberText => Get(nameof(LabelPartNumberText));
        public static string LabelRevisionText => Get(nameof(LabelRevisionText));
        public static string LabelConfigurationText => Get(nameof(LabelConfigurationText));
        public static string LabelSourceText => Get(nameof(LabelSourceText));
        public static string TooltipLoadFromGlados => Get(nameof(TooltipLoadFromGlados));
        public static string TooltipSaveToFile => Get(nameof(TooltipSaveToFile));
        public static string TooltipReloadFromFile => Get(nameof(TooltipReloadFromFile));
        public static string TooltipVisitWeblink => Get(nameof(TooltipVisitWeblink));
        public static string TooltipKindLocked => Get(nameof(TooltipKindLocked));
        public static string TooltipNotConnectedYet => Get(nameof(TooltipNotConnectedYet));
        public static string TooltipConnectedFormat => Get(nameof(TooltipConnectedFormat));
        public static string TooltipOfflineUnreachableFormat =>
            Get(nameof(TooltipOfflineUnreachableFormat));
        public static string TooltipOfflineNoCredentials => Get(nameof(TooltipOfflineNoCredentials));
        public static string TooltipOfflineTokenRejected => Get(nameof(TooltipOfflineTokenRejected));
        public static string TooltipOfflineServerErrorFormat => Get(nameof(TooltipOfflineServerErrorFormat));
        public static string StateActive => Get(nameof(StateActive));
        public static string StateInactive => Get(nameof(StateInactive));
        public static string StateReplaced => Get(nameof(StateReplaced));
        public static string StateNoCatalogueItem => Get(nameof(StateNoCatalogueItem));
        public static string SyncInSync => Get(nameof(SyncInSync));
        public static string SyncNotCheckedIn => Get(nameof(SyncNotCheckedIn));
        public static string SyncNotInGlados => Get(nameof(SyncNotInGlados));
        public static string SyncPossibleDuplicate => Get(nameof(SyncPossibleDuplicate));
        public static string SyncLookupFailed => Get(nameof(SyncLookupFailed));
        public static string SyncNoPartNumber => Get(nameof(SyncNoPartNumber));
        public static string AssemblyNotTrackedText => Get(nameof(AssemblyNotTrackedText));
        public static string PossibleDuplicateDetailFormat =>
            Get(nameof(PossibleDuplicateDetailFormat));
        public static string MessageBoxTitleGlados => Get(nameof(MessageBoxTitleGlados));
        public static string ErrorGladosLookupFailedFormat =>
            Get(nameof(ErrorGladosLookupFailedFormat));
        public static string ErrorGladosLookupFailedNoConnection =>
            Get(nameof(ErrorGladosLookupFailedNoConnection));
        public static string ErrorServerAnsweredFormat => Get(nameof(ErrorServerAnsweredFormat));
        public static string ErrorSignInFailedFormat => Get(nameof(ErrorSignInFailedFormat));
        public static string ErrorCouldNotReachServerFormat =>
            Get(nameof(ErrorCouldNotReachServerFormat));
        public static string ErrorServerUrlInvalidFormat =>
            Get(nameof(ErrorServerUrlInvalidFormat));
        public static string ErrorLoadFailedFormat => Get(nameof(ErrorLoadFailedFormat));
        public static string ErrorLoadFailedNoConnection =>
            Get(nameof(ErrorLoadFailedNoConnection));
        public static string ErrorWeblinkInvalidFormat => Get(nameof(ErrorWeblinkInvalidFormat));
        public static string ErrorWeblinkOpenFailedFormat =>
            Get(nameof(ErrorWeblinkOpenFailedFormat));
        public static string ErrorFailedToSaveDocumentFormat =>
            Get(nameof(ErrorFailedToSaveDocumentFormat));
        public static string ErrorSavedButFailedFormat => Get(nameof(ErrorSavedButFailedFormat));
        public static string ChangeSourceWarningTitle => Get(nameof(ChangeSourceWarningTitle));
        public static string ChangeSourceWarningMessage =>
            Get(nameof(ChangeSourceWarningMessage));
        public static string ErrorUnexpectedFormat => Get(nameof(ErrorUnexpectedFormat));
        public static string SettingsEnterServerAndPatWarning =>
            Get(nameof(SettingsEnterServerAndPatWarning));
        public static string SettingsPatValidFormat => Get(nameof(SettingsPatValidFormat));
        public static string SettingsPatTestFailedFormat =>
            Get(nameof(SettingsPatTestFailedFormat));
        public static string MadeItemInspectionStandaloneTooltip =>
            Get(nameof(MadeItemInspectionStandaloneTooltip));
        public static string MadeItemProcessesUnreadableHint =>
            Get(nameof(MadeItemProcessesUnreadableHint));
        public static string MadeItemStepsTitleText => Get(nameof(MadeItemStepsTitleText));
        public static string LabelMadeItemStepNoteText => Get(nameof(LabelMadeItemStepNoteText));
        public static string MadeItemStepRoleInspectionText =>
            Get(nameof(MadeItemStepRoleInspectionText));
        public static string ProcessRoutingRowTitleFormat =>
            Get(nameof(ProcessRoutingRowTitleFormat));
        public static string ProcessRoutingInspectionChoiceFormat =>
            Get(nameof(ProcessRoutingInspectionChoiceFormat));
        public static string TooltipProcessRoutingLink => Get(nameof(TooltipProcessRoutingLink));
        public static string TooltipProcessRoutingSplit => Get(nameof(TooltipProcessRoutingSplit));
        public static string TooltipProcessRoutingMoveUp =>
            Get(nameof(TooltipProcessRoutingMoveUp));
        public static string TooltipProcessRoutingMoveDown =>
            Get(nameof(TooltipProcessRoutingMoveDown));
        public static string ButtonPdmValidateText => Get(nameof(ButtonPdmValidateText));
        public static string TooltipPdmValidate => Get(nameof(TooltipPdmValidate));
        public static string PdmValidationNotChecked => Get(nameof(PdmValidationNotChecked));
        public static string PdmValidationChecking => Get(nameof(PdmValidationChecking));
        public static string PdmValidationReady => Get(nameof(PdmValidationReady));
        public static string PdmValidationReadyFormat => Get(nameof(PdmValidationReadyFormat));
        public static string PdmValidationOutcomeCreated =>
            Get(nameof(PdmValidationOutcomeCreated));
        public static string PdmValidationOutcomeRevised =>
            Get(nameof(PdmValidationOutcomeRevised));
        public static string PdmValidationOutcomeUpdated =>
            Get(nameof(PdmValidationOutcomeUpdated));
        public static string PdmValidationOutcomeClaimed =>
            Get(nameof(PdmValidationOutcomeClaimed));
        public static string PdmValidationOutcomeStatus =>
            Get(nameof(PdmValidationOutcomeStatus));
        public static string PdmValidationOutcomeUnknown =>
            Get(nameof(PdmValidationOutcomeUnknown));
        public static string PdmValidationBlocked => Get(nameof(PdmValidationBlocked));
        public static string PdmValidationUnreachable => Get(nameof(PdmValidationUnreachable));
        public static string PdmValidationIntegrationDisabled =>
            Get(nameof(PdmValidationIntegrationDisabled));
        public static string PdmValidationFileIdentityUnresolved =>
            Get(nameof(PdmValidationFileIdentityUnresolved));
        public static string PdmValidationPendingTooltipFormat =>
            Get(nameof(PdmValidationPendingTooltipFormat));
        public static string DownloadsReferenceWarning => Get(nameof(DownloadsReferenceWarning));
        public static string DownloadsReferenceHeader => Get(nameof(DownloadsReferenceHeader));
        public static string DownloadsReferenceAdvice => Get(nameof(DownloadsReferenceAdvice));
        public static string TooltipRequiredField => Get(nameof(TooltipRequiredField));
        public static string TooltipAssemblyNotTracked => Get(nameof(TooltipAssemblyNotTracked));
        public static string TooltipRequiredFabricationStep =>
            Get(nameof(TooltipRequiredFabricationStep));
        public static string PlaceholderPartNumberAssignedByPdm =>
            Get(nameof(PlaceholderPartNumberAssignedByPdm));
        public static string PlaceholderRevisionAssignedByPdm =>
            Get(nameof(PlaceholderRevisionAssignedByPdm));
        public static string LabelSourceItemText => Get(nameof(LabelSourceItemText));
        public static string ButtonSourceItemSelectText => Get(nameof(ButtonSourceItemSelectText));
        public static string ButtonSourceItemClearText => Get(nameof(ButtonSourceItemClearText));
        public static string SourceItemPickerTitleText => Get(nameof(SourceItemPickerTitleText));
        public static string LabelSourceItemSearchText => Get(nameof(LabelSourceItemSearchText));
        public static string LabelSourceItemTypeText => Get(nameof(LabelSourceItemTypeText));
        public static string LabelSourceItemDimensionText =>
            Get(nameof(LabelSourceItemDimensionText));
        public static string LabelSourceItemStatusText => Get(nameof(LabelSourceItemStatusText));
        public static string LabelSourceItemNoteText => Get(nameof(LabelSourceItemNoteText));
        public static string CheckBoxSourceItemShowArchivedText =>
            Get(nameof(CheckBoxSourceItemShowArchivedText));
        public static string ButtonSourceItemRefreshText => Get(nameof(ButtonSourceItemRefreshText));
        public static string ButtonSourceItemUseText => Get(nameof(ButtonSourceItemUseText));
        public static string ButtonSourceItemCancelText => Get(nameof(ButtonSourceItemCancelText));
        public static string ButtonSourceItemCreateText => Get(nameof(ButtonSourceItemCreateText));
        public static string TooltipSourceItemCreateDisconnected =>
            Get(nameof(TooltipSourceItemCreateDisconnected));
        public static string TooltipSourceItemCreateNoPermission =>
            Get(nameof(TooltipSourceItemCreateNoPermission));
        public static string ErrorSourceItemCreateRequiredFields =>
            Get(nameof(ErrorSourceItemCreateRequiredFields));
        public static string ErrorSourceItemCreateInvalidMass =>
            Get(nameof(ErrorSourceItemCreateInvalidMass));
        public static string ErrorSourceItemCreateForbidden =>
            Get(nameof(ErrorSourceItemCreateForbidden));
        public static string ErrorSourceItemCreateNoConnection =>
            Get(nameof(ErrorSourceItemCreateNoConnection));
        public static string ErrorSourceItemCreateDimensionInvalid =>
            Get(nameof(ErrorSourceItemCreateDimensionInvalid));
        public static string GroupBoxSourceItemCreateText => Get(nameof(GroupBoxSourceItemCreateText));
        public static string DimensionShapeBlockText => Get(nameof(DimensionShapeBlockText));
        public static string DimensionShapeProfileText => Get(nameof(DimensionShapeProfileText));
        public static string DimensionShapeTurnedPartText => Get(nameof(DimensionShapeTurnedPartText));
        public static string DimensionShapeTubeText => Get(nameof(DimensionShapeTubeText));
        public static string DimensionShapeCustomText => Get(nameof(DimensionShapeCustomText));
        public static string LabelDimensionWidthText => Get(nameof(LabelDimensionWidthText));
        public static string LabelDimensionHeightText => Get(nameof(LabelDimensionHeightText));
        public static string LabelDimensionThicknessText => Get(nameof(LabelDimensionThicknessText));
        public static string LabelDimensionLengthText => Get(nameof(LabelDimensionLengthText));
        public static string LabelDimensionDiameterText => Get(nameof(LabelDimensionDiameterText));
        public static string LabelDimensionWallThicknessText =>
            Get(nameof(LabelDimensionWallThicknessText));
        public static string LabelDimensionUnitFormat => Get(nameof(LabelDimensionUnitFormat));
        public static string LabelSourceItemBaseSizeFormat =>
            Get(nameof(LabelSourceItemBaseSizeFormat));
        public static string InfoSourceItemDuplicateText => Get(nameof(InfoSourceItemDuplicateText));
        public static string ButtonCalcText => Get(nameof(ButtonCalcText));
        public static string TooltipCalcRestoreMass => Get(nameof(TooltipCalcRestoreMass));
        public static string TooltipCalcRestoreMaterial => Get(nameof(TooltipCalcRestoreMaterial));
        public static string TooltipCalcAlreadyCalculated => Get(nameof(TooltipCalcAlreadyCalculated));
        public static string ErrorRestoreFailedFormat => Get(nameof(ErrorRestoreFailedFormat));
        public static string SettingsFormTitleText => Get(nameof(SettingsFormTitleText));
        public static string SettingsTabAuthText => Get(nameof(SettingsTabAuthText));
        public static string SettingsLabelServerUrlText => Get(nameof(SettingsLabelServerUrlText));
        public static string SettingsLabelPatText => Get(nameof(SettingsLabelPatText));
        public static string SettingsButtonTestPatText => Get(nameof(SettingsButtonTestPatText));
        public static string SettingsTabFieldMappingText =>
            Get(nameof(SettingsTabFieldMappingText));
        public static string SettingsColumnKindText => Get(nameof(SettingsColumnKindText));
        public static string SettingsColumnFieldText => Get(nameof(SettingsColumnFieldText));
        public static string SettingsColumnOwnerText => Get(nameof(SettingsColumnOwnerText));
        public static string SettingsColumnPropertyText => Get(nameof(SettingsColumnPropertyText));
        public static string SettingsColumnUpdateInPlaceText =>
            Get(nameof(SettingsColumnUpdateInPlaceText));
        public static string SettingsButtonSaveText => Get(nameof(SettingsButtonSaveText));
        public static string SettingsButtonCancelText => Get(nameof(SettingsButtonCancelText));
        public static string SettingsKindMadeText => Get(nameof(SettingsKindMadeText));
        public static string SettingsKindBoughtText => Get(nameof(SettingsKindBoughtText));
        public static string SettingsYesText => Get(nameof(SettingsYesText));
        public static string SettingsNoText => Get(nameof(SettingsNoText));
        public static string SettingsFieldMappingDefaultsText =>
            Get(nameof(SettingsFieldMappingDefaultsText));
        public static string SettingsFieldMappingSyncedFormat =>
            Get(nameof(SettingsFieldMappingSyncedFormat));
        public static string SettingsFeatureTreeTemplatesFormat =>
            Get(nameof(SettingsFeatureTreeTemplatesFormat));
        public static string SettingsInsecureServerUrlWarningFormat =>
            Get(nameof(SettingsInsecureServerUrlWarningFormat));
        public static string SettingsLinkOpenLogFolderText => Get(nameof(SettingsLinkOpenLogFolderText));
        public static string ErrorOpenLogFolderFailedFormat => Get(nameof(ErrorOpenLogFolderFailedFormat));
        public static string ButtonEmbedImportedComponentsText =>
            Get(nameof(ButtonEmbedImportedComponentsText));
        public static string TooltipEmbedImportedComponents =>
            Get(nameof(TooltipEmbedImportedComponents));
        public static string EmbedImportedComponentsConfirmTitle =>
            Get(nameof(EmbedImportedComponentsConfirmTitle));
        public static string EmbedImportedComponentsConfirmMessage =>
            Get(nameof(EmbedImportedComponentsConfirmMessage));
        public static string EmbedImportedComponentsDoneFormat =>
            Get(nameof(EmbedImportedComponentsDoneFormat));
        public static string EmbedImportedComponentsNothingFound =>
            Get(nameof(EmbedImportedComponentsNothingFound));
        public static string EmbedImportedComponentsReadOnly =>
            Get(nameof(EmbedImportedComponentsReadOnly));
        public static string EmbedImportedComponentsFailedFormat =>
            Get(nameof(EmbedImportedComponentsFailedFormat));
        public static string EmbedImportedComponentsSaveFailed =>
            Get(nameof(EmbedImportedComponentsSaveFailed));
        public static string EmbedImportedComponentsCheckInCommentFormat =>
            Get(nameof(EmbedImportedComponentsCheckInCommentFormat));
        public static string EmbedImportedComponentsNotCheckedInFormat =>
            Get(nameof(EmbedImportedComponentsNotCheckedInFormat));
        public static string EmbedImportedComponentsStepBreakLink =>
            Get(nameof(EmbedImportedComponentsStepBreakLink));
        public static string EmbedImportedComponentsStepSaveToWorkFolder =>
            Get(nameof(EmbedImportedComponentsStepSaveToWorkFolder));
        public static string EmbedImportedComponentsStepDissolve =>
            Get(nameof(EmbedImportedComponentsStepDissolve));
        public static string EmbedImportedComponentsStepMakeVirtual =>
            Get(nameof(EmbedImportedComponentsStepMakeVirtual));
        public static string DataPortalFormTitleText => Get(nameof(DataPortalFormTitleText));
        public static string LabelDataPortalSearchText => Get(nameof(LabelDataPortalSearchText));
        public static string ButtonDataPortalSearchText => Get(nameof(ButtonDataPortalSearchText));
        public static string ButtonDataPortalTakeText => Get(nameof(ButtonDataPortalTakeText));
        public static string ButtonDataPortalCancelText => Get(nameof(ButtonDataPortalCancelText));
        public static string ButtonDataPortalText => Get(nameof(ButtonDataPortalText));
        public static string ColumnDataPortalTakeText => Get(nameof(ColumnDataPortalTakeText));
        public static string ColumnDataPortalFieldText => Get(nameof(ColumnDataPortalFieldText));
        public static string ColumnDataPortalCurrentText => Get(nameof(ColumnDataPortalCurrentText));
        public static string ColumnDataPortalPortalText => Get(nameof(ColumnDataPortalPortalText));
        public static string DataPortalSearchingText => Get(nameof(DataPortalSearchingText));
        public static string DataPortalNoHitsText => Get(nameof(DataPortalNoHitsText));
        public static string DataPortalHitsFormat => Get(nameof(DataPortalHitsFormat));
        public static string DataPortalSearchFailedFormat => Get(nameof(DataPortalSearchFailedFormat));
        public static string DataPortalSearchFailedNoConnection => Get(nameof(DataPortalSearchFailedNoConnection));
        public static string DataPortalExistingItemFormat => Get(nameof(DataPortalExistingItemFormat));
        public static string DataPortalExistingItemNoPartNumber => Get(nameof(DataPortalExistingItemNoPartNumber));
        public static string DataPortalNoSupplierFormat => Get(nameof(DataPortalNoSupplierFormat));
        public static string DataPortalManufacturerFormat => Get(nameof(DataPortalManufacturerFormat));
        public static string DataPortalPartNumberFormat => Get(nameof(DataPortalPartNumberFormat));
        public static string DataPortalOrderNumberFormat => Get(nameof(DataPortalOrderNumberFormat));
        public static string DataPortalTypeNumberFormat => Get(nameof(DataPortalTypeNumberFormat));
        public static string DataPortalProductFamilyFormat => Get(nameof(DataPortalProductFamilyFormat));
        public static string DataPortalNoPictureText => Get(nameof(DataPortalNoPictureText));
        public static string LinkDataPortalPartPageText => Get(nameof(LinkDataPortalPartPageText));
        public static string LinkDataPortalFoundFormat => Get(nameof(LinkDataPortalFoundFormat));
        public static string LabelDataPortalLinkedFormat => Get(nameof(LabelDataPortalLinkedFormat));
        public static string TooltipDataPortalSearch => Get(nameof(TooltipDataPortalSearch));
        public static string TooltipDataPortalNoPermission => Get(nameof(TooltipDataPortalNoPermission));
        public static string TooltipDataPortalFound => Get(nameof(TooltipDataPortalFound));
        public static string SaveConflictFormTitleText => Get(nameof(SaveConflictFormTitleText));
        public static string LabelSaveConflictIntroText => Get(nameof(LabelSaveConflictIntroText));
        public static string ColumnSaveConflictFieldText => Get(nameof(ColumnSaveConflictFieldText));
        public static string ColumnSaveConflictFileText => Get(nameof(ColumnSaveConflictFileText));
        public static string ColumnSaveConflictMineText => Get(nameof(ColumnSaveConflictMineText));
        public static string ButtonSaveConflictUseMineText => Get(nameof(ButtonSaveConflictUseMineText));
        public static string ButtonSaveConflictKeepFileText => Get(nameof(ButtonSaveConflictKeepFileText));
        public static string ButtonSaveConflictCancelText => Get(nameof(ButtonSaveConflictCancelText));
        public static string SaveConflictEmptyValueText => Get(nameof(SaveConflictEmptyValueText));
        public static string SaveConflictUnreadableValueText => Get(nameof(SaveConflictUnreadableValueText));
        public static string FieldDataPortalPartText => Get(nameof(FieldDataPortalPartText));
        public static string TooltipReloadFromFileChangedOutside => Get(nameof(TooltipReloadFromFileChangedOutside));
        public static string SettingsVersionFormat => Get(nameof(SettingsVersionFormat));
        public static string BackendVersionWarningShort => Get(nameof(BackendVersionWarningShort));
        public static string BackendVersionWarningFormat => Get(nameof(BackendVersionWarningFormat));
    }
}

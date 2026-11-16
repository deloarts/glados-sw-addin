using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;
using GladosSwAddin.Api;
using GladosSwAddin.Helpers;
using GladosSwAddin.Models;
using GladosSwAddin.Resources;
using GladosSwAddin.Services;

namespace GladosSwAddin.Forms
{
    public partial class SettingsForm : Form
    {
        private readonly SettingsService _settingsService = ApplicationServices.Settings;

        private readonly CredentialService _credentialService = ApplicationServices.Credentials;

        private readonly FieldMappingService _fieldMappingService = ApplicationServices.FieldMapping;

        private Settings _settings;

        public SettingsForm()
        {
            InitializeComponent();
            ApplyLocalizedStaticText();

            LoadSettings();
            LoadFieldMapping();
        }

        /// <summary>The designer file carries English placeholders only; every visible caption is
        /// replaced here from Strings so the form follows the UI culture like the task pane does.</summary>
        private void ApplyLocalizedStaticText()
        {
            Text = Strings.SettingsFormTitleText;
            tabPageAuth.Text = Strings.SettingsTabAuthText;
            labelSettingsAuthServerURL.Text = Strings.SettingsLabelServerUrlText;
            labelSettingsAuthPAT.Text = Strings.SettingsLabelPatText;
            buttonSettingsAuthTestPAT.Text = Strings.SettingsButtonTestPatText;
            tabPageFieldMapping.Text = Strings.SettingsTabFieldMappingText;
            columnFieldMappingKind.HeaderText = Strings.SettingsColumnKindText;
            columnFieldMappingField.HeaderText = Strings.SettingsColumnFieldText;
            columnFieldMappingOwner.HeaderText = Strings.SettingsColumnOwnerText;
            columnFieldMappingProperty.HeaderText = Strings.SettingsColumnPropertyText;
            columnFieldMappingUpdateInPlace.HeaderText = Strings.SettingsColumnUpdateInPlaceText;
            buttonSave.Text = Strings.SettingsButtonSaveText;
            buttonCancel.Text = Strings.SettingsButtonCancelText;
            linkLabelOpenLogFolder.Text = Strings.SettingsLinkOpenLogFolderText;
            labelSettingsVersion.Text = string.Format(
                CultureInfo.CurrentCulture, Strings.SettingsVersionFormat, AddinInfo.Version);
        }

        private void LoadSettings()
        {
            _settings = _settingsService.Load();

            textBoxSettingsAuthServerURL.Text = _settings.ServerUrl;
            textBoxSettingsAuthPAT.Text = _credentialService.GetPat() ?? string.Empty;
        }

        /// <summary>Populates the read-only grid from FieldMappingService.Entries — whatever's
        /// currently in effect for the configured server (a synced mapping, a previously-synced
        /// one still valid for it, or the compiled-in defaults; see FieldMappingService) — and
        /// sets the provenance line and the two feature-tree templates above it. This tab never
        /// triggers a sync itself: syncing happens after a successful connect
        /// (GladosTaskPaneControl.RefreshAuthStateAsync), not from Settings, which only ever
        /// displays. A label row (article_group_names, manufacturer_name, process_names,
        /// source_item_name) is a normal row here now, like any other field — docs/FIELD_OWNERSHIP.md
        /// section 1 row 3 — no longer inserted or greyed specially.</summary>
        private void LoadFieldMapping()
        {
            dataGridViewFieldMapping.Rows.Clear();

            foreach (var kind in new[] { ItemKind.Made, ItemKind.Bought })
            {
                var kindText = kind == ItemKind.Made ? Strings.SettingsKindMadeText : Strings.SettingsKindBoughtText;

                foreach (var row in _fieldMappingService.Entries(kind))
                {
                    var entry = row.Entry;
                    var updateInPlaceText = entry.UpdateInPlace.HasValue
                        ? (entry.UpdateInPlace.Value ? Strings.SettingsYesText : Strings.SettingsNoText)
                        : string.Empty;

                    int rowIndex = dataGridViewFieldMapping.Rows.Add(
                        kindText,
                        entry.Field,
                        entry.Owner.ToDisplayString(),
                        entry.PropertyName ?? string.Empty,
                        updateInPlaceText
                    );

                    // Greyed rather than hidden: a field the server sent that this add-in build
                    // doesn't recognize yet is still worth showing, per docs/FIELD_OWNERSHIP.md's
                    // tolerant-parsing rule — just visually distinguished from the fields this
                    // version actually understands.
                    if (!row.IsKnown)
                        dataGridViewFieldMapping.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Gray;
                }
            }

            labelFieldMappingProvenance.Text = _fieldMappingService.IsDefaults
                ? Strings.SettingsFieldMappingDefaultsText
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.SettingsFieldMappingSyncedFormat,
                    _fieldMappingService.ServerUrl,
                    _fieldMappingService.SyncedAt.Value.ToLocalTime()
                );

            labelFeatureTreeTemplates.Text = string.Format(
                CultureInfo.CurrentCulture,
                Strings.SettingsFeatureTreeTemplatesFormat,
                _fieldMappingService.MadeFeatureTreeTemplate,
                _fieldMappingService.BoughtFeatureTreeTemplate
            );
        }

        #region Callbacks

        private async void buttonSettingsAuthTestPAT_Click(object sender, EventArgs e)
        {
            var serverUrl = textBoxSettingsAuthServerURL.Text.Trim();
            var pat = textBoxSettingsAuthPAT.Text.Trim();

            if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(pat))
            {
                MessageBox.Show(
                    Strings.SettingsEnterServerAndPatWarning,
                    Strings.MessageBoxTitleGlados,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            if (!ConfirmInsecureServerUrl(serverUrl))
                return;

            buttonSettingsAuthTestPAT.Enabled = false;
            try
            {
                using (var api = new GladosApi(serverUrl, pat))
                {
                    var user = await api.Auth.TestPersonalAccessTokenAsync();

                    MessageBox.Show(
                        string.Format(CultureInfo.CurrentCulture, Strings.SettingsPatValidFormat, user.FullName, user.Username),
                        Strings.MessageBoxTitleGlados,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
            catch (GladosApiException ex)
            {
                Log.Warning("PAT test rejected by the server", ex);
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Strings.SettingsPatTestFailedFormat, ex.Message),
                    Strings.MessageBoxTitleGlados,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (HttpRequestException ex)
            {
                Log.Warning("PAT test could not reach the server", ex);
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Strings.ErrorCouldNotReachServerFormat, ex.Message),
                    Strings.MessageBoxTitleGlados,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (FormatException ex)
            {
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Strings.ErrorServerUrlInvalidFormat, ex.Message),
                    Strings.MessageBoxTitleGlados,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            // Last line of defence for an async void handler: anything else (an HttpClient
            // timeout surfaces as TaskCanceledException, for one) would otherwise escape onto
            // the SolidWorks message loop.
            catch (Exception ex)
            {
                Log.Error("PAT test failed unexpectedly", ex);
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Strings.ErrorUnexpectedFormat, ex.Message),
                    Strings.MessageBoxTitleGlados,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                buttonSettingsAuthTestPAT.Enabled = true;
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (!ConfirmInsecureServerUrl(textBoxSettingsAuthServerURL.Text.Trim()))
                return;

            _settings.ServerUrl = textBoxSettingsAuthServerURL.Text.Trim();

            _settingsService.Save(_settings);

            _credentialService.SavePat(textBoxSettingsAuthPAT.Text.Trim());

            ApplicationServices.RefreshApi();

            DialogResult = DialogResult.OK;
            Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void linkLabelOpenLogFolder_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(Log.Directory);
                Process.Start("explorer.exe", "\"" + Log.Directory + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Strings.ErrorOpenLogFolderFailedFormat, ex.Message),
                    Strings.MessageBoxTitleGlados,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        #endregion

        /// <summary>Warns before a personal access token would travel over plain HTTP and lets
        /// the user back out. A warning rather than a hard block: an internal test server
        /// without TLS is a legitimate setup. Loopback hosts never leave the machine, so they
        /// pass silently, as does anything that isn't a parseable http URL (the request path
        /// reports those itself).</summary>
        private static bool ConfirmInsecureServerUrl(string serverUrl)
        {
            Uri uri;
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttp
                || uri.IsLoopback)
                return true;

            return MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Strings.SettingsInsecureServerUrlWarningFormat, serverUrl),
                    Strings.MessageBoxTitleGlados,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2
                ) == DialogResult.Yes;
        }
    }
}

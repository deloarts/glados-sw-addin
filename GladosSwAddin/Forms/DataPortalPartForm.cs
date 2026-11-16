using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using GladosSwAddin.Api;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;
using GladosSwAddin.Resources;
using GladosSwAddin.Services;

namespace GladosSwAddin.Forms
{
    /// <summary>
    /// Modal EPLAN Data Portal lookup for the bought tab, opened from
    /// GladosTaskPaneControl.OpenDataPortalAsync via ShowDialog(this). Lists the parts an order
    /// code names (an exact match first, the portal's ranked search when that finds nothing),
    /// shows the selected part with its picture, and compares its values with the tab's
    /// current ones (DataPortalTakeover), one tick box per field. Take hands the result back
    /// as Result; nothing is written here. The caller shows it as unsaved edits and the normal
    /// Save writes the document, Glados.EdpPartId included, which PDM ingest then uses to link
    /// the catalogue item and fetch its picture. This add-in never creates the catalogue item
    /// itself.
    /// </summary>
    public partial class DataPortalPartForm : Form
    {
        private readonly BoughtItemProperties _current;

        private readonly Func<int?, string> _manufacturerName;

        private readonly Func<IEnumerable<int>, string> _articleGroupNames;

        /// <summary>Hits the task pane already fetched in the background, shown instead of a
        /// first search; null searches the initial query on Load.</summary>
        private readonly IReadOnlyList<EdpSearchHitResponse> _initialHits;

        /// <summary>Pictures already fetched this session of the dialog, by part id; null
        /// records a part without one, so it isn't asked for again. Disposed with the
        /// form.</summary>
        private readonly Dictionary<int, Image> _pictures = new Dictionary<int, Image>();

        /// <summary>Bumped by every search and every selection, so an answer that arrives after
        /// the user has moved on is dropped instead of painted over the newer state.</summary>
        private int _searchGeneration;

        private int _selectionGeneration;

        /// <summary>The tab's values with the chosen fields taken from the part and the part
        /// linked, once the dialog closes with DialogResult.OK; null on Cancel.</summary>
        public BoughtItemProperties Result { get; private set; }

        /// <param name="current">The bought tab's values as shown, unsaved edits included.</param>
        /// <param name="query">Prefilled into the search box, usually Order Code 1.</param>
        /// <param name="hits">Hits already fetched for query, or null to search on Load.</param>
        /// <param name="manufacturerName">Resolves a supplier id to its name for display.</param>
        /// <param name="articleGroupNames">Resolves article group ids to one display text.</param>
        public DataPortalPartForm(
            BoughtItemProperties current,
            string query,
            IReadOnlyList<EdpSearchHitResponse> hits,
            Func<int?, string> manufacturerName,
            Func<IEnumerable<int>, string> articleGroupNames)
        {
            InitializeComponent();

            _current = current ?? new BoughtItemProperties();
            _manufacturerName = manufacturerName;
            _articleGroupNames = articleGroupNames;
            _initialHits = hits;

            ApplyLocalizedStaticText();
            textBoxSearch.Text = query ?? string.Empty;
            ShowPart(null);

            Disposed += (s, e) =>
            {
                foreach (var image in _pictures.Values)
                    image?.Dispose();
                _pictures.Clear();
            };
        }

        private void ApplyLocalizedStaticText()
        {
            Text = Strings.DataPortalFormTitleText;
            labelSearch.Text = Strings.LabelDataPortalSearchText;
            buttonSearch.Text = Strings.ButtonDataPortalSearchText;
            columnTake.HeaderText = Strings.ColumnDataPortalTakeText;
            columnField.HeaderText = Strings.ColumnDataPortalFieldText;
            columnCurrent.HeaderText = Strings.ColumnDataPortalCurrentText;
            columnPortal.HeaderText = Strings.ColumnDataPortalPortalText;
            labelNoPicture.Text = Strings.DataPortalNoPictureText;
            linkLabelPartPage.Text = Strings.LinkDataPortalPartPageText;
            buttonTake.Text = Strings.ButtonDataPortalTakeText;
            buttonCancel.Text = Strings.ButtonDataPortalCancelText;
            labelStatus.Text = string.Empty;
        }

        private async void DataPortalPartForm_Load(object sender, EventArgs e)
        {
            if (_initialHits != null)
                ShowHits(_initialHits, null);
            else
                await SearchAsync(textBoxSearch.Text);
        }

        #region Search

        private async void buttonSearch_Click(object sender, EventArgs e)
        {
            await SearchAsync(textBoxSearch.Text);
        }

        /// <summary>Enter searches rather than taking the selected part, which is why the form
        /// has no AcceptButton.</summary>
        private async void textBoxSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            await SearchAsync(textBoxSearch.Text);
        }

        /// <summary>An exact match on the number first, narrowed to the tab's manufacturer when
        /// one is chosen, so a typed order code lands on its part; the ranked search only when
        /// the number names nothing, e.g. for a part name.</summary>
        private async Task SearchAsync(string query)
        {
            query = (query ?? string.Empty).Trim();
            if (query.Length < 2)
            {
                ShowHits(new List<EdpSearchHitResponse>(), null);
                return;
            }

            int generation = ++_searchGeneration;
            labelStatus.Text = Strings.DataPortalSearchingText;
            buttonSearch.Enabled = false;

            List<EdpSearchHitResponse> hits = null;
            string error = null;
            try
            {
                var matched = await ApplicationServices.Api.DataPortal.MatchAsync(query, _current.ManufacturerId);
                hits = matched?.Hits ?? new List<EdpSearchHitResponse>();
                if (hits.Count == 0)
                {
                    var searched = await ApplicationServices.Api.DataPortal.SearchAsync(query);
                    hits = searched?.Hits ?? new List<EdpSearchHitResponse>();
                }
            }
            catch (GladosApiException ex)
            {
                error = string.Format(CultureInfo.CurrentCulture, Strings.DataPortalSearchFailedFormat, ex.Message);
            }
            catch (HttpRequestException)
            {
                error = Strings.DataPortalSearchFailedNoConnection;
            }

            if (IsDisposed || generation != _searchGeneration)
                return;

            buttonSearch.Enabled = true;
            ShowHits(hits ?? new List<EdpSearchHitResponse>(), error);
        }

        private void ShowHits(IReadOnlyList<EdpSearchHitResponse> hits, string error)
        {
            listBoxHits.BeginUpdate();
            listBoxHits.Items.Clear();
            foreach (var hit in hits.Where(h => h?.Part != null && h.Draft != null))
                listBoxHits.Items.Add(new HitEntry(hit));
            listBoxHits.EndUpdate();

            if (error != null)
                labelStatus.Text = error;
            else if (listBoxHits.Items.Count == 0)
                labelStatus.Text = Strings.DataPortalNoHitsText;
            else
                labelStatus.Text = string.Format(
                    CultureInfo.CurrentCulture, Strings.DataPortalHitsFormat, listBoxHits.Items.Count);

            if (listBoxHits.Items.Count > 0)
                listBoxHits.SelectedIndex = 0;
            else
                ShowPart(null);
        }

        #endregion

        #region Selected part

        private void listBoxHits_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowPart((listBoxHits.SelectedItem as HitEntry)?.Hit);
        }

        private EdpSearchHitResponse SelectedHit => (listBoxHits.SelectedItem as HitEntry)?.Hit;

        private void ShowPart(EdpSearchHitResponse hit)
        {
            int generation = ++_selectionGeneration;
            var part = hit?.Part;
            var draft = hit?.Draft;

            labelDetailDesignation.Text = part?.Designation ?? draft?.ArticleName ?? string.Empty;
            labelDetailManufacturer.Text = Detail(Strings.DataPortalManufacturerFormat, part?.ManufacturerName);
            labelDetailOrderNumber.Text = Detail(Strings.DataPortalOrderNumberFormat, part?.OrderNumber);
            labelDetailTypeNumber.Text = Detail(Strings.DataPortalTypeNumberFormat, part?.TypeNumber);
            labelDetailPartNumber.Text = Detail(Strings.DataPortalPartNumberFormat, part?.PartNumber);
            labelDetailProductFamily.Text = Detail(
                Strings.DataPortalProductFamilyFormat, draft?.ProductFamilyName ?? part?.ProductFamilyName);
            linkLabelPartPage.Visible = !string.IsNullOrWhiteSpace(part?.PartPageUrl);

            var existing = hit?.ExistingItem;
            labelExistingItem.Text = existing == null
                ? string.Empty
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.DataPortalExistingItemFormat,
                    string.IsNullOrWhiteSpace(existing.PartNumber)
                        ? Strings.DataPortalExistingItemNoPartNumber
                        : existing.PartNumber,
                    existing.ArticleName);

            var manufacturerCode = draft?.ManufacturerCode ?? part?.ManufacturerCode;
            labelNoSupplier.Text = draft != null && !draft.ManufacturerId.HasValue
                    && !string.IsNullOrWhiteSpace(manufacturerCode)
                ? string.Format(CultureInfo.CurrentCulture, Strings.DataPortalNoSupplierFormat, manufacturerCode)
                : string.Empty;

            FillComparison(draft);
            buttonTake.Enabled = draft != null;

            SetPicture(null, hit != null);
            if (part != null)
                LoadPictureAsync(part.PartId, generation);
        }

        private static string Detail(string format, string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Format(CultureInfo.CurrentCulture, format, value);

        private void FillComparison(EdpItemDraftResponse draft)
        {
            dataGridViewFields.Rows.Clear();
            if (draft == null)
                return;

            foreach (var row in DataPortalTakeover.Compare(_current, draft, _manufacturerName, _articleGroupNames))
            {
                int index = dataGridViewFields.Rows.Add(
                    row.TakeByDefault, FieldLabel(row.Field), row.Current ?? string.Empty, row.Portal ?? string.Empty);
                var gridRow = dataGridViewFields.Rows[index];
                gridRow.Tag = row.Field;

                if (!row.Takeable)
                {
                    // Nothing to take: the part has no value here or it is already the same.
                    gridRow.Cells[columnTake.Index].ReadOnly = true;
                    gridRow.DefaultCellStyle.ForeColor = SystemColors.GrayText;
                }
            }
        }

        private static string FieldLabel(DataPortalField field)
        {
            switch (field)
            {
                case DataPortalField.ArticleName: return Strings.LabelArticleNameText;
                case DataPortalField.OrderCode1: return Strings.LabelOrderCode1Text;
                case DataPortalField.OrderCode2: return Strings.LabelOrderCode2Text;
                case DataPortalField.Ean: return Strings.LabelEanText;
                case DataPortalField.Weblink: return Strings.LabelWeblinkText;
                case DataPortalField.Manufacturer: return Strings.LabelManufacturerText;
                case DataPortalField.ArticleGroups: return Strings.LabelArticleGroupsText;
                default: return field.ToString();
            }
        }

        /// <summary>Fetches the picture through Glados (never from the portal directly) once per
        /// part; a part without one (404) is remembered as such. Any other failure just leaves
        /// the "No picture" placeholder, since the picture is a convenience.</summary>
        private async void LoadPictureAsync(int partId, int generation)
        {
            if (!_pictures.TryGetValue(partId, out var image))
            {
                image = await FetchPictureAsync(partId);
                if (IsDisposed)
                {
                    image?.Dispose();
                    return;
                }

                if (_pictures.ContainsKey(partId))
                    image?.Dispose();
                else
                    _pictures[partId] = image;
                image = _pictures[partId];
            }

            if (generation == _selectionGeneration)
                SetPicture(image, true);
        }

        private static async Task<Image> FetchPictureAsync(int partId)
        {
            try
            {
                var bytes = await ApplicationServices.Api.DataPortal.GetPictureAsync(partId);
                using (var stream = new MemoryStream(bytes))
                using (var loaded = Image.FromStream(stream))
                {
                    // Image.FromStream keeps the stream alive for the image's lifetime, so the
                    // picture is copied into a Bitmap that outlives this block.
                    return new Bitmap(loaded);
                }
            }
            catch (GladosApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (GladosApiException ex)
            {
                Log.Warning($"Data Portal picture of part #{partId} not loaded", ex);
                return null;
            }
            catch (HttpRequestException ex)
            {
                Log.Warning($"Data Portal picture of part #{partId} not loaded", ex);
                return null;
            }
            catch (ArgumentException ex)
            {
                // Image.FromStream rejects bytes that are no image.
                Log.Warning($"Data Portal picture of part #{partId} not readable", ex);
                return null;
            }
        }

        /// <summary>The form owns the cached images, so the picture box only borrows one and
        /// never disposes what it showed before.</summary>
        private void SetPicture(Image image, bool partShown)
        {
            pictureBoxPart.Image = image;
            labelNoPicture.Visible = partShown && image == null;
        }

        private void linkLabelPartPage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var url = SelectedHit?.Part?.PartPageUrl;
            if (!Uri.TryCreate(url ?? string.Empty, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return;

            try
            {
                System.Diagnostics.Process.Start(uri.AbsoluteUri);
            }
            catch (Exception ex)
                when (!(ex is OutOfMemoryException || ex is StackOverflowException))
            {
                Log.Warning("Opening the Data Portal page failed", ex);
            }
        }

        #endregion

        private void buttonTake_Click(object sender, EventArgs e)
        {
            var draft = SelectedHit?.Draft;
            if (draft == null)
                return;

            // Commits a tick box the user is still in the middle of editing.
            dataGridViewFields.EndEdit();

            var chosen = dataGridViewFields.Rows
                .Cast<DataGridViewRow>()
                .Where(r => r.Tag is DataPortalField
                    && !r.Cells[columnTake.Index].ReadOnly
                    && Equals(r.Cells[columnTake.Index].Value, true))
                .Select(r => (DataPortalField)r.Tag)
                .ToList();

            Result = DataPortalTakeover.Apply(_current, draft, chosen);
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>One line of the hit list: order number, designation and manufacturer, the
        /// three things that tell parts sharing a number apart.</summary>
        private sealed class HitEntry
        {
            public HitEntry(EdpSearchHitResponse hit)
            {
                Hit = hit;
            }

            public EdpSearchHitResponse Hit { get; }

            public override string ToString()
            {
                var part = Hit.Part;
                var parts = new[] { part.OrderNumber ?? part.TypeNumber, part.Designation, part.ManufacturerName }
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                return string.Join(" · ", parts);
            }
        }
    }
}

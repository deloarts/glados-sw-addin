using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GladosSwAddin.Api.Models;
using GladosSwAddin.Models;
using GladosSwAddin.Resources;
using GladosSwAddin.Services;
using SolidWorks.Interop.sldworks;

namespace GladosSwAddin
{
    /// <summary>
    /// The customer fields of the Made tab: the customer data of a made part (customer, customer
    /// part number, customer revision, docs/FIELD_OWNERSHIP.md section 3.2). A customer item is a
    /// made item with a customer, not a kind of its own, so these fields are part of the
    /// made-item form: PopulateMadeItemFields fills them, ReadMadeItemFromUi reads them, and the
    /// Made tab's Save writes them. They sit in panelMadeItemCustomer under the article name and
    /// show only for a customer item, that is a file that already holds customer data or one
    /// the engineer started with Create Customer Item. A made assembly never shows them, since
    /// it is not a catalogue item (see ApplySourceGateAsync).
    /// </summary>
    public partial class GladosTaskPaneControl
    {
        private readonly List<CustomerLookupResponse> _customerChoices = new List<CustomerLookupResponse>();

        /// <summary>The document Create Customer Item was clicked for. It keeps the customer
        /// fields showing for that document even before it holds customer data, and lets every
        /// populate pre-select the customer guessed from its folder. Bound to the model so
        /// switching to another document drops it, compared by reference like
        /// OnActiveDocChangedAsync does.</summary>
        private ModelDoc2 _customerItemRequestedFor;

        private bool _customerFieldsShown;

        /// <summary>Called once from the constructor.</summary>
        private void WireCustomerFields()
        {
            CaptureBaseLabelText(labelCustomer);
            CaptureBaseLabelText(labelCustomerPartNumber);
            CaptureBaseLabelText(labelCustomerRevision);

            comboBoxCustomer.SelectionChangeCommitted += (s, e) => UpdateCustomerDirtyMarkers();
            textBoxCustomerPartNumber.TextChanged += (s, e) => UpdateCustomerDirtyMarkers();
            textBoxCustomerRevision.TextChanged += (s, e) => UpdateCustomerDirtyMarkers();
        }

        /// <summary>The customer fields' share of ApplyLocalizedStaticText, called from inside its
        /// UI-thread block.</summary>
        private void ApplyCustomerLocalizedText()
        {
            buttonInitCustomer.Text = Strings.ButtonInitCustomerText;
            buttonCustomerClear.Text = Strings.ButtonCustomerClearText;
            labelCustomer.Text = Strings.LabelCustomerText;
            labelCustomerPartNumber.Text = Strings.LabelCustomerPartNumberText;
            labelCustomerRevision.Text = Strings.LabelCustomerRevisionText;

            foreach (var label in new[] { labelCustomer, labelCustomerPartNumber, labelCustomerRevision })
                _baseLabelText[label] = label.Text;
        }

        /// <summary>Same reasoning as SetManufacturerSelection: the choices are the snapshot's
        /// non-archived customers plus customerId itself when it names an archived one, and a
        /// placeholder entry only when customerId is not in the snapshot at all, so a stale or
        /// empty cache never blanks the combo and the next Save never wipes the link.</summary>
        private void SetCustomerSelection(int? customerId, string customerName)
        {
            var keep = customerId.HasValue ? new[] { customerId.Value } : Array.Empty<int>();

            _customerChoices.Clear();
            _customerChoices.AddRange(VisibleRows(
                ApplicationServices.Lookups.Current.Customers, c => c.Archived, c => c.Id, c => c.Name, keep));

            if (customerId.HasValue && !_customerChoices.Any(c => c.Id == customerId.Value))
            {
                _customerChoices.Add(
                    new CustomerLookupResponse
                    {
                        Id = customerId.Value,
                        Name = PlaceholderLabel(customerName, customerId.Value),
                    }
                );
            }

            // Reassigned rather than mutated in place, see BindManufacturerChoices.
            comboBoxCustomer.DataSource = null;
            comboBoxCustomer.DisplayMember = "Name";
            comboBoxCustomer.ValueMember = "Id";
            comboBoxCustomer.DataSource = _customerChoices;

            if (customerId.HasValue)
                comboBoxCustomer.SelectedValue = customerId.Value;
            else
                comboBoxCustomer.SelectedIndex = -1;

            buttonCustomerClear.Enabled = customerId.HasValue;
        }

        private int? SelectedCustomerId => (comboBoxCustomer.SelectedItem as CustomerLookupResponse)?.Id;

        /// <summary>Empty string when there is no customer (clear the label), the customer's
        /// name when the lookup snapshot can resolve it (archived rows included), null when it
        /// can't — see SwCustomPropertyService.WriteCustomer for what null means.</summary>
        private string ResolveCustomerLabel(int? customerId)
        {
            if (customerId == null)
                return string.Empty;

            return ApplicationServices.Lookups.Current.Customers
                .FirstOrDefault(c => c.Id == customerId.Value)?.Name;
        }

        private bool CustomerItemRequested =>
            _customerItemRequestedFor != null && ReferenceEquals(_customerItemRequestedFor, _activeModel);

        /// <summary>The customer fields' share of PopulateMadeItemFields, called from inside its
        /// UI-thread block with the same forceOverwrite. While Create Customer Item is pending
        /// for a file without a customer, the customer guessed from its folder takes the place
        /// of the empty one, so a later populate (a lookup refresh that now knows the customer,
        /// say) still lands on the guess rather than on nothing.</summary>
        private void PopulateCustomerFields(MadeItemProperties local, bool forceOverwrite)
        {
            SetCustomerFieldsVisible(CustomerItemRequested || HasCustomerData(local));

            if (forceOverwrite || !comboBoxCustomer.Focused)
            {
                var customerId = local.CustomerId;
                var customerName = local.CustomerName;

                if (customerId == null && CustomerItemRequested)
                {
                    var guess = CustomerFolderMatcher.Match(
                        _activeModel.GetPathName(), ApplicationServices.Lookups.Current.Customers);
                    customerId = guess?.Id;
                    customerName = guess?.Name;
                }

                SetCustomerSelection(customerId, customerName);
            }
            SetTextIfNotFocused(textBoxCustomerPartNumber, local.CustomerPartNumber, forceOverwrite);
            SetTextIfNotFocused(textBoxCustomerRevision, local.CustomerRevision, forceOverwrite);
        }

        private static bool HasCustomerData(MadeItemProperties local)
        {
            return local.CustomerId.HasValue
                || !IsBlank(local.CustomerPartNumber)
                || !IsBlank(local.CustomerRevision);
        }

        /// <summary>Shows or hides panelMadeItemCustomer and moves every Made panel control
        /// below it by the panel's height, the same way SetEmbedImportedComponentsVisible makes
        /// room on the Bought tab. The designer lays the tab out without the panel, as a plain
        /// made part sees it. The move is relative, so it holds while the panel is scrolled, and
        /// the offset is the laid-out height, so it follows DPI scaling.</summary>
        private void SetCustomerFieldsVisible(bool visible)
        {
            if (visible == _customerFieldsShown)
                return;

            _customerFieldsShown = visible;
            var offset = visible ? panelMadeItemCustomer.Height : -panelMadeItemCustomer.Height;

            panelMadeItemProperties.SuspendLayout();
            foreach (Control control in panelMadeItemProperties.Controls)
            {
                if (control != panelMadeItemCustomer && control.Top >= panelMadeItemCustomer.Top)
                    control.Top += offset;
            }
            panelMadeItemCustomer.Visible = visible;
            panelMadeItemProperties.ResumeLayout();
        }

        private void UpdateCustomerDirtyMarkers()
        {
            var saved = _madeItemLocal;

            SetDirtyMarker(labelCustomer, SelectedCustomerId != saved?.CustomerId);
            SetDirtyMarker(
                labelCustomerPartNumber,
                !TextMatches(textBoxCustomerPartNumber.Text, saved?.CustomerPartNumber)
            );
            SetDirtyMarker(
                labelCustomerRevision,
                !TextMatches(textBoxCustomerRevision.Text, saved?.CustomerRevision)
            );
            buttonCustomerClear.Enabled = SelectedCustomerId.HasValue;

            UpdateCustomerRequiredMarkers();
        }

        /// <summary>The backend's customer-data rule as markers: a customer needs the customer's
        /// part number, and a customer part number or revision needs a customer. Advisory only,
        /// like every other required marker: Save still writes what the tab holds, and PDM
        /// ingest stores it as received.</summary>
        private void UpdateCustomerRequiredMarkers()
        {
            var hasCustomer = SelectedCustomerId.HasValue;
            var hasCustomerData =
                !IsBlank(textBoxCustomerPartNumber.Text) || !IsBlank(textBoxCustomerRevision.Text);

            SetRequiredMarker(
                labelCustomer, !hasCustomer && hasCustomerData, Strings.TooltipCustomerRequired);
            SetRequiredMarker(
                labelCustomerPartNumber,
                hasCustomer && IsBlank(textBoxCustomerPartNumber.Text),
                Strings.TooltipCustomerPartNumberRequired);
        }

        private void buttonCustomerClear_Click(object sender, EventArgs e)
        {
            comboBoxCustomer.SelectedIndex = -1;
            UpdateCustomerDirtyMarkers();
        }

        /// <summary>Picks "made" exactly as buttonInitMade does and marks the document as a
        /// customer item, so the Made tab shows the customer fields with the customer guessed
        /// from the file's folder. Kind and the customer data are still only written by the
        /// first Save. A pick that changes the kind loads the Made tab, and that load fills the
        /// fields; when the kind already was "made" nothing reloads, so the fields are filled
        /// here. Declining the change-kind warning drops the request.</summary>
        private void buttonInitCustomer_Click(object sender, EventArgs e)
        {
            var wasMade = IsMadeSource(_currentSource);
            _customerItemRequestedFor = _activeModel;

            SelectSource(ItemKind.Made);

            if (!IsMadeSource(_currentSource))
            {
                _customerItemRequestedFor = null;
                return;
            }

            if (wasMade && _madeItemLocal != null)
            {
                PopulateCustomerFields(_madeItemLocal, forceOverwrite: false);
                UpdateCustomerDirtyMarkers();
                tabControlSource.SelectedTab = tabPageMade;
            }
        }
    }
}

namespace GladosSwAddin.Forms
{
    partial class SourceItemPickerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.labelFilterSearch = new System.Windows.Forms.Label();
            this.textBoxFilterSearch = new System.Windows.Forms.TextBox();
            this.labelFilterType = new System.Windows.Forms.Label();
            this.comboBoxFilterType = new System.Windows.Forms.ComboBox();
            this.labelFilterMaterial = new System.Windows.Forms.Label();
            this.textBoxFilterMaterial = new System.Windows.Forms.TextBox();
            this.labelFilterDimension = new System.Windows.Forms.Label();
            this.textBoxFilterDimension = new System.Windows.Forms.TextBox();
            this.labelFilterManufacturer = new System.Windows.Forms.Label();
            this.comboBoxFilterManufacturer = new System.Windows.Forms.ComboBox();
            this.checkBoxShowArchived = new System.Windows.Forms.CheckBox();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.dataGridViewSourceItems = new System.Windows.Forms.DataGridView();
            this.columnType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnDimension = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnMaterial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnMass = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnManufacturer = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnOrderCode1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelDetail = new System.Windows.Forms.Panel();
            this.labelDetailNote = new System.Windows.Forms.Label();
            this.labelDetailStatus = new System.Windows.Forms.Label();
            this.labelDetailMass = new System.Windows.Forms.Label();
            this.labelDetailOrderCode2 = new System.Windows.Forms.Label();
            this.labelDetailOrderCode1 = new System.Windows.Forms.Label();
            this.labelDetailManufacturer = new System.Windows.Forms.Label();
            this.labelDetailTitle = new System.Windows.Forms.Label();
            this.buttonCreateSubmit = new System.Windows.Forms.Button();
            this.textBoxCreateNote = new System.Windows.Forms.TextBox();
            this.labelCreateNote = new System.Windows.Forms.Label();
            this.textBoxCreateOrderCode2 = new System.Windows.Forms.TextBox();
            this.labelCreateOrderCode2 = new System.Windows.Forms.Label();
            this.textBoxCreateOrderCode1 = new System.Windows.Forms.TextBox();
            this.labelCreateOrderCode1 = new System.Windows.Forms.Label();
            this.comboBoxCreateManufacturer = new System.Windows.Forms.ComboBox();
            this.labelCreateManufacturer = new System.Windows.Forms.Label();
            this.textBoxCreateMass = new System.Windows.Forms.TextBox();
            this.labelCreateMass = new System.Windows.Forms.Label();
            this.textBoxCreateMaterial = new System.Windows.Forms.TextBox();
            this.labelCreateMaterial = new System.Windows.Forms.Label();
            this.textBoxCreateDimensionWidth = new System.Windows.Forms.TextBox();
            this.labelCreateDimension = new System.Windows.Forms.Label();
            this.comboBoxCreateType = new System.Windows.Forms.ComboBox();
            this.labelCreateType = new System.Windows.Forms.Label();
            this.buttonUse = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.groupBoxSearchExistingSourceItems = new System.Windows.Forms.GroupBox();
            this.groupBoxCreateSourceItem = new System.Windows.Forms.GroupBox();
            this.labelCreateError = new System.Windows.Forms.Label();
            this.labelCreateDimensionD = new System.Windows.Forms.Label();
            this.labelCreateDimensionX3 = new System.Windows.Forms.Label();
            this.labelCreateDimensionX2 = new System.Windows.Forms.Label();
            this.labelCreateDimensionX1 = new System.Windows.Forms.Label();
            this.labelCreateDimensionWallThickness = new System.Windows.Forms.Label();
            this.labelCreateDimensionLength = new System.Windows.Forms.Label();
            this.labelCreateDimensionHeight = new System.Windows.Forms.Label();
            this.labelCreateDimensionWidth = new System.Windows.Forms.Label();
            this.textBoxCreateDimensionWallThickness = new System.Windows.Forms.TextBox();
            this.textBoxCreateDimensionLength = new System.Windows.Forms.TextBox();
            this.textBoxCreateDimensionHeight = new System.Windows.Forms.TextBox();
            this.comboBoxCreatePreset = new System.Windows.Forms.ComboBox();
            this.labelBaseSize = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSourceItems)).BeginInit();
            this.panelDetail.SuspendLayout();
            this.groupBoxSearchExistingSourceItems.SuspendLayout();
            this.groupBoxCreateSourceItem.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelFilterSearch
            // 
            this.labelFilterSearch.AutoSize = true;
            this.labelFilterSearch.Location = new System.Drawing.Point(13, 26);
            this.labelFilterSearch.Name = "labelFilterSearch";
            this.labelFilterSearch.Size = new System.Drawing.Size(29, 13);
            this.labelFilterSearch.TabIndex = 0;
            this.labelFilterSearch.Text = "Filter";
            // 
            // textBoxFilterSearch
            // 
            this.textBoxFilterSearch.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxFilterSearch.Location = new System.Drawing.Point(13, 42);
            this.textBoxFilterSearch.Name = "textBoxFilterSearch";
            this.textBoxFilterSearch.Size = new System.Drawing.Size(208, 20);
            this.textBoxFilterSearch.TabIndex = 1;
            this.textBoxFilterSearch.TextChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // labelFilterType
            // 
            this.labelFilterType.AutoSize = true;
            this.labelFilterType.Location = new System.Drawing.Point(227, 26);
            this.labelFilterType.Name = "labelFilterType";
            this.labelFilterType.Size = new System.Drawing.Size(31, 13);
            this.labelFilterType.TabIndex = 2;
            this.labelFilterType.Text = "Type";
            // 
            // comboBoxFilterType
            // 
            this.comboBoxFilterType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFilterType.FormattingEnabled = true;
            this.comboBoxFilterType.Location = new System.Drawing.Point(227, 42);
            this.comboBoxFilterType.Name = "comboBoxFilterType";
            this.comboBoxFilterType.Size = new System.Drawing.Size(112, 21);
            this.comboBoxFilterType.TabIndex = 2;
            this.comboBoxFilterType.SelectedIndexChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // labelFilterMaterial
            // 
            this.labelFilterMaterial.AutoSize = true;
            this.labelFilterMaterial.Location = new System.Drawing.Point(345, 26);
            this.labelFilterMaterial.Name = "labelFilterMaterial";
            this.labelFilterMaterial.Size = new System.Drawing.Size(44, 13);
            this.labelFilterMaterial.TabIndex = 4;
            this.labelFilterMaterial.Text = "Material";
            // 
            // textBoxFilterMaterial
            // 
            this.textBoxFilterMaterial.Location = new System.Drawing.Point(345, 42);
            this.textBoxFilterMaterial.Name = "textBoxFilterMaterial";
            this.textBoxFilterMaterial.Size = new System.Drawing.Size(110, 20);
            this.textBoxFilterMaterial.TabIndex = 3;
            this.textBoxFilterMaterial.TextChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // labelFilterDimension
            // 
            this.labelFilterDimension.AutoSize = true;
            this.labelFilterDimension.Location = new System.Drawing.Point(463, 26);
            this.labelFilterDimension.Name = "labelFilterDimension";
            this.labelFilterDimension.Size = new System.Drawing.Size(56, 13);
            this.labelFilterDimension.TabIndex = 6;
            this.labelFilterDimension.Text = "Dimension";
            // 
            // textBoxFilterDimension
            // 
            this.textBoxFilterDimension.Location = new System.Drawing.Point(463, 42);
            this.textBoxFilterDimension.Name = "textBoxFilterDimension";
            this.textBoxFilterDimension.Size = new System.Drawing.Size(110, 20);
            this.textBoxFilterDimension.TabIndex = 4;
            this.textBoxFilterDimension.TextChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // labelFilterManufacturer
            // 
            this.labelFilterManufacturer.AutoSize = true;
            this.labelFilterManufacturer.Location = new System.Drawing.Point(581, 26);
            this.labelFilterManufacturer.Name = "labelFilterManufacturer";
            this.labelFilterManufacturer.Size = new System.Drawing.Size(70, 13);
            this.labelFilterManufacturer.TabIndex = 8;
            this.labelFilterManufacturer.Text = "Manufacturer";
            // 
            // comboBoxFilterManufacturer
            // 
            this.comboBoxFilterManufacturer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFilterManufacturer.FormattingEnabled = true;
            this.comboBoxFilterManufacturer.Location = new System.Drawing.Point(581, 42);
            this.comboBoxFilterManufacturer.MaxDropDownItems = 5;
            this.comboBoxFilterManufacturer.Name = "comboBoxFilterManufacturer";
            this.comboBoxFilterManufacturer.Size = new System.Drawing.Size(170, 21);
            this.comboBoxFilterManufacturer.TabIndex = 5;
            this.comboBoxFilterManufacturer.SelectedIndexChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // checkBoxShowArchived
            // 
            this.checkBoxShowArchived.AutoSize = true;
            this.checkBoxShowArchived.Location = new System.Drawing.Point(13, 65);
            this.checkBoxShowArchived.Name = "checkBoxShowArchived";
            this.checkBoxShowArchived.Size = new System.Drawing.Size(97, 17);
            this.checkBoxShowArchived.TabIndex = 6;
            this.checkBoxShowArchived.Text = "Show archived";
            this.checkBoxShowArchived.UseVisualStyleBackColor = true;
            this.checkBoxShowArchived.CheckedChanged += new System.EventHandler(this.Filter_Changed);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(757, 41);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(76, 23);
            this.buttonRefresh.TabIndex = 7;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // dataGridViewSourceItems
            // 
            this.dataGridViewSourceItems.AllowUserToAddRows = false;
            this.dataGridViewSourceItems.AllowUserToDeleteRows = false;
            this.dataGridViewSourceItems.AllowUserToResizeRows = false;
            this.dataGridViewSourceItems.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridViewSourceItems.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dataGridViewSourceItems.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnType,
            this.columnDimension,
            this.columnMaterial,
            this.columnMass,
            this.columnManufacturer,
            this.columnOrderCode1,
            this.columnStatus});
            this.dataGridViewSourceItems.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dataGridViewSourceItems.Location = new System.Drawing.Point(8, 90);
            this.dataGridViewSourceItems.MultiSelect = false;
            this.dataGridViewSourceItems.Name = "dataGridViewSourceItems";
            this.dataGridViewSourceItems.ReadOnly = true;
            this.dataGridViewSourceItems.RowHeadersVisible = false;
            this.dataGridViewSourceItems.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewSourceItems.Size = new System.Drawing.Size(560, 289);
            this.dataGridViewSourceItems.TabIndex = 12;
            this.dataGridViewSourceItems.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewSourceItems_CellDoubleClick);
            this.dataGridViewSourceItems.SelectionChanged += new System.EventHandler(this.dataGridViewSourceItems_SelectionChanged);
            this.dataGridViewSourceItems.KeyDown += new System.Windows.Forms.KeyEventHandler(this.dataGridViewSourceItems_KeyDown);
            // 
            // columnType
            // 
            this.columnType.HeaderText = "Type";
            this.columnType.Name = "columnType";
            this.columnType.ReadOnly = true;
            this.columnType.Width = 80;
            // 
            // columnDimension
            // 
            this.columnDimension.HeaderText = "Dimension";
            this.columnDimension.Name = "columnDimension";
            this.columnDimension.ReadOnly = true;
            this.columnDimension.Width = 80;
            // 
            // columnMaterial
            // 
            this.columnMaterial.HeaderText = "Material";
            this.columnMaterial.Name = "columnMaterial";
            this.columnMaterial.ReadOnly = true;
            this.columnMaterial.Width = 80;
            // 
            // columnMass
            // 
            this.columnMass.HeaderText = "Mass [g]";
            this.columnMass.Name = "columnMass";
            this.columnMass.ReadOnly = true;
            this.columnMass.Width = 50;
            // 
            // columnManufacturer
            // 
            this.columnManufacturer.HeaderText = "Manufacturer";
            this.columnManufacturer.Name = "columnManufacturer";
            this.columnManufacturer.ReadOnly = true;
            this.columnManufacturer.Width = 90;
            // 
            // columnOrderCode1
            // 
            this.columnOrderCode1.HeaderText = "Order Code 1";
            this.columnOrderCode1.Name = "columnOrderCode1";
            this.columnOrderCode1.ReadOnly = true;
            this.columnOrderCode1.Width = 70;
            // 
            // columnStatus
            // 
            this.columnStatus.HeaderText = "Status";
            this.columnStatus.Name = "columnStatus";
            this.columnStatus.ReadOnly = true;
            this.columnStatus.Width = 50;
            // 
            // panelDetail
            // 
            this.panelDetail.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelDetail.AutoScroll = true;
            this.panelDetail.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelDetail.Controls.Add(this.labelDetailNote);
            this.panelDetail.Controls.Add(this.labelDetailStatus);
            this.panelDetail.Controls.Add(this.labelDetailMass);
            this.panelDetail.Controls.Add(this.labelDetailOrderCode2);
            this.panelDetail.Controls.Add(this.labelDetailOrderCode1);
            this.panelDetail.Controls.Add(this.labelDetailManufacturer);
            this.panelDetail.Controls.Add(this.labelDetailTitle);
            this.panelDetail.Location = new System.Drawing.Point(574, 90);
            this.panelDetail.Name = "panelDetail";
            this.panelDetail.Size = new System.Drawing.Size(259, 289);
            this.panelDetail.TabIndex = 13;
            // 
            // labelDetailNote
            // 
            this.labelDetailNote.AutoSize = true;
            this.labelDetailNote.Location = new System.Drawing.Point(6, 154);
            this.labelDetailNote.MaximumSize = new System.Drawing.Size(240, 0);
            this.labelDetailNote.Name = "labelDetailNote";
            this.labelDetailNote.Size = new System.Drawing.Size(0, 13);
            this.labelDetailNote.TabIndex = 6;
            // 
            // labelDetailStatus
            // 
            this.labelDetailStatus.AutoSize = true;
            this.labelDetailStatus.Location = new System.Drawing.Point(6, 130);
            this.labelDetailStatus.MaximumSize = new System.Drawing.Size(240, 0);
            this.labelDetailStatus.Name = "labelDetailStatus";
            this.labelDetailStatus.Size = new System.Drawing.Size(0, 13);
            this.labelDetailStatus.TabIndex = 5;
            // 
            // labelDetailMass
            // 
            this.labelDetailMass.AutoSize = true;
            this.labelDetailMass.Location = new System.Drawing.Point(6, 106);
            this.labelDetailMass.MaximumSize = new System.Drawing.Size(240, 0);
            this.labelDetailMass.Name = "labelDetailMass";
            this.labelDetailMass.Size = new System.Drawing.Size(0, 13);
            this.labelDetailMass.TabIndex = 4;
            // 
            // labelDetailOrderCode2
            // 
            this.labelDetailOrderCode2.AutoSize = true;
            this.labelDetailOrderCode2.Location = new System.Drawing.Point(6, 82);
            this.labelDetailOrderCode2.MaximumSize = new System.Drawing.Size(240, 0);
            this.labelDetailOrderCode2.Name = "labelDetailOrderCode2";
            this.labelDetailOrderCode2.Size = new System.Drawing.Size(0, 13);
            this.labelDetailOrderCode2.TabIndex = 3;
            // 
            // labelDetailOrderCode1
            // 
            this.labelDetailOrderCode1.AutoSize = true;
            this.labelDetailOrderCode1.Location = new System.Drawing.Point(6, 58);
            this.labelDetailOrderCode1.MaximumSize = new System.Drawing.Size(240, 0);
            this.labelDetailOrderCode1.Name = "labelDetailOrderCode1";
            this.labelDetailOrderCode1.Size = new System.Drawing.Size(0, 13);
            this.labelDetailOrderCode1.TabIndex = 2;
            // 
            // labelDetailManufacturer
            // 
            this.labelDetailManufacturer.AutoSize = true;
            this.labelDetailManufacturer.Location = new System.Drawing.Point(6, 34);
            this.labelDetailManufacturer.MaximumSize = new System.Drawing.Size(240, 0);
            this.labelDetailManufacturer.Name = "labelDetailManufacturer";
            this.labelDetailManufacturer.Size = new System.Drawing.Size(0, 13);
            this.labelDetailManufacturer.TabIndex = 1;
            // 
            // labelDetailTitle
            // 
            this.labelDetailTitle.AutoSize = true;
            this.labelDetailTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelDetailTitle.Location = new System.Drawing.Point(6, 6);
            this.labelDetailTitle.MaximumSize = new System.Drawing.Size(240, 0);
            this.labelDetailTitle.Name = "labelDetailTitle";
            this.labelDetailTitle.Size = new System.Drawing.Size(0, 15);
            this.labelDetailTitle.TabIndex = 0;
            // 
            // buttonCreateSubmit
            // 
            this.buttonCreateSubmit.Location = new System.Drawing.Point(13, 174);
            this.buttonCreateSubmit.Name = "buttonCreateSubmit";
            this.buttonCreateSubmit.Size = new System.Drawing.Size(106, 23);
            this.buttonCreateSubmit.TabIndex = 22;
            this.buttonCreateSubmit.Text = "Create";
            this.buttonCreateSubmit.UseVisualStyleBackColor = true;
            this.buttonCreateSubmit.Click += new System.EventHandler(this.buttonCreateSubmit_Click);
            // 
            // textBoxCreateNote
            // 
            this.textBoxCreateNote.Location = new System.Drawing.Point(576, 47);
            this.textBoxCreateNote.Multiline = true;
            this.textBoxCreateNote.Name = "textBoxCreateNote";
            this.textBoxCreateNote.Size = new System.Drawing.Size(259, 150);
            this.textBoxCreateNote.TabIndex = 21;
            // 
            // labelCreateNote
            // 
            this.labelCreateNote.AutoSize = true;
            this.labelCreateNote.Location = new System.Drawing.Point(573, 31);
            this.labelCreateNote.Name = "labelCreateNote";
            this.labelCreateNote.Size = new System.Drawing.Size(30, 13);
            this.labelCreateNote.TabIndex = 14;
            this.labelCreateNote.Text = "Note";
            // 
            // textBoxCreateOrderCode2
            // 
            this.textBoxCreateOrderCode2.Location = new System.Drawing.Point(210, 139);
            this.textBoxCreateOrderCode2.Name = "textBoxCreateOrderCode2";
            this.textBoxCreateOrderCode2.Size = new System.Drawing.Size(160, 20);
            this.textBoxCreateOrderCode2.TabIndex = 19;
            // 
            // labelCreateOrderCode2
            // 
            this.labelCreateOrderCode2.AutoSize = true;
            this.labelCreateOrderCode2.Location = new System.Drawing.Point(210, 123);
            this.labelCreateOrderCode2.Name = "labelCreateOrderCode2";
            this.labelCreateOrderCode2.Size = new System.Drawing.Size(70, 13);
            this.labelCreateOrderCode2.TabIndex = 12;
            this.labelCreateOrderCode2.Text = "Order Code 2";
            // 
            // textBoxCreateOrderCode1
            // 
            this.textBoxCreateOrderCode1.Location = new System.Drawing.Point(13, 139);
            this.textBoxCreateOrderCode1.Name = "textBoxCreateOrderCode1";
            this.textBoxCreateOrderCode1.Size = new System.Drawing.Size(191, 20);
            this.textBoxCreateOrderCode1.TabIndex = 18;
            // 
            // labelCreateOrderCode1
            // 
            this.labelCreateOrderCode1.AutoSize = true;
            this.labelCreateOrderCode1.Location = new System.Drawing.Point(13, 123);
            this.labelCreateOrderCode1.Name = "labelCreateOrderCode1";
            this.labelCreateOrderCode1.Size = new System.Drawing.Size(70, 13);
            this.labelCreateOrderCode1.TabIndex = 10;
            this.labelCreateOrderCode1.Text = "Order Code 1";
            // 
            // comboBoxCreateManufacturer
            // 
            this.comboBoxCreateManufacturer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCreateManufacturer.FormattingEnabled = true;
            this.comboBoxCreateManufacturer.Location = new System.Drawing.Point(376, 138);
            this.comboBoxCreateManufacturer.Name = "comboBoxCreateManufacturer";
            this.comboBoxCreateManufacturer.Size = new System.Drawing.Size(160, 21);
            this.comboBoxCreateManufacturer.TabIndex = 20;
            // 
            // labelCreateManufacturer
            // 
            this.labelCreateManufacturer.AutoSize = true;
            this.labelCreateManufacturer.Location = new System.Drawing.Point(375, 123);
            this.labelCreateManufacturer.Name = "labelCreateManufacturer";
            this.labelCreateManufacturer.Size = new System.Drawing.Size(70, 13);
            this.labelCreateManufacturer.TabIndex = 8;
            this.labelCreateManufacturer.Text = "Manufacturer";
            // 
            // textBoxCreateMass
            // 
            this.textBoxCreateMass.Location = new System.Drawing.Point(456, 94);
            this.textBoxCreateMass.Name = "textBoxCreateMass";
            this.textBoxCreateMass.Size = new System.Drawing.Size(80, 20);
            this.textBoxCreateMass.TabIndex = 17;
            // 
            // labelCreateMass
            // 
            this.labelCreateMass.AutoSize = true;
            this.labelCreateMass.Location = new System.Drawing.Point(456, 78);
            this.labelCreateMass.Name = "labelCreateMass";
            this.labelCreateMass.Size = new System.Drawing.Size(47, 13);
            this.labelCreateMass.TabIndex = 6;
            this.labelCreateMass.Text = "Mass [g]";
            // 
            // textBoxCreateMaterial
            // 
            this.textBoxCreateMaterial.Location = new System.Drawing.Point(310, 94);
            this.textBoxCreateMaterial.Name = "textBoxCreateMaterial";
            this.textBoxCreateMaterial.Size = new System.Drawing.Size(140, 20);
            this.textBoxCreateMaterial.TabIndex = 16;
            // 
            // labelCreateMaterial
            // 
            this.labelCreateMaterial.AutoSize = true;
            this.labelCreateMaterial.Location = new System.Drawing.Point(310, 78);
            this.labelCreateMaterial.Name = "labelCreateMaterial";
            this.labelCreateMaterial.Size = new System.Drawing.Size(44, 13);
            this.labelCreateMaterial.TabIndex = 4;
            this.labelCreateMaterial.Text = "Material";
            // 
            // textBoxCreateDimensionWidth
            // 
            this.textBoxCreateDimensionWidth.Location = new System.Drawing.Point(142, 46);
            this.textBoxCreateDimensionWidth.Name = "textBoxCreateDimensionWidth";
            this.textBoxCreateDimensionWidth.Size = new System.Drawing.Size(80, 20);
            this.textBoxCreateDimensionWidth.TabIndex = 11;
            // 
            // labelCreateDimension
            // 
            this.labelCreateDimension.AutoSize = true;
            this.labelCreateDimension.Location = new System.Drawing.Point(10, 30);
            this.labelCreateDimension.Name = "labelCreateDimension";
            this.labelCreateDimension.Size = new System.Drawing.Size(56, 13);
            this.labelCreateDimension.TabIndex = 2;
            this.labelCreateDimension.Text = "Dimension";
            // 
            // comboBoxCreateType
            // 
            this.comboBoxCreateType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCreateType.FormattingEnabled = true;
            this.comboBoxCreateType.Location = new System.Drawing.Point(13, 93);
            this.comboBoxCreateType.Name = "comboBoxCreateType";
            this.comboBoxCreateType.Size = new System.Drawing.Size(288, 21);
            this.comboBoxCreateType.TabIndex = 15;
            // 
            // labelCreateType
            // 
            this.labelCreateType.AutoSize = true;
            this.labelCreateType.Location = new System.Drawing.Point(13, 77);
            this.labelCreateType.Name = "labelCreateType";
            this.labelCreateType.Size = new System.Drawing.Size(31, 13);
            this.labelCreateType.TabIndex = 0;
            this.labelCreateType.Text = "Type";
            // 
            // buttonUse
            // 
            this.buttonUse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonUse.Location = new System.Drawing.Point(686, 632);
            this.buttonUse.Name = "buttonUse";
            this.buttonUse.Size = new System.Drawing.Size(80, 23);
            this.buttonUse.TabIndex = 30;
            this.buttonUse.Text = "Use";
            this.buttonUse.UseVisualStyleBackColor = true;
            this.buttonUse.Click += new System.EventHandler(this.buttonUse_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(772, 632);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(80, 23);
            this.buttonCancel.TabIndex = 31;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // groupBoxSearchExistingSourceItems
            // 
            this.groupBoxSearchExistingSourceItems.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.dataGridViewSourceItems);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.checkBoxShowArchived);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.labelFilterSearch);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.textBoxFilterSearch);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.labelFilterType);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.panelDetail);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.comboBoxFilterType);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.buttonRefresh);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.labelFilterMaterial);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.comboBoxFilterManufacturer);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.textBoxFilterMaterial);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.labelFilterManufacturer);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.labelFilterDimension);
            this.groupBoxSearchExistingSourceItems.Controls.Add(this.textBoxFilterDimension);
            this.groupBoxSearchExistingSourceItems.Location = new System.Drawing.Point(12, 12);
            this.groupBoxSearchExistingSourceItems.Name = "groupBoxSearchExistingSourceItems";
            this.groupBoxSearchExistingSourceItems.Size = new System.Drawing.Size(840, 389);
            this.groupBoxSearchExistingSourceItems.TabIndex = 18;
            this.groupBoxSearchExistingSourceItems.TabStop = false;
            this.groupBoxSearchExistingSourceItems.Text = "Search";
            // 
            // groupBoxCreateSourceItem
            // 
            this.groupBoxCreateSourceItem.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateError);
            this.groupBoxCreateSourceItem.Controls.Add(this.buttonCreateSubmit);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimensionD);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimensionX3);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateNote);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimensionX2);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateNote);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimensionX1);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateOrderCode2);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimensionWallThickness);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateOrderCode2);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimensionLength);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateOrderCode1);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimensionHeight);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateOrderCode1);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimensionWidth);
            this.groupBoxCreateSourceItem.Controls.Add(this.comboBoxCreateManufacturer);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateManufacturer);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateDimensionWallThickness);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateDimensionLength);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateMass);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateMass);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateDimensionHeight);
            this.groupBoxCreateSourceItem.Controls.Add(this.comboBoxCreatePreset);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateMaterial);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateDimension);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateMaterial);
            this.groupBoxCreateSourceItem.Controls.Add(this.textBoxCreateDimensionWidth);
            this.groupBoxCreateSourceItem.Controls.Add(this.comboBoxCreateType);
            this.groupBoxCreateSourceItem.Controls.Add(this.labelCreateType);
            this.groupBoxCreateSourceItem.Location = new System.Drawing.Point(12, 417);
            this.groupBoxCreateSourceItem.Name = "groupBoxCreateSourceItem";
            this.groupBoxCreateSourceItem.Size = new System.Drawing.Size(840, 209);
            this.groupBoxCreateSourceItem.TabIndex = 19;
            this.groupBoxCreateSourceItem.TabStop = false;
            this.groupBoxCreateSourceItem.Text = "Create Source Item";
            // 
            // labelCreateError
            // 
            this.labelCreateError.ForeColor = System.Drawing.Color.Red;
            this.labelCreateError.Location = new System.Drawing.Point(129, 178);
            this.labelCreateError.Name = "labelCreateError";
            this.labelCreateError.Size = new System.Drawing.Size(438, 20);
            this.labelCreateError.TabIndex = 18;
            // 
            // labelCreateDimensionD
            // 
            this.labelCreateDimensionD.AutoSize = true;
            this.labelCreateDimensionD.Location = new System.Drawing.Point(123, 49);
            this.labelCreateDimensionD.Name = "labelCreateDimensionD";
            this.labelCreateDimensionD.Size = new System.Drawing.Size(15, 13);
            this.labelCreateDimensionD.TabIndex = 15;
            this.labelCreateDimensionD.Text = "Ø";
            // 
            // labelCreateDimensionX3
            // 
            this.labelCreateDimensionX3.AutoSize = true;
            this.labelCreateDimensionX3.Location = new System.Drawing.Point(437, 49);
            this.labelCreateDimensionX3.Name = "labelCreateDimensionX3";
            this.labelCreateDimensionX3.Size = new System.Drawing.Size(13, 13);
            this.labelCreateDimensionX3.TabIndex = 14;
            this.labelCreateDimensionX3.Text = "×";
            // 
            // labelCreateDimensionX2
            // 
            this.labelCreateDimensionX2.AutoSize = true;
            this.labelCreateDimensionX2.Location = new System.Drawing.Point(333, 49);
            this.labelCreateDimensionX2.Name = "labelCreateDimensionX2";
            this.labelCreateDimensionX2.Size = new System.Drawing.Size(13, 13);
            this.labelCreateDimensionX2.TabIndex = 13;
            this.labelCreateDimensionX2.Text = "×";
            // 
            // labelCreateDimensionX1
            // 
            this.labelCreateDimensionX1.AutoSize = true;
            this.labelCreateDimensionX1.Location = new System.Drawing.Point(228, 49);
            this.labelCreateDimensionX1.Name = "labelCreateDimensionX1";
            this.labelCreateDimensionX1.Size = new System.Drawing.Size(13, 13);
            this.labelCreateDimensionX1.TabIndex = 12;
            this.labelCreateDimensionX1.Text = "×";
            // 
            // labelCreateDimensionWallThickness
            // 
            this.labelCreateDimensionWallThickness.AutoSize = true;
            this.labelCreateDimensionWallThickness.Location = new System.Drawing.Point(453, 31);
            this.labelCreateDimensionWallThickness.Name = "labelCreateDimensionWallThickness";
            this.labelCreateDimensionWallThickness.Size = new System.Drawing.Size(80, 13);
            this.labelCreateDimensionWallThickness.TabIndex = 11;
            this.labelCreateDimensionWallThickness.Text = "Wall Thickness";
            // 
            // labelCreateDimensionLength
            // 
            this.labelCreateDimensionLength.AutoSize = true;
            this.labelCreateDimensionLength.Location = new System.Drawing.Point(352, 31);
            this.labelCreateDimensionLength.Name = "labelCreateDimensionLength";
            this.labelCreateDimensionLength.Size = new System.Drawing.Size(40, 13);
            this.labelCreateDimensionLength.TabIndex = 10;
            this.labelCreateDimensionLength.Text = "Length";
            // 
            // labelCreateDimensionHeight
            // 
            this.labelCreateDimensionHeight.AutoSize = true;
            this.labelCreateDimensionHeight.Location = new System.Drawing.Point(244, 30);
            this.labelCreateDimensionHeight.Name = "labelCreateDimensionHeight";
            this.labelCreateDimensionHeight.Size = new System.Drawing.Size(38, 13);
            this.labelCreateDimensionHeight.TabIndex = 9;
            this.labelCreateDimensionHeight.Text = "Height";
            // 
            // labelCreateDimensionWidth
            // 
            this.labelCreateDimensionWidth.AutoSize = true;
            this.labelCreateDimensionWidth.Location = new System.Drawing.Point(139, 30);
            this.labelCreateDimensionWidth.Name = "labelCreateDimensionWidth";
            this.labelCreateDimensionWidth.Size = new System.Drawing.Size(35, 13);
            this.labelCreateDimensionWidth.TabIndex = 8;
            this.labelCreateDimensionWidth.Text = "Width";
            // 
            // textBoxCreateDimensionWallThickness
            // 
            this.textBoxCreateDimensionWallThickness.Location = new System.Drawing.Point(456, 47);
            this.textBoxCreateDimensionWallThickness.Name = "textBoxCreateDimensionWallThickness";
            this.textBoxCreateDimensionWallThickness.Size = new System.Drawing.Size(80, 20);
            this.textBoxCreateDimensionWallThickness.TabIndex = 14;
            // 
            // textBoxCreateDimensionLength
            // 
            this.textBoxCreateDimensionLength.Location = new System.Drawing.Point(352, 47);
            this.textBoxCreateDimensionLength.Name = "textBoxCreateDimensionLength";
            this.textBoxCreateDimensionLength.Size = new System.Drawing.Size(80, 20);
            this.textBoxCreateDimensionLength.TabIndex = 13;
            // 
            // textBoxCreateDimensionHeight
            // 
            this.textBoxCreateDimensionHeight.Location = new System.Drawing.Point(247, 46);
            this.textBoxCreateDimensionHeight.Name = "textBoxCreateDimensionHeight";
            this.textBoxCreateDimensionHeight.Size = new System.Drawing.Size(80, 20);
            this.textBoxCreateDimensionHeight.TabIndex = 12;
            // 
            // comboBoxCreatePreset
            // 
            this.comboBoxCreatePreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCreatePreset.FormattingEnabled = true;
            this.comboBoxCreatePreset.Location = new System.Drawing.Point(13, 46);
            this.comboBoxCreatePreset.Name = "comboBoxCreatePreset";
            this.comboBoxCreatePreset.Size = new System.Drawing.Size(94, 21);
            this.comboBoxCreatePreset.TabIndex = 10;
            // 
            // labelBaseSize
            // 
            this.labelBaseSize.AutoSize = true;
            this.labelBaseSize.Location = new System.Drawing.Point(12, 694);
            this.labelBaseSize.Name = "labelBaseSize";
            this.labelBaseSize.Size = new System.Drawing.Size(63, 13);
            this.labelBaseSize.TabIndex = 20;
            this.labelBaseSize.Text = "Base Size: -";
            // 
            // SourceItemPickerForm
            // 
            this.AcceptButton = this.buttonUse;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(864, 661);
            this.Controls.Add(this.labelBaseSize);
            this.Controls.Add(this.groupBoxCreateSourceItem);
            this.Controls.Add(this.groupBoxSearchExistingSourceItems);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonUse);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(880, 550);
            this.Name = "SourceItemPickerForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "GLADOS SOLIDWORKS Add-in | Select Source Item";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.SourceItemPickerForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSourceItems)).EndInit();
            this.panelDetail.ResumeLayout(false);
            this.panelDetail.PerformLayout();
            this.groupBoxSearchExistingSourceItems.ResumeLayout(false);
            this.groupBoxSearchExistingSourceItems.PerformLayout();
            this.groupBoxCreateSourceItem.ResumeLayout(false);
            this.groupBoxCreateSourceItem.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelFilterSearch;
        private System.Windows.Forms.TextBox textBoxFilterSearch;
        private System.Windows.Forms.Label labelFilterType;
        private System.Windows.Forms.ComboBox comboBoxFilterType;
        private System.Windows.Forms.Label labelFilterMaterial;
        private System.Windows.Forms.TextBox textBoxFilterMaterial;
        private System.Windows.Forms.Label labelFilterDimension;
        private System.Windows.Forms.TextBox textBoxFilterDimension;
        private System.Windows.Forms.Label labelFilterManufacturer;
        private System.Windows.Forms.ComboBox comboBoxFilterManufacturer;
        private System.Windows.Forms.CheckBox checkBoxShowArchived;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.DataGridView dataGridViewSourceItems;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnType;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnDimension;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnMaterial;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnMass;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnManufacturer;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnOrderCode1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStatus;
        private System.Windows.Forms.Panel panelDetail;
        private System.Windows.Forms.Label labelDetailTitle;
        private System.Windows.Forms.Label labelDetailManufacturer;
        private System.Windows.Forms.Label labelDetailOrderCode1;
        private System.Windows.Forms.Label labelDetailOrderCode2;
        private System.Windows.Forms.Label labelDetailMass;
        private System.Windows.Forms.Label labelDetailStatus;
        private System.Windows.Forms.Label labelDetailNote;
        private System.Windows.Forms.Label labelCreateType;
        private System.Windows.Forms.ComboBox comboBoxCreateType;
        private System.Windows.Forms.Label labelCreateDimension;
        private System.Windows.Forms.TextBox textBoxCreateDimensionWidth;
        private System.Windows.Forms.Label labelCreateMaterial;
        private System.Windows.Forms.TextBox textBoxCreateMaterial;
        private System.Windows.Forms.Label labelCreateMass;
        private System.Windows.Forms.TextBox textBoxCreateMass;
        private System.Windows.Forms.Label labelCreateManufacturer;
        private System.Windows.Forms.ComboBox comboBoxCreateManufacturer;
        private System.Windows.Forms.Label labelCreateOrderCode1;
        private System.Windows.Forms.TextBox textBoxCreateOrderCode1;
        private System.Windows.Forms.Label labelCreateOrderCode2;
        private System.Windows.Forms.TextBox textBoxCreateOrderCode2;
        private System.Windows.Forms.Label labelCreateNote;
        private System.Windows.Forms.TextBox textBoxCreateNote;
        private System.Windows.Forms.Button buttonCreateSubmit;
        private System.Windows.Forms.Button buttonUse;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.GroupBox groupBoxSearchExistingSourceItems;
        private System.Windows.Forms.GroupBox groupBoxCreateSourceItem;
        private System.Windows.Forms.TextBox textBoxCreateDimensionWallThickness;
        private System.Windows.Forms.TextBox textBoxCreateDimensionLength;
        private System.Windows.Forms.TextBox textBoxCreateDimensionHeight;
        private System.Windows.Forms.ComboBox comboBoxCreatePreset;
        private System.Windows.Forms.Label labelCreateDimensionWallThickness;
        private System.Windows.Forms.Label labelCreateDimensionLength;
        private System.Windows.Forms.Label labelCreateDimensionHeight;
        private System.Windows.Forms.Label labelCreateDimensionWidth;
        private System.Windows.Forms.Label labelCreateDimensionD;
        private System.Windows.Forms.Label labelCreateDimensionX3;
        private System.Windows.Forms.Label labelCreateDimensionX2;
        private System.Windows.Forms.Label labelCreateDimensionX1;
        private System.Windows.Forms.Label labelBaseSize;
        private System.Windows.Forms.Label labelCreateError;
    }
}

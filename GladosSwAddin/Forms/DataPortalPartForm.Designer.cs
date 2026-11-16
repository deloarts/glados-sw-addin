namespace GladosSwAddin.Forms
{
    partial class DataPortalPartForm
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
            this.labelSearch = new System.Windows.Forms.Label();
            this.textBoxSearch = new System.Windows.Forms.TextBox();
            this.buttonSearch = new System.Windows.Forms.Button();
            this.listBoxHits = new System.Windows.Forms.ListBox();
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelNoPicture = new System.Windows.Forms.Label();
            this.pictureBoxPart = new System.Windows.Forms.PictureBox();
            this.labelDetailDesignation = new System.Windows.Forms.Label();
            this.labelDetailManufacturer = new System.Windows.Forms.Label();
            this.labelDetailOrderNumber = new System.Windows.Forms.Label();
            this.labelDetailTypeNumber = new System.Windows.Forms.Label();
            this.labelDetailPartNumber = new System.Windows.Forms.Label();
            this.labelDetailProductFamily = new System.Windows.Forms.Label();
            this.linkLabelPartPage = new System.Windows.Forms.LinkLabel();
            this.labelExistingItem = new System.Windows.Forms.Label();
            this.labelNoSupplier = new System.Windows.Forms.Label();
            this.dataGridViewFields = new System.Windows.Forms.DataGridView();
            this.columnTake = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.columnField = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnCurrent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnPortal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.buttonTake = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPart)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFields)).BeginInit();
            this.SuspendLayout();
            //
            // labelSearch
            //
            this.labelSearch.AutoSize = true;
            this.labelSearch.Location = new System.Drawing.Point(12, 12);
            this.labelSearch.Name = "labelSearch";
            this.labelSearch.Size = new System.Drawing.Size(165, 13);
            this.labelSearch.TabIndex = 0;
            this.labelSearch.Text = "Order code, type number or name";
            //
            // textBoxSearch
            //
            this.textBoxSearch.Location = new System.Drawing.Point(12, 28);
            this.textBoxSearch.Name = "textBoxSearch";
            this.textBoxSearch.Size = new System.Drawing.Size(249, 20);
            this.textBoxSearch.TabIndex = 1;
            this.textBoxSearch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBoxSearch_KeyDown);
            //
            // buttonSearch
            //
            this.buttonSearch.Location = new System.Drawing.Point(267, 27);
            this.buttonSearch.Name = "buttonSearch";
            this.buttonSearch.Size = new System.Drawing.Size(75, 22);
            this.buttonSearch.TabIndex = 2;
            this.buttonSearch.Text = "Search";
            this.buttonSearch.UseVisualStyleBackColor = true;
            this.buttonSearch.Click += new System.EventHandler(this.buttonSearch_Click);
            //
            // listBoxHits
            //
            this.listBoxHits.FormattingEnabled = true;
            this.listBoxHits.HorizontalScrollbar = true;
            this.listBoxHits.IntegralHeight = false;
            this.listBoxHits.Location = new System.Drawing.Point(12, 58);
            this.listBoxHits.Name = "listBoxHits";
            this.listBoxHits.Size = new System.Drawing.Size(330, 450);
            this.listBoxHits.TabIndex = 3;
            this.listBoxHits.SelectedIndexChanged += new System.EventHandler(this.listBoxHits_SelectedIndexChanged);
            //
            // labelStatus
            //
            this.labelStatus.Location = new System.Drawing.Point(12, 514);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(330, 32);
            this.labelStatus.TabIndex = 4;
            this.labelStatus.Text = "-";
            //
            // labelNoPicture
            //
            this.labelNoPicture.BackColor = System.Drawing.SystemColors.Window;
            this.labelNoPicture.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelNoPicture.Location = new System.Drawing.Point(355, 131);
            this.labelNoPicture.Name = "labelNoPicture";
            this.labelNoPicture.Size = new System.Drawing.Size(158, 13);
            this.labelNoPicture.TabIndex = 5;
            this.labelNoPicture.Text = "No picture";
            this.labelNoPicture.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // pictureBoxPart
            //
            this.pictureBoxPart.BackColor = System.Drawing.SystemColors.Window;
            this.pictureBoxPart.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxPart.Location = new System.Drawing.Point(354, 58);
            this.pictureBoxPart.Name = "pictureBoxPart";
            this.pictureBoxPart.Size = new System.Drawing.Size(160, 160);
            this.pictureBoxPart.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxPart.TabIndex = 6;
            this.pictureBoxPart.TabStop = false;
            //
            // labelDetailDesignation
            //
            this.labelDetailDesignation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelDetailDesignation.Location = new System.Drawing.Point(524, 58);
            this.labelDetailDesignation.Name = "labelDetailDesignation";
            this.labelDetailDesignation.Size = new System.Drawing.Size(284, 32);
            this.labelDetailDesignation.TabIndex = 7;
            this.labelDetailDesignation.Text = "-";
            //
            // labelDetailManufacturer
            //
            this.labelDetailManufacturer.AutoEllipsis = true;
            this.labelDetailManufacturer.Location = new System.Drawing.Point(524, 96);
            this.labelDetailManufacturer.Name = "labelDetailManufacturer";
            this.labelDetailManufacturer.Size = new System.Drawing.Size(284, 13);
            this.labelDetailManufacturer.TabIndex = 8;
            this.labelDetailManufacturer.Text = "-";
            //
            // labelDetailOrderNumber
            //
            this.labelDetailOrderNumber.AutoEllipsis = true;
            this.labelDetailOrderNumber.Location = new System.Drawing.Point(524, 114);
            this.labelDetailOrderNumber.Name = "labelDetailOrderNumber";
            this.labelDetailOrderNumber.Size = new System.Drawing.Size(284, 13);
            this.labelDetailOrderNumber.TabIndex = 9;
            this.labelDetailOrderNumber.Text = "-";
            //
            // labelDetailTypeNumber
            //
            this.labelDetailTypeNumber.AutoEllipsis = true;
            this.labelDetailTypeNumber.Location = new System.Drawing.Point(524, 132);
            this.labelDetailTypeNumber.Name = "labelDetailTypeNumber";
            this.labelDetailTypeNumber.Size = new System.Drawing.Size(284, 13);
            this.labelDetailTypeNumber.TabIndex = 10;
            this.labelDetailTypeNumber.Text = "-";
            //
            // labelDetailPartNumber
            //
            this.labelDetailPartNumber.AutoEllipsis = true;
            this.labelDetailPartNumber.Location = new System.Drawing.Point(524, 150);
            this.labelDetailPartNumber.Name = "labelDetailPartNumber";
            this.labelDetailPartNumber.Size = new System.Drawing.Size(284, 13);
            this.labelDetailPartNumber.TabIndex = 11;
            this.labelDetailPartNumber.Text = "-";
            //
            // labelDetailProductFamily
            //
            this.labelDetailProductFamily.AutoEllipsis = true;
            this.labelDetailProductFamily.Location = new System.Drawing.Point(524, 168);
            this.labelDetailProductFamily.Name = "labelDetailProductFamily";
            this.labelDetailProductFamily.Size = new System.Drawing.Size(284, 13);
            this.labelDetailProductFamily.TabIndex = 12;
            this.labelDetailProductFamily.Text = "-";
            //
            // linkLabelPartPage
            //
            this.linkLabelPartPage.AutoSize = true;
            this.linkLabelPartPage.Location = new System.Drawing.Point(524, 192);
            this.linkLabelPartPage.Name = "linkLabelPartPage";
            this.linkLabelPartPage.Size = new System.Drawing.Size(121, 13);
            this.linkLabelPartPage.TabIndex = 13;
            this.linkLabelPartPage.TabStop = true;
            this.linkLabelPartPage.Text = "Open in the Data Portal";
            this.linkLabelPartPage.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelPartPage_LinkClicked);
            //
            // labelExistingItem
            //
            this.labelExistingItem.ForeColor = System.Drawing.Color.DarkRed;
            this.labelExistingItem.Location = new System.Drawing.Point(354, 224);
            this.labelExistingItem.Name = "labelExistingItem";
            this.labelExistingItem.Size = new System.Drawing.Size(454, 26);
            this.labelExistingItem.TabIndex = 14;
            //
            // labelNoSupplier
            //
            this.labelNoSupplier.ForeColor = System.Drawing.Color.DarkRed;
            this.labelNoSupplier.Location = new System.Drawing.Point(354, 250);
            this.labelNoSupplier.Name = "labelNoSupplier";
            this.labelNoSupplier.Size = new System.Drawing.Size(454, 26);
            this.labelNoSupplier.TabIndex = 15;
            //
            // dataGridViewFields
            //
            this.dataGridViewFields.AllowUserToAddRows = false;
            this.dataGridViewFields.AllowUserToDeleteRows = false;
            this.dataGridViewFields.AllowUserToResizeRows = false;
            this.dataGridViewFields.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewFields.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridViewFields.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewFields.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnTake,
            this.columnField,
            this.columnCurrent,
            this.columnPortal});
            this.dataGridViewFields.Location = new System.Drawing.Point(354, 280);
            this.dataGridViewFields.MultiSelect = false;
            this.dataGridViewFields.Name = "dataGridViewFields";
            this.dataGridViewFields.RowHeadersVisible = false;
            this.dataGridViewFields.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewFields.Size = new System.Drawing.Size(454, 228);
            this.dataGridViewFields.TabIndex = 16;
            //
            // columnTake
            //
            this.columnTake.FillWeight = 40F;
            this.columnTake.HeaderText = "Take";
            this.columnTake.Name = "columnTake";
            //
            // columnField
            //
            this.columnField.FillWeight = 70F;
            this.columnField.HeaderText = "Field";
            this.columnField.Name = "columnField";
            this.columnField.ReadOnly = true;
            //
            // columnCurrent
            //
            this.columnCurrent.HeaderText = "Current";
            this.columnCurrent.Name = "columnCurrent";
            this.columnCurrent.ReadOnly = true;
            //
            // columnPortal
            //
            this.columnPortal.HeaderText = "Data Portal";
            this.columnPortal.Name = "columnPortal";
            this.columnPortal.ReadOnly = true;
            //
            // buttonTake
            //
            this.buttonTake.Location = new System.Drawing.Point(622, 518);
            this.buttonTake.Name = "buttonTake";
            this.buttonTake.Size = new System.Drawing.Size(105, 26);
            this.buttonTake.TabIndex = 17;
            this.buttonTake.Text = "Take selected";
            this.buttonTake.UseVisualStyleBackColor = true;
            this.buttonTake.Click += new System.EventHandler(this.buttonTake_Click);
            //
            // buttonCancel
            //
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(733, 518);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 26);
            this.buttonCancel.TabIndex = 18;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            //
            // DataPortalPartForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(820, 556);
            this.Controls.Add(this.labelNoPicture);
            this.Controls.Add(this.pictureBoxPart);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonTake);
            this.Controls.Add(this.dataGridViewFields);
            this.Controls.Add(this.labelNoSupplier);
            this.Controls.Add(this.labelExistingItem);
            this.Controls.Add(this.linkLabelPartPage);
            this.Controls.Add(this.labelDetailProductFamily);
            this.Controls.Add(this.labelDetailPartNumber);
            this.Controls.Add(this.labelDetailTypeNumber);
            this.Controls.Add(this.labelDetailOrderNumber);
            this.Controls.Add(this.labelDetailManufacturer);
            this.Controls.Add(this.labelDetailDesignation);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.listBoxHits);
            this.Controls.Add(this.buttonSearch);
            this.Controls.Add(this.textBoxSearch);
            this.Controls.Add(this.labelSearch);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DataPortalPartForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "GLADOS SOLIDWORKS Add-in | EPLAN Data Portal";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.DataPortalPartForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFields)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelSearch;
        private System.Windows.Forms.TextBox textBoxSearch;
        private System.Windows.Forms.Button buttonSearch;
        private System.Windows.Forms.ListBox listBoxHits;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Label labelNoPicture;
        private System.Windows.Forms.PictureBox pictureBoxPart;
        private System.Windows.Forms.Label labelDetailDesignation;
        private System.Windows.Forms.Label labelDetailManufacturer;
        private System.Windows.Forms.Label labelDetailOrderNumber;
        private System.Windows.Forms.Label labelDetailTypeNumber;
        private System.Windows.Forms.Label labelDetailPartNumber;
        private System.Windows.Forms.Label labelDetailProductFamily;
        private System.Windows.Forms.LinkLabel linkLabelPartPage;
        private System.Windows.Forms.Label labelExistingItem;
        private System.Windows.Forms.Label labelNoSupplier;
        private System.Windows.Forms.DataGridView dataGridViewFields;
        private System.Windows.Forms.DataGridViewCheckBoxColumn columnTake;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnField;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnCurrent;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnPortal;
        private System.Windows.Forms.Button buttonTake;
        private System.Windows.Forms.Button buttonCancel;
    }
}

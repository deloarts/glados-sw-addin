namespace GladosSwAddin.Forms
{
    partial class SettingsForm
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
            this.tabControlSettings = new System.Windows.Forms.TabControl();
            this.tabPageAuth = new System.Windows.Forms.TabPage();
            this.buttonSettingsAuthTestPAT = new System.Windows.Forms.Button();
            this.textBoxSettingsAuthPAT = new System.Windows.Forms.TextBox();
            this.labelSettingsAuthPAT = new System.Windows.Forms.Label();
            this.textBoxSettingsAuthServerURL = new System.Windows.Forms.TextBox();
            this.labelSettingsAuthServerURL = new System.Windows.Forms.Label();
            this.tabPageFieldMapping = new System.Windows.Forms.TabPage();
            this.dataGridViewFieldMapping = new System.Windows.Forms.DataGridView();
            this.columnFieldMappingKind = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnFieldMappingField = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnFieldMappingOwner = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnFieldMappingProperty = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnFieldMappingUpdateInPlace = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.labelFieldMappingProvenance = new System.Windows.Forms.Label();
            this.labelFeatureTreeTemplates = new System.Windows.Forms.Label();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.linkLabelOpenLogFolder = new System.Windows.Forms.LinkLabel();
            this.labelSettingsVersion = new System.Windows.Forms.Label();
            this.tabControlSettings.SuspendLayout();
            this.tabPageAuth.SuspendLayout();
            this.tabPageFieldMapping.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFieldMapping)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControlSettings
            // 
            this.tabControlSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControlSettings.Controls.Add(this.tabPageAuth);
            this.tabControlSettings.Controls.Add(this.tabPageFieldMapping);
            this.tabControlSettings.Location = new System.Drawing.Point(0, 0);
            this.tabControlSettings.Name = "tabControlSettings";
            this.tabControlSettings.SelectedIndex = 0;
            this.tabControlSettings.Size = new System.Drawing.Size(620, 400);
            this.tabControlSettings.TabIndex = 0;
            // 
            // tabPageAuth
            // 
            this.tabPageAuth.Controls.Add(this.buttonSettingsAuthTestPAT);
            this.tabPageAuth.Controls.Add(this.textBoxSettingsAuthPAT);
            this.tabPageAuth.Controls.Add(this.labelSettingsAuthPAT);
            this.tabPageAuth.Controls.Add(this.textBoxSettingsAuthServerURL);
            this.tabPageAuth.Controls.Add(this.labelSettingsAuthServerURL);
            this.tabPageAuth.Location = new System.Drawing.Point(4, 22);
            this.tabPageAuth.Name = "tabPageAuth";
            this.tabPageAuth.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageAuth.Size = new System.Drawing.Size(612, 374);
            this.tabPageAuth.TabIndex = 0;
            this.tabPageAuth.Text = "Authentication";
            this.tabPageAuth.UseVisualStyleBackColor = true;
            // 
            // buttonSettingsAuthTestPAT
            // 
            this.buttonSettingsAuthTestPAT.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSettingsAuthTestPAT.Location = new System.Drawing.Point(486, 64);
            this.buttonSettingsAuthTestPAT.Name = "buttonSettingsAuthTestPAT";
            this.buttonSettingsAuthTestPAT.Size = new System.Drawing.Size(120, 23);
            this.buttonSettingsAuthTestPAT.TabIndex = 4;
            this.buttonSettingsAuthTestPAT.Text = "Test Credentials";
            this.buttonSettingsAuthTestPAT.UseVisualStyleBackColor = true;
            this.buttonSettingsAuthTestPAT.Click += new System.EventHandler(this.buttonSettingsAuthTestPAT_Click);
            // 
            // textBoxSettingsAuthPAT
            // 
            this.textBoxSettingsAuthPAT.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxSettingsAuthPAT.Location = new System.Drawing.Point(134, 38);
            this.textBoxSettingsAuthPAT.Name = "textBoxSettingsAuthPAT";
            this.textBoxSettingsAuthPAT.PasswordChar = '*';
            this.textBoxSettingsAuthPAT.Size = new System.Drawing.Size(472, 20);
            this.textBoxSettingsAuthPAT.TabIndex = 3;
            // 
            // labelSettingsAuthPAT
            // 
            this.labelSettingsAuthPAT.AutoSize = true;
            this.labelSettingsAuthPAT.Location = new System.Drawing.Point(8, 41);
            this.labelSettingsAuthPAT.Name = "labelSettingsAuthPAT";
            this.labelSettingsAuthPAT.Size = new System.Drawing.Size(120, 13);
            this.labelSettingsAuthPAT.TabIndex = 2;
            this.labelSettingsAuthPAT.Text = "Personal Access Token";
            // 
            // textBoxSettingsAuthServerURL
            // 
            this.textBoxSettingsAuthServerURL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxSettingsAuthServerURL.Location = new System.Drawing.Point(134, 12);
            this.textBoxSettingsAuthServerURL.Name = "textBoxSettingsAuthServerURL";
            this.textBoxSettingsAuthServerURL.Size = new System.Drawing.Size(472, 20);
            this.textBoxSettingsAuthServerURL.TabIndex = 1;
            // 
            // labelSettingsAuthServerURL
            // 
            this.labelSettingsAuthServerURL.AutoSize = true;
            this.labelSettingsAuthServerURL.Location = new System.Drawing.Point(8, 15);
            this.labelSettingsAuthServerURL.Name = "labelSettingsAuthServerURL";
            this.labelSettingsAuthServerURL.Size = new System.Drawing.Size(63, 13);
            this.labelSettingsAuthServerURL.TabIndex = 0;
            this.labelSettingsAuthServerURL.Text = "Server URL";
            // 
            // tabPageFieldMapping
            // 
            this.tabPageFieldMapping.Controls.Add(this.dataGridViewFieldMapping);
            this.tabPageFieldMapping.Controls.Add(this.labelFieldMappingProvenance);
            this.tabPageFieldMapping.Controls.Add(this.labelFeatureTreeTemplates);
            this.tabPageFieldMapping.Location = new System.Drawing.Point(4, 22);
            this.tabPageFieldMapping.Name = "tabPageFieldMapping";
            this.tabPageFieldMapping.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageFieldMapping.Size = new System.Drawing.Size(612, 374);
            this.tabPageFieldMapping.TabIndex = 1;
            this.tabPageFieldMapping.Text = "Field Mapping";
            this.tabPageFieldMapping.UseVisualStyleBackColor = true;
            // 
            // dataGridViewFieldMapping
            // 
            this.dataGridViewFieldMapping.AllowUserToAddRows = false;
            this.dataGridViewFieldMapping.AllowUserToDeleteRows = false;
            this.dataGridViewFieldMapping.AllowUserToResizeRows = false;
            this.dataGridViewFieldMapping.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridViewFieldMapping.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dataGridViewFieldMapping.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnFieldMappingKind,
            this.columnFieldMappingField,
            this.columnFieldMappingOwner,
            this.columnFieldMappingProperty,
            this.columnFieldMappingUpdateInPlace});
            this.dataGridViewFieldMapping.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.dataGridViewFieldMapping.Location = new System.Drawing.Point(8, 28);
            this.dataGridViewFieldMapping.MultiSelect = false;
            this.dataGridViewFieldMapping.Name = "dataGridViewFieldMapping";
            this.dataGridViewFieldMapping.ReadOnly = true;
            this.dataGridViewFieldMapping.RowHeadersVisible = false;
            this.dataGridViewFieldMapping.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewFieldMapping.Size = new System.Drawing.Size(596, 296);
            this.dataGridViewFieldMapping.TabIndex = 1;
            // 
            // columnFieldMappingKind
            // 
            this.columnFieldMappingKind.HeaderText = "Kind";
            this.columnFieldMappingKind.Name = "columnFieldMappingKind";
            this.columnFieldMappingKind.ReadOnly = true;
            this.columnFieldMappingKind.Width = 55;
            // 
            // columnFieldMappingField
            // 
            this.columnFieldMappingField.HeaderText = "Field";
            this.columnFieldMappingField.Name = "columnFieldMappingField";
            this.columnFieldMappingField.ReadOnly = true;
            this.columnFieldMappingField.Width = 150;
            // 
            // columnFieldMappingOwner
            // 
            this.columnFieldMappingOwner.HeaderText = "Owner";
            this.columnFieldMappingOwner.Name = "columnFieldMappingOwner";
            this.columnFieldMappingOwner.ReadOnly = true;
            this.columnFieldMappingOwner.Width = 90;
            // 
            // columnFieldMappingProperty
            // 
            this.columnFieldMappingProperty.HeaderText = "Custom Property";
            this.columnFieldMappingProperty.Name = "columnFieldMappingProperty";
            this.columnFieldMappingProperty.ReadOnly = true;
            this.columnFieldMappingProperty.Width = 150;
            // 
            // columnFieldMappingUpdateInPlace
            // 
            this.columnFieldMappingUpdateInPlace.HeaderText = "Update in Place";
            this.columnFieldMappingUpdateInPlace.Name = "columnFieldMappingUpdateInPlace";
            this.columnFieldMappingUpdateInPlace.ReadOnly = true;
            this.columnFieldMappingUpdateInPlace.Width = 110;
            // 
            // labelFieldMappingProvenance
            // 
            this.labelFieldMappingProvenance.AutoSize = true;
            this.labelFieldMappingProvenance.Location = new System.Drawing.Point(8, 8);
            this.labelFieldMappingProvenance.Name = "labelFieldMappingProvenance";
            this.labelFieldMappingProvenance.Size = new System.Drawing.Size(127, 13);
            this.labelFieldMappingProvenance.TabIndex = 0;
            this.labelFieldMappingProvenance.Text = "Defaults — not yet synced";
            //
            // labelFeatureTreeTemplates
            //
            this.labelFeatureTreeTemplates.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelFeatureTreeTemplates.AutoSize = false;
            this.labelFeatureTreeTemplates.Location = new System.Drawing.Point(8, 328);
            this.labelFeatureTreeTemplates.Name = "labelFeatureTreeTemplates";
            this.labelFeatureTreeTemplates.Size = new System.Drawing.Size(596, 34);
            this.labelFeatureTreeTemplates.TabIndex = 2;
            this.labelFeatureTreeTemplates.Text = "Feature tree, made: \r\nFeature tree, bought: ";
            //
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(459, 409);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(75, 23);
            this.buttonSave.TabIndex = 1;
            this.buttonSave.Text = "Save";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(540, 409);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // linkLabelOpenLogFolder
            // 
            this.linkLabelOpenLogFolder.AutoSize = true;
            this.linkLabelOpenLogFolder.Location = new System.Drawing.Point(12, 414);
            this.linkLabelOpenLogFolder.Name = "linkLabelOpenLogFolder";
            this.linkLabelOpenLogFolder.Size = new System.Drawing.Size(88, 13);
            this.linkLabelOpenLogFolder.TabIndex = 3;
            this.linkLabelOpenLogFolder.TabStop = true;
            this.linkLabelOpenLogFolder.Text = "Open log folder";
            this.linkLabelOpenLogFolder.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelOpenLogFolder_LinkClicked);
            //
            // labelSettingsVersion
            //
            this.labelSettingsVersion.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelSettingsVersion.Location = new System.Drawing.Point(253, 414);
            this.labelSettingsVersion.Name = "labelSettingsVersion";
            this.labelSettingsVersion.Size = new System.Drawing.Size(200, 13);
            this.labelSettingsVersion.TabIndex = 4;
            this.labelSettingsVersion.Text = "Version";
            this.labelSettingsVersion.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // SettingsForm
            // 
            this.AcceptButton = this.buttonSave;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(624, 441);
            this.Controls.Add(this.labelSettingsVersion);
            this.Controls.Add(this.linkLabelOpenLogFolder);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.tabControlSettings);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "GLADOS SOLIDWORKS Add-in | Settings";
            this.TopMost = true;
            this.tabControlSettings.ResumeLayout(false);
            this.tabPageAuth.ResumeLayout(false);
            this.tabPageAuth.PerformLayout();
            this.tabPageFieldMapping.ResumeLayout(false);
            this.tabPageFieldMapping.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFieldMapping)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControlSettings;
        private System.Windows.Forms.TabPage tabPageAuth;
        private System.Windows.Forms.TabPage tabPageFieldMapping;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.LinkLabel linkLabelOpenLogFolder;
        private System.Windows.Forms.Label labelSettingsVersion;
        private System.Windows.Forms.Label labelSettingsAuthServerURL;
        private System.Windows.Forms.TextBox textBoxSettingsAuthServerURL;
        private System.Windows.Forms.TextBox textBoxSettingsAuthPAT;
        private System.Windows.Forms.Label labelSettingsAuthPAT;
        private System.Windows.Forms.Button buttonSettingsAuthTestPAT;
        private System.Windows.Forms.Label labelFieldMappingProvenance;
        private System.Windows.Forms.Label labelFeatureTreeTemplates;
        private System.Windows.Forms.DataGridView dataGridViewFieldMapping;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnFieldMappingKind;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnFieldMappingField;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnFieldMappingOwner;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnFieldMappingProperty;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnFieldMappingUpdateInPlace;
    }
}
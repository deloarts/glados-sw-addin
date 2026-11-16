namespace GladosSwAddin.Forms
{
    partial class SaveConflictForm
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
            this.labelIntro = new System.Windows.Forms.Label();
            this.dataGridViewConflicts = new System.Windows.Forms.DataGridView();
            this.columnField = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnMine = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.buttonUseMine = new System.Windows.Forms.Button();
            this.buttonKeepFile = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewConflicts)).BeginInit();
            this.SuspendLayout();
            //
            // labelIntro
            //
            this.labelIntro.Location = new System.Drawing.Point(12, 12);
            this.labelIntro.Name = "labelIntro";
            this.labelIntro.Size = new System.Drawing.Size(496, 42);
            this.labelIntro.TabIndex = 0;
            this.labelIntro.Text = "These fields were changed in the file since it was loaded, and you changed them too.";
            //
            // dataGridViewConflicts
            //
            this.dataGridViewConflicts.AllowUserToAddRows = false;
            this.dataGridViewConflicts.AllowUserToDeleteRows = false;
            this.dataGridViewConflicts.AllowUserToResizeRows = false;
            this.dataGridViewConflicts.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewConflicts.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dataGridViewConflicts.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridViewConflicts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewConflicts.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnField,
            this.columnFile,
            this.columnMine});
            this.dataGridViewConflicts.Location = new System.Drawing.Point(12, 60);
            this.dataGridViewConflicts.MultiSelect = false;
            this.dataGridViewConflicts.Name = "dataGridViewConflicts";
            this.dataGridViewConflicts.ReadOnly = true;
            this.dataGridViewConflicts.RowHeadersVisible = false;
            this.dataGridViewConflicts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewConflicts.Size = new System.Drawing.Size(496, 180);
            this.dataGridViewConflicts.TabIndex = 1;
            //
            // columnField
            //
            this.columnField.FillWeight = 60F;
            this.columnField.HeaderText = "Field";
            this.columnField.Name = "columnField";
            this.columnField.ReadOnly = true;
            //
            // columnFile
            //
            this.columnFile.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.columnFile.HeaderText = "In the file";
            this.columnFile.Name = "columnFile";
            this.columnFile.ReadOnly = true;
            //
            // columnMine
            //
            this.columnMine.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.columnMine.HeaderText = "Yours";
            this.columnMine.Name = "columnMine";
            this.columnMine.ReadOnly = true;
            //
            // buttonUseMine
            //
            this.buttonUseMine.Location = new System.Drawing.Point(182, 250);
            this.buttonUseMine.Name = "buttonUseMine";
            this.buttonUseMine.Size = new System.Drawing.Size(110, 26);
            this.buttonUseMine.TabIndex = 2;
            this.buttonUseMine.Text = "Save mine";
            this.buttonUseMine.UseVisualStyleBackColor = true;
            this.buttonUseMine.Click += new System.EventHandler(this.buttonUseMine_Click);
            //
            // buttonKeepFile
            //
            this.buttonKeepFile.Location = new System.Drawing.Point(298, 250);
            this.buttonKeepFile.Name = "buttonKeepFile";
            this.buttonKeepFile.Size = new System.Drawing.Size(129, 26);
            this.buttonKeepFile.TabIndex = 3;
            this.buttonKeepFile.Text = "Keep file values";
            this.buttonKeepFile.UseVisualStyleBackColor = true;
            this.buttonKeepFile.Click += new System.EventHandler(this.buttonKeepFile_Click);
            //
            // buttonCancel
            //
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(433, 250);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 26);
            this.buttonCancel.TabIndex = 4;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            //
            // SaveConflictForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(520, 288);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonKeepFile);
            this.Controls.Add(this.buttonUseMine);
            this.Controls.Add(this.dataGridViewConflicts);
            this.Controls.Add(this.labelIntro);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SaveConflictForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "GLADOS SOLIDWORKS Add-in | Changed in the file";
            this.TopMost = true;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewConflicts)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label labelIntro;
        private System.Windows.Forms.DataGridView dataGridViewConflicts;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnField;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnMine;
        private System.Windows.Forms.Button buttonUseMine;
        private System.Windows.Forms.Button buttonKeepFile;
        private System.Windows.Forms.Button buttonCancel;
    }
}

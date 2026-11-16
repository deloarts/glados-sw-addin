namespace GladosSwAddin
{
    partial class DesignerProbeControl
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

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.labelProbe = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // labelProbe
            //
            this.labelProbe.AutoSize = true;
            this.labelProbe.Location = new System.Drawing.Point(10, 10);
            this.labelProbe.Name = "labelProbe";
            this.labelProbe.Size = new System.Drawing.Size(100, 13);
            this.labelProbe.TabIndex = 0;
            this.labelProbe.Text = "Designer probe";
            //
            // DesignerProbeControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.labelProbe);
            this.Name = "DesignerProbeControl";
            this.Size = new System.Drawing.Size(200, 100);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label labelProbe;
    }
}

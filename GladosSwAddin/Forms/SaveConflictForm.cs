using System.Collections.Generic;
using System.Windows.Forms;
using GladosSwAddin.Resources;

namespace GladosSwAddin.Forms
{
    /// <summary>
    /// Modal question a Save asks when a field changed in the file since the task pane loaded
    /// it (a PDM data card, File &gt; Properties) and the user changed the same field to
    /// something else in the pane — see SaveMergeService. Fields only one side changed never
    /// reach here: Save keeps the outside change or the user's edit on its own. The choice
    /// covers every listed field at once; a per-field pick was left out on purpose, since a
    /// conflict means two people edited the same item at the same time and the user needs to
    /// look at the result either way. Writes nothing itself.
    /// </summary>
    public partial class SaveConflictForm : Form
    {
        public SaveConflictChoice Choice { get; private set; } = SaveConflictChoice.Cancel;

        public SaveConflictForm(IEnumerable<SaveConflictRow> rows)
        {
            InitializeComponent();

            Text = Strings.SaveConflictFormTitleText;
            labelIntro.Text = Strings.LabelSaveConflictIntroText;
            columnField.HeaderText = Strings.ColumnSaveConflictFieldText;
            columnFile.HeaderText = Strings.ColumnSaveConflictFileText;
            columnMine.HeaderText = Strings.ColumnSaveConflictMineText;
            buttonUseMine.Text = Strings.ButtonSaveConflictUseMineText;
            buttonKeepFile.Text = Strings.ButtonSaveConflictKeepFileText;
            buttonCancel.Text = Strings.ButtonSaveConflictCancelText;

            foreach (var row in rows ?? new SaveConflictRow[0])
                dataGridViewConflicts.Rows.Add(row.Field, row.FileValue, row.MineValue);
        }

        private void buttonUseMine_Click(object sender, System.EventArgs e)
        {
            Choice = SaveConflictChoice.UseMine;
            DialogResult = DialogResult.OK;
        }

        private void buttonKeepFile_Click(object sender, System.EventArgs e)
        {
            Choice = SaveConflictChoice.KeepFile;
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>What the user chose in SaveConflictForm.</summary>
    public enum SaveConflictChoice
    {
        Cancel,
        UseMine,
        KeepFile,
    }

    /// <summary>One conflicting field as the dialog shows it: its label and both values,
    /// already formatted for display by the caller (which has the lookups to turn ids into
    /// names).</summary>
    public sealed class SaveConflictRow
    {
        public SaveConflictRow(string field, string fileValue, string mineValue)
        {
            Field = field;
            FileValue = fileValue;
            MineValue = mineValue;
        }

        public string Field { get; }

        public string FileValue { get; }

        public string MineValue { get; }
    }
}

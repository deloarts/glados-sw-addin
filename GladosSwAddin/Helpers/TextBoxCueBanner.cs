using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GladosSwAddin.Helpers
{
    /// <summary>Wraps the native Win32 EM_SETCUEBANNER message so a TextBox can show placeholder
    /// text without any WinForms-level state to keep in sync: the cue banner is a property of
    /// the underlying edit control itself, drawn automatically whenever the control's actual
    /// text is empty and hidden the instant real text is set — including through
    /// TextBox.Text — so callers never need to clear it by hand when a real value arrives.
    /// Used for textBoxPartNumber/textBoxRevision (GladosTaskPaneControl), which are read-only:
    /// a normal watermark implementation that intercepts Paint/Focus doesn't fit a control the
    /// user can't type into, but the native cue banner works unchanged since it depends only on
    /// the control's text length, not on focus or editability.</summary>
    public static class TextBoxCueBanner
    {
        private const int EmSetCueBanner = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Sets (or, given an empty string, clears) <paramref name="textBox"/>'s cue
        /// banner. <paramref name="showWhenFocused"/> defaults to true because these boxes are
        /// read-only — there is no in-progress typing a persistent banner could get in the way
        /// of — so the placeholder stays visible even if the control ever receives focus (e.g.
        /// Tab order).</summary>
        public static void SetCueBanner(TextBox textBox, string text, bool showWhenFocused = true)
        {
            SendMessage(textBox.Handle, EmSetCueBanner, (IntPtr)(showWhenFocused ? 1 : 0), text ?? string.Empty);
        }
    }
}

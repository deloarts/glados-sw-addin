namespace GladosSwAddin.Helpers
{
    public static class MaterialIcons
    {
        public const string Cloud = "\uE2BD";
        public const string CloudOff = "\uE2C1";
        // Google "error_outline". Used for the rejected-PAT state; the previous U+F3CC was a
        // Material Design Icons (mdi) codepoint, "cloud-alert", which the embedded Google
        // Material Icons font doesn't contain, so it rendered as a fallback glyph.
        public const string ErrorOutline = "\uE001";
        public const string CloudUpload = "\uE2C3";
        public const string CloudDownload = "\uE2C0";
        public const string CloudDone = "\uE2BF";
        public const string CheckCircle = "\uE86C";
        public const string Block = "\uE14B";
        public const string Autorenew = "\uE863";

        /// <summary>"link" - joined-boundary glyph for ProcessRoutingControl's link/split
        /// toggle between two routing rows. Verified present in the embedded
        /// MaterialIcons-Regular.ttf's cmap (U+E157) via fontTools before use.</summary>
        public const string Link = "\uE157";

        /// <summary>"call_split" - the nearest Material Icons equivalent to the web UI's
        /// mdi-arrow-split-vertical (Google's Material Icons font has no arrow-split-vertical
        /// glyph). U+E0B6 per Google's official MaterialIcons-Regular.codepoints (U+E155 is
        /// "forward", not call_split); verified present in the embedded font's cmap via
        /// fontTools before use. ProcessRoutingControl uses LinkOff instead (a closer visual
        /// match to Link for the same split-boundary meaning); kept here as a verified glyph
        /// constant in case a future card needs an actual "split" icon.</summary>
        public const string CallSplit = "\uE0B6";

        /// <summary>"link_off" - split-boundary glyph for ProcessRoutingControl: shown on an
        /// active (split) boundary toggle, and as the fixed, non-interactive marker beside an
        /// inspection step (a QA checkpoint is always standalone, never a user choice - see
        /// ProcessRoutingControl's class doc comment). U+E16F per Google's official
        /// MaterialIcons-Regular.codepoints; verified present in the embedded font's cmap via
        /// fontTools before use.</summary>
        public const string LinkOff = "\uE16F";

        /// <summary>"keyboard_arrow_up" - ProcessRoutingControl's move-step-up button. U+E316
        /// per Google's official MaterialIcons-Regular.codepoints; verified present in the
        /// embedded font's cmap via fontTools before use.</summary>
        public const string KeyboardArrowUp = "\uE316";

        /// <summary>"keyboard_arrow_down" - ProcessRoutingControl's move-step-down button.
        /// U+E313 per Google's official MaterialIcons-Regular.codepoints; verified present in
        /// the embedded font's cmap via fontTools before use.</summary>
        public const string KeyboardArrowDown = "\uE313";

        /// <summary>"warning" - the task pane's Downloads-reference warning. U+E002 per Google's
        /// official MaterialIcons-Regular.codepoints; verified present in the embedded font's
        /// cmap before use.</summary>
        public const string Warning = "\uE002";
    }
}

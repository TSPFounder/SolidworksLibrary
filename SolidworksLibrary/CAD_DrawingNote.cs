
using System;

namespace CAD
{
    /// <summary>
    /// Simple POCO representing a drawing note with an ID, text, and categorical type.
    /// </summary>
    public class CAD_DrawingNote
    {
        // -----------------------------
        // Types
        // -----------------------------
        public enum NoteType
        {
            General = 0,
            Safety,
            Process,
            Material,
            Finish,
            Reference,
            Tolerance,
            Other
        }

        // -----------------------------
        // Construction
        // -----------------------------
        public CAD_DrawingNote() { }

        public CAD_DrawingNote(string id, string text, NoteType type = NoteType.General)
        {
            DrawingNoteID = id;
            NoteText = text;
            MyNoteType = type;
        }

        // -----------------------------
        // Properties
        // -----------------------------
        /// <summary>Unique identifier for the note (optional).</summary>
        public string DrawingNoteID { get; set; }

        /// <summary>Displayed note text (may be multi-line).</summary>
        public string NoteText { get; set; }

        /// <summary>Category of the note to aid filtering/searching.</summary>
        public NoteType MyNoteType { get; set; } = NoteType.General;

        // -----------------------------
        // Helpers
        // -----------------------------
        /// <summary>Returns true if the note has no visible text.</summary>
        public bool IsEmpty() => string.IsNullOrWhiteSpace(NoteText);

        /// <summary>Updates note text, trimming leading/trailing whitespace.</summary>
        public void SetText(string text) => NoteText = text?.Trim();

        public override string ToString()
            => string.IsNullOrWhiteSpace(NoteText)
                ? $"[{MyNoteType}] (empty)"
                : $"[{MyNoteType}] {NoteText}";
    }
}


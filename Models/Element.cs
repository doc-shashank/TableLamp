using System;

namespace TableLamp.Models
{
    /// <summary>
    /// Element class holding un-formatted text, formatted text, or user specified image.
    /// </summary>
    public class Element
    {
        /// <summary>
        /// Holds un-formatted String.
        /// </summary>
        public string? simple_text { get; set; }

        /// <summary>
        /// Holds formatted String (e.g. Markdown, HTML, or RTF).
        /// </summary>
        public string? formatted_text { get; set; }

        /// <summary>
        /// Holds a user specified image (BitmapImage, byte array, file path, or ImageSource).
        /// </summary>
        public object? simple_image { get; set; }

        // Idiomatic C# aliases
        public string? SimpleText { get => simple_text; set => simple_text = value; }
        public string? FormattedText { get => formatted_text; set => formatted_text = value; }
        public object? SimpleImage { get => simple_image; set => simple_image = value; }

        public Element()
        {
        }

        public Element(string? simpleText, string? formattedText = null, object? simpleImage = null)
        {
            this.simple_text = simpleText;
            this.formatted_text = formattedText;
            this.simple_image = simpleImage;
        }

        public static Element FromText(string text) => new Element(simpleText: text);
        public static Element FromFormattedText(string formatted) => new Element(simpleText: null, formattedText: formatted);
        public static Element FromImage(object image, string? altText = null) => new Element(simpleText: altText, simpleImage: image);

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(simple_text)) return simple_text;
            if (!string.IsNullOrEmpty(formatted_text)) return formatted_text;
            if (simple_image != null) return "[Image Element]";
            return "[Empty Element]";
        }
    }
}

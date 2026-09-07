using System;
using System.Linq;

namespace TableLamp.Models
{
    /// <summary>
    /// SimpleQuestion is an extension of BaseQuestion that just takes Question and not Answer.
    /// In v0.0.2, question elements can independently contain text, image, or both.
    /// </summary>
    public class SimpleQuestion : BaseQuestion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public Element? TextElement =>
            question_element_array?.FirstOrDefault(e => !string.IsNullOrEmpty(e.simple_text) || !string.IsNullOrEmpty(e.formatted_text));

        public Element? ImageElement =>
            question_element_array?.FirstOrDefault(e => e.simple_image != null);

        public bool HasText => TextElement != null;
        public bool HasImage => ImageElement != null;

        public string SummaryText
        {
            get
            {
                if (HasText)
                {
                    string text = TextElement?.simple_text ?? TextElement?.formatted_text ?? "";
                    if (text.Length > 80) return text.Substring(0, 77) + "...";
                    return text;
                }
                if (HasImage)
                {
                    return "[Image Question]";
                }
                return "[Empty Question]";
            }
        }

        public SimpleQuestion()
        {
            answer_element_array = Array.Empty<Element>();
        }

        /// <summary>
        /// Initializes a SimpleQuestion taking only question elements and no answer elements.
        /// </summary>
        public SimpleQuestion(Element[] questionElements, Tag? tag = null, bool isEditable = true)
            : base(questionElements, Array.Empty<Element>(), tag, isEditable)
        {
        }

        /// <summary>
        /// Convenience constructor taking a single text question.
        /// </summary>
        public SimpleQuestion(string questionText, Tag? tag = null, bool isEditable = true)
            : base(new[] { Element.FromText(questionText) }, Array.Empty<Element>(), tag, isEditable)
        {
        }

        /// <summary>
        /// Convenience constructor taking both text and image elements independently.
        /// </summary>
        public SimpleQuestion(string? text, object? image, Tag? tag = null, bool isEditable = true)
            : base(CreateElements(text, image), Array.Empty<Element>(), tag, isEditable)
        {
        }

        private static Element[] CreateElements(string? text, object? image)
        {
            var list = new System.Collections.Generic.List<Element>();
            if (!string.IsNullOrWhiteSpace(text))
            {
                list.Add(Element.FromText(text));
            }
            if (image != null)
            {
                list.Add(new Element { simple_image = image });
            }
            return list.ToArray();
        }

        public override string ToString()
        {
            int qCount = question_element_array?.Length ?? 0;
            return $"SimpleQuestion: {qCount} Element(s) (HasText: {HasText}, HasImage: {HasImage}), Tag: {tag?.subject_name ?? "None"}, isEditable: {isEditable}";
        }
    }
}

using System;

namespace TableLamp.Models
{
    /// <summary>
    /// BaseQuestion class containing question elements, answer elements, a Tag, and an isEditable flag.
    /// </summary>
    public class BaseQuestion
    {
        /// <summary>
        /// Instances of Element stored in array representing the Question.
        /// </summary>
        public Element[] question_element_array { get; set; } = Array.Empty<Element>();

        /// <summary>
        /// Instances of Element stored in array representing the Answer.
        /// </summary>
        public Element[] answer_element_array { get; set; } = Array.Empty<Element>();

        /// <summary>
        /// Associated Tag instance holding metadata.
        /// </summary>
        public Tag? tag { get; set; }

        /// <summary>
        /// Indicates if the Question is editable or not.
        /// </summary>
        public bool isEditable { get; set; } = true;

        // Idiomatic C# aliases
        public Element[] QuestionElements { get => question_element_array; set => question_element_array = value; }
        public Element[] AnswerElements { get => answer_element_array; set => answer_element_array = value; }
        public Tag? Tag { get => tag; set => tag = value; }
        public bool IsEditable { get => isEditable; set => isEditable = value; }

        public BaseQuestion()
        {
        }

        public BaseQuestion(Element[] questionElements, Element[]? answerElements = null, Tag? tag = null, bool isEditable = true)
        {
            this.question_element_array = questionElements ?? Array.Empty<Element>();
            this.answer_element_array = answerElements ?? Array.Empty<Element>();
            this.tag = tag;
            this.isEditable = isEditable;
        }

        public override string ToString()
        {
            int qCount = question_element_array?.Length ?? 0;
            int aCount = answer_element_array?.Length ?? 0;
            return $"BaseQuestion: {qCount} Question Element(s), {aCount} Answer Element(s), Tag: {tag?.subject_name ?? "None"}, isEditable: {isEditable}";
        }
    }
}

using System;
using System.Collections.Generic;

namespace TableLamp.Models
{
    /// <summary>
    /// Bundle is a collection of BaseQuestion instances with creation, review schedule, and curation properties.
    /// </summary>
    public class Bundle
    {
        private readonly List<BaseQuestion> _questions = new();
        private bool _isCurated;

        /// <summary>
        /// Collection of BaseQuestion instances contained in the Bundle.
        /// </summary>
        public IReadOnlyList<BaseQuestion> questions => _questions.AsReadOnly();

        /// <summary>
        /// Date when the bundle was created and stored by User.
        /// </summary>
        public DateTime creation_date { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the bundle is scheduled to be reviewed next.
        /// </summary>
        public DateTime? next_review_date { get; set; }

        /// <summary>
        /// Determines if the Bundle is editable or not.
        /// If isCurated = true, all BaseQuestion instances have their isEditable Boolean turned to false.
        /// </summary>
        public bool isCurated
        {
            get => _isCurated;
            set
            {
                _isCurated = value;
                if (_isCurated)
                {
                    ApplyCuratedRule();
                }
            }
        }

        // Idiomatic C# aliases
        public DateTime CreationDate { get => creation_date; set => creation_date = value; }
        public DateTime? NextReviewDate { get => next_review_date; set => next_review_date = value; }
        public bool IsCurated { get => isCurated; set => isCurated = value; }
        public IReadOnlyList<BaseQuestion> Questions => questions;

        public Bundle()
        {
        }

        public Bundle(IEnumerable<BaseQuestion>? initialQuestions, bool isCurated = false, DateTime? nextReviewDate = null)
        {
            creation_date = DateTime.UtcNow;
            next_review_date = nextReviewDate;

            if (initialQuestions != null)
            {
                foreach (var q in initialQuestions)
                {
                    AddQuestion(q);
                }
            }

            this.isCurated = isCurated;
        }

        /// <summary>
        /// Adds a BaseQuestion to this bundle. If the bundle is curated, the question's isEditable is set to false.
        /// </summary>
        public virtual void AddQuestion(BaseQuestion question)
        {
            if (question == null) throw new ArgumentNullException(nameof(question));

            if (_isCurated)
            {
                question.isEditable = false;
            }

            _questions.Add(question);
        }

        /// <summary>
        /// Adds multiple BaseQuestions to this bundle.
        /// </summary>
        public virtual void AddQuestions(IEnumerable<BaseQuestion> questionsToAdd)
        {
            if (questionsToAdd == null) return;
            foreach (var q in questionsToAdd)
            {
                AddQuestion(q);
            }
        }

        /// <summary>
        /// Removes a BaseQuestion from this bundle.
        /// </summary>
        public virtual bool RemoveQuestion(BaseQuestion question)
        {
            return _questions.Remove(question);
        }

        /// <summary>
        /// Clears all questions from this bundle.
        /// </summary>
        public virtual void ClearQuestions()
        {
            _questions.Clear();
        }

        /// <summary>
        /// Applies the curation rule: turns isEditable to false for all BaseQuestion instances.
        /// </summary>
        protected void ApplyCuratedRule()
        {
            foreach (var question in _questions)
            {
                question.isEditable = false;
            }
        }

        public override string ToString()
        {
            return $"Bundle: {_questions.Count} questions, Curated: {isCurated}, Created: {creation_date:yyyy-MM-dd}, Next Review: {next_review_date:yyyy-MM-dd}";
        }
    }
}

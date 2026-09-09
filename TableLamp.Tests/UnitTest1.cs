using System;
using TableLamp.Models;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TableLamp.Tests
{
    public class ModelAndExtensionTests
    {
        [Fact]
        public void Tag_InitializesAndHoldsPropertiesCorrectly()
        {
            var tag = new Tag
            {
                id = 42,
                subject_name = "Physics",
                chapter_number = 4,
                chapter_name = "Thermodynamics",
                topic_name = "Carnot Engine"
            };

            Assert.Equal(42, tag.id);
            Assert.Equal("Physics", tag.subject_name);
            Assert.Equal(4, tag.chapter_number);
            Assert.Equal("Thermodynamics", tag.chapter_name);
            Assert.Equal("Carnot Engine", tag.topic_name);

            // Aliases
            Assert.Equal(42, tag.Id);
            Assert.Equal("Physics", tag.SubjectName);
            Assert.Equal(4, tag.ChapterNumber);
            Assert.Equal("Thermodynamics", tag.ChapterName);
            Assert.Equal("Carnot Engine", tag.TopicName);
        }

        [Fact]
        public void Element_InitializesSimpleAndFormattedTextAndImage()
        {
            var textElement = new Element { simple_text = "What is momentum?" };
            Assert.Equal("What is momentum?", textElement.simple_text);
            Assert.Null(textElement.formatted_text);
            Assert.Null(textElement.simple_image);

            var formattedElement = new Element { formatted_text = "**p = mv**" };
            Assert.Equal("**p = mv**", formattedElement.formatted_text);

            var imageElem = new Element { simple_image = "path/to/diagram.png" };
            Assert.Equal("path/to/diagram.png", imageElem.simple_image);
        }

        [Fact]
        public void BaseQuestion_HoldsComponentsCorrectly()
        {
            var tag = new Tag(1, "Chemistry", 2, "Atomic Structure", "Bohr Model");
            var qElem = Element.FromText("What is the Rydberg formula?");
            var aElem = Element.FromFormattedText("1/lambda = R*(1/n1^2 - 1/n2^2)");

            var bq = new BaseQuestion(new[] { qElem }, new[] { aElem }, tag, isEditable: true);

            Assert.Single(bq.question_element_array);
            Assert.Equal(qElem, bq.question_element_array[0]);
            Assert.Single(bq.answer_element_array);
            Assert.Equal(aElem, bq.answer_element_array[0]);
            Assert.Equal(tag, bq.tag);
            Assert.True(bq.isEditable);
        }

        [Fact]
        public void SimpleQuestion_ExtendsBaseQuestionAndTakesOnlyQuestionElements()
        {
            var tag = new Tag(2, "History", 1, "Ancient Civilizations", "Mesopotamia");
            var qElem = Element.FromText("Where was the Code of Hammurabi created?");

            var sq = new SimpleQuestion(new[] { qElem }, tag, isEditable: true);

            Assert.IsAssignableFrom<BaseQuestion>(sq);
            Assert.Single(sq.question_element_array);
            Assert.Empty(sq.answer_element_array);
            Assert.Equal(tag, sq.tag);
            Assert.True(sq.isEditable);
        }

        [Fact]
        public void Bundle_CuratedBehavior_TurnsAllQuestionsNonEditable()
        {
            var q1 = new BaseQuestion(new[] { Element.FromText("Q1") }, new[] { Element.FromText("A1") }, isEditable: true);
            var q2 = new SimpleQuestion("Q2", isEditable: true);

            var bundle = new Bundle(new[] { q1, q2 }, isCurated: false);

            Assert.False(bundle.isCurated);
            Assert.True(q1.isEditable);
            Assert.True(q2.isEditable);

            // Act: Turn isCurated = true
            bundle.isCurated = true;

            // Assert: All BaseQuestion instances have isEditable set to false
            Assert.True(bundle.isCurated);
            Assert.False(q1.isEditable);
            Assert.False(q2.isEditable);

            // Adding new question to curated bundle also sets isEditable to false
            var q3 = new BaseQuestion(new[] { Element.FromText("Q3") }, isEditable: true);
            bundle.AddQuestion(q3);
            Assert.False(q3.isEditable);
        }

        [Fact]
        public void BasicSessionBundle_ExtendsBundleAndInheritsMethods()
        {
            var q = new BaseQuestion(new[] { Element.FromText("Session Question") });
            var sessionBundle = new BasicSessionBundle(new[] { q }, isCurated: false, sessionName: "Morning Warmup")
            {
                next_review_date = DateTime.UtcNow.AddDays(7)
            };

            Assert.IsAssignableFrom<Bundle>(sessionBundle);
            Assert.Equal("Morning Warmup", sessionBundle.SessionName);
            Assert.Single(sessionBundle.questions);
            Assert.NotNull(sessionBundle.next_review_date);

            sessionBundle.isCurated = true;
            Assert.False(q.isEditable);
        }

        [Theory]
        [InlineData("Basic", "Basic")]
        [InlineData("basic", "Basic")]
        [InlineData("BASIC", "Basic")]
        [InlineData("Advanced", "Advanced")]
        [InlineData("advanced", "Advanced")]
        [InlineData("Generator", "Generator")]
        [InlineData("generator", "Generator")]
        [InlineData("", "Basic")]
        [InlineData("   ", "Basic")]
        [InlineData(null, "Basic")]
        [InlineData("UnknownMode", "Basic")]
        public void LauncherMode_Normalize_ValidatesAndNormalizesArguments(string? input, string expected)
        {
            string actual = LauncherMode.Normalize(input);
            Assert.Equal(expected, actual);
        }
    }
}
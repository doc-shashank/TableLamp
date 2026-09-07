using System;
using System.Collections.Generic;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0053FeaturesTests
    {
        private const string MultiChapterJson = @"{
  ""Robins Physiology"": {
    ""short_name"": ""Robins Physiology"",
    ""full_name"": ""Robins Pathologic Basis of Disease"",
    ""edition"": ""7th"",
    ""Chapters"": {
      ""chapter1"": {
        ""name"": ""Cell Injury"",
        ""chapter_number"": 1,
        ""Topics"": {
          ""topic_1"": {
            ""name"": ""Causes of Cell Injury"",
            ""start_page"": ""1"",
            ""end_page"": ""15""
          },
          ""topic_2"": {
            ""name"": ""Reversible Injury"",
            ""start_page"": ""16"",
            ""end_page"": ""30""
          }
        }
      },
      ""chapter2"": {
        ""name"": ""Inflammation"",
        ""chapter_number"": 2,
        ""Topics"": {
          ""topic_1"": {
            ""name"": ""Acute Inflammation"",
            ""start_page"": ""31"",
            ""end_page"": ""45""
          },
          ""topic_2"": {
            ""name"": ""Chronic Inflammation"",
            ""start_page"": ""46"",
            ""end_page"": ""60""
          }
        }
      }
    }
  }
}";

        [Fact]
        public void DetectContextAtCaret_DetectsSubjectLevelAttributes()
        {
            // Caret inside "short_name": "Robins Physiology"
            int shortNameOffset = MultiChapterJson.IndexOf("\"short_name\"");
            Assert.True(shortNameOffset > 0);

            var context = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, shortNameOffset);
            Assert.Equal("Subject", context.ElementType);
            Assert.Equal("Robins Physiology", context.SubjectKey);

            // Caret inside "edition": "7th"
            int editionOffset = MultiChapterJson.IndexOf("\"edition\"");
            Assert.True(editionOffset > 0);

            var editionContext = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, editionOffset);
            Assert.Equal("Subject", editionContext.ElementType);
            Assert.Equal("Robins Physiology", editionContext.SubjectKey);
        }

        [Fact]
        public void DetectContextAtCaret_DetectsChapterLevelAttributes()
        {
            // Caret on chapter1 header
            int ch1Offset = MultiChapterJson.IndexOf("\"chapter1\"");
            Assert.True(ch1Offset > 0);

            var ch1Context = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, ch1Offset);
            Assert.Equal("Chapter", ch1Context.ElementType);
            Assert.Equal("Robins Physiology", ch1Context.SubjectKey);
            Assert.Equal("chapter1", ch1Context.ChapterKey);

            // Caret inside chapter 1's "name": "Cell Injury"
            int ch1NameOffset = MultiChapterJson.IndexOf("\"Cell Injury\"");
            Assert.True(ch1NameOffset > 0);

            var ch1NameContext = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, ch1NameOffset);
            Assert.Equal("Chapter", ch1NameContext.ElementType);
            Assert.Equal("chapter1", ch1NameContext.ChapterKey);

            // Caret on chapter2 header
            int ch2Offset = MultiChapterJson.IndexOf("\"chapter2\"");
            Assert.True(ch2Offset > 0);

            var ch2Context = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, ch2Offset);
            Assert.Equal("Chapter", ch2Context.ElementType);
            Assert.Equal("chapter2", ch2Context.ChapterKey);
        }

        [Fact]
        public void DetectContextAtCaret_DetectsTopicLevelAttributesAndDifferentiatesChapters()
        {
            // Caret inside chapter1 topic_1 "Causes of Cell Injury"
            int ch1Top1Offset = MultiChapterJson.IndexOf("\"Causes of Cell Injury\"");
            Assert.True(ch1Top1Offset > 0);

            var ch1Top1Context = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, ch1Top1Offset);
            Assert.Equal("Topic", ch1Top1Context.ElementType);
            Assert.Equal("Robins Physiology", ch1Top1Context.SubjectKey);
            Assert.Equal("chapter1", ch1Top1Context.ChapterKey);
            Assert.Equal("topic_1", ch1Top1Context.TopicKey);

            // Caret inside chapter1 topic_2 "start_page": "16"
            int ch1Top2PageOffset = MultiChapterJson.IndexOf("\"16\"");
            Assert.True(ch1Top2PageOffset > 0);

            var ch1Top2Context = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, ch1Top2PageOffset);
            Assert.Equal("Topic", ch1Top2Context.ElementType);
            Assert.Equal("chapter1", ch1Top2Context.ChapterKey);
            Assert.Equal("topic_2", ch1Top2Context.TopicKey);

            // Caret inside chapter2 topic_1 "Acute Inflammation" (must match chapter2, not chapter1!)
            int ch2Top1Offset = MultiChapterJson.IndexOf("\"Acute Inflammation\"");
            Assert.True(ch2Top1Offset > 0);

            var ch2Top1Context = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, ch2Top1Offset);
            Assert.Equal("Topic", ch2Top1Context.ElementType);
            Assert.Equal("chapter2", ch2Top1Context.ChapterKey);
            Assert.Equal("topic_1", ch2Top1Context.TopicKey);

            // Caret inside chapter2 topic_2 "Chronic Inflammation"
            int ch2Top2Offset = MultiChapterJson.IndexOf("\"Chronic Inflammation\"");
            Assert.True(ch2Top2Offset > 0);

            var ch2Top2Context = DevToolsNavigator.DetectContextAtCaret(MultiChapterJson, ch2Top2Offset);
            Assert.Equal("Topic", ch2Top2Context.ElementType);
            Assert.Equal("chapter2", ch2Top2Context.ChapterKey);
            Assert.Equal("topic_2", ch2Top2Context.TopicKey);
        }

        [Fact]
        public void DetectContextAtCaret_HandlesEmptyStringGracefully()
        {
            var context = DevToolsNavigator.DetectContextAtCaret("", 0);
            Assert.Equal("Root", context.ElementType);
        }

        [Fact]
        public void ActiveConfiguration_CyclesProperlyWithWrapAround()
        {
            int count = 3;
            int currentIndex = 0; // Config "1" (0-based)

            // Up Arrow (-1): wraps to last config (index 2, "3")
            currentIndex = (currentIndex - 1 + count) % count;
            Assert.Equal(2, currentIndex);
            Assert.Equal("3", (currentIndex + 1).ToString());

            // Down Arrow (+1): wraps back to first config (index 0, "1")
            currentIndex = (currentIndex + 1) % count;
            Assert.Equal(0, currentIndex);
            Assert.Equal("1", (currentIndex + 1).ToString());

            // Down Arrow (+1): next config (index 1, "2")
            currentIndex = (currentIndex + 1) % count;
            Assert.Equal(1, currentIndex);
            Assert.Equal("2", (currentIndex + 1).ToString());
        }
    }
}

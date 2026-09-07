using System;
using System.Collections.Generic;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0051FeaturesTests
    {
        private const string MultiChapterJson = @"{
  ""Robins Physiology"": {
    ""short_name"": ""Robins Physiology"",
    ""full_name"": ""Robins Pathologic Basis of Disease"",
    ""edition"": ""7th"",
    ""Chapters"": {
      ""chapter1"": {
        ""name"": ""Cell Injury"",
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
        public void ScanElementPositions_DiscoversAllElementsInSequentialDocumentOrder()
        {
            var positions = DevToolsNavigator.ScanElementPositions(MultiChapterJson);

            // 1 Subject + 2 Chapters + 4 Topics = 7 elements
            Assert.Equal(7, positions.Count);

            // Element 0: Subject
            Assert.Equal("Subject", positions[0].Type);
            Assert.Equal("Robins Physiology", positions[0].SubjectKey);

            // Element 1: chapter1
            Assert.Equal("Chapter", positions[1].Type);
            Assert.Equal("chapter1", positions[1].ChapterKey);

            // Element 2: topic_1 of chapter1
            Assert.Equal("Topic", positions[2].Type);
            Assert.Equal("chapter1", positions[2].ChapterKey);
            Assert.Equal("topic_1", positions[2].TopicKey);

            // Element 3: topic_2 of chapter1
            Assert.Equal("Topic", positions[3].Type);
            Assert.Equal("chapter1", positions[3].ChapterKey);
            Assert.Equal("topic_2", positions[3].TopicKey);

            // Element 4: chapter2
            Assert.Equal("Chapter", positions[4].Type);
            Assert.Equal("chapter2", positions[4].ChapterKey);

            // Element 5: topic_1 of chapter2 (duplicate key 'topic_1' in chapter2)
            Assert.Equal("Topic", positions[5].Type);
            Assert.Equal("chapter2", positions[5].ChapterKey);
            Assert.Equal("topic_1", positions[5].TopicKey);

            // Element 6: topic_2 of chapter2
            Assert.Equal("Topic", positions[6].Type);
            Assert.Equal("chapter2", positions[6].ChapterKey);
            Assert.Equal("topic_2", positions[6].TopicKey);

            // Verify offsets are monotonically increasing
            for (int i = 1; i < positions.Count; i++)
            {
                Assert.True(positions[i].CharOffset > positions[i - 1].CharOffset,
                    $"Offset at index {i} ({positions[i].CharOffset}) should be greater than index {i - 1} ({positions[i - 1].CharOffset})");
            }
        }

        [Fact]
        public void FindCurrentElementIndex_DifferentiatesDuplicateTopicKeysAcrossChapters()
        {
            var positions = DevToolsNavigator.ScanElementPositions(MultiChapterJson);

            // When in chapter 1, topic_1
            int ch1Topic1 = DevToolsNavigator.FindCurrentElementIndex(positions, "Robins Physiology", "chapter1", "topic_1");
            Assert.Equal(2, ch1Topic1);

            // When in chapter 2, topic_1 (must NOT match chapter 1's topic_1)
            int ch2Topic1 = DevToolsNavigator.FindCurrentElementIndex(positions, "Robins Physiology", "chapter2", "topic_1");
            Assert.Equal(5, ch2Topic1);

            // When in chapter 2, topic_2
            int ch2Topic2 = DevToolsNavigator.FindCurrentElementIndex(positions, "Robins Physiology", "chapter2", "topic_2");
            Assert.Equal(6, ch2Topic2);
        }

        [Fact]
        public void Navigation_NextAndPrevious_StepsSmoothlyAcrossChaptersWithoutLooping()
        {
            var positions = DevToolsNavigator.ScanElementPositions(MultiChapterJson);
            int count = positions.Count; // 7

            // From chapter1 topic_2 (index 3), next element is chapter2 (index 4)
            int nextAfterCh1Top2 = DevToolsNavigator.GetNextIndex(3, count);
            Assert.Equal(4, nextAfterCh1Top2);
            Assert.Equal("Chapter", positions[nextAfterCh1Top2].Type);
            Assert.Equal("chapter2", positions[nextAfterCh1Top2].ChapterKey);

            // Next after chapter2 is chapter2 topic_1 (index 5)
            int nextAfterCh2 = DevToolsNavigator.GetNextIndex(4, count);
            Assert.Equal(5, nextAfterCh2);
            Assert.Equal("topic_1", positions[nextAfterCh2].TopicKey);
            Assert.Equal("chapter2", positions[nextAfterCh2].ChapterKey);

            // Previous from chapter2 topic_1 (index 5) steps back to chapter2 (index 4)
            int prevFromCh2Top1 = DevToolsNavigator.GetPreviousIndex(5, count);
            Assert.Equal(4, prevFromCh2Top1);

            // Previous from chapter2 (index 4) steps back to chapter1 topic_2 (index 3)
            int prevFromCh2 = DevToolsNavigator.GetPreviousIndex(4, count);
            Assert.Equal(3, prevFromCh2);
            Assert.Equal("topic_2", positions[prevFromCh2].TopicKey);
            Assert.Equal("chapter1", positions[prevFromCh2].ChapterKey);
        }

        [Fact]
        public void Navigation_WrapAroundBoundaries()
        {
            const int count = 7;

            // Next from last index (6) wraps around to 0
            Assert.Equal(0, DevToolsNavigator.GetNextIndex(6, count));

            // Previous from index 0 wraps around to last index (6)
            Assert.Equal(6, DevToolsNavigator.GetPreviousIndex(0, count));
        }

        [Fact]
        public void GeneratorParse_UpdatesTreeWhenUserDirectlyEditsJsonText()
        {
            var generator = new PresetTagGenerator();

            // Original tree
            var originalTree = generator.Parse(MultiChapterJson);
            Assert.Equal("Causes of Cell Injury", originalTree["Robins Physiology"].Chapters["chapter1"].Topics["topic_1"].name);

            // User edits the topic name directly in the JSON preview
            string editedJson = MultiChapterJson.Replace("Causes of Cell Injury", "Mechanisms and Etiology of Cell Injury");
            editedJson = editedJson.Replace("\"start_page\": \"1\"", "\"start_page\": \"5\"");

            var updatedTree = generator.Parse(editedJson);
            var ch1 = updatedTree["Robins Physiology"].Chapters["chapter1"];
            var topic1 = ch1.Topics["topic_1"];

            Assert.Equal("Mechanisms and Etiology of Cell Injury", topic1.name);
            Assert.Equal("5", topic1.start_page);
            Assert.Equal("15", topic1.end_page);
        }

        [Fact]
        public void GeneratorParse_HandlesEditedChapterNameAndSubjectDetails()
        {
            var generator = new PresetTagGenerator();

            string editedJson = MultiChapterJson
                .Replace("Cell Injury", "Fundamental Cell Injury and Adaptation")
                .Replace("\"edition\": \"7th\"", "\"edition\": \"8th Edition\"");

            var updatedTree = generator.Parse(editedJson);
            var subject = updatedTree["Robins Physiology"];

            Assert.Equal("8th Edition", subject.edition);
            Assert.Equal("Fundamental Cell Injury and Adaptation", subject.Chapters["chapter1"].name);
        }
    }
}

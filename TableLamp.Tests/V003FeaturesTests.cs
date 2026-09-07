using System;
using System.Collections.Generic;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V003FeaturesTests
    {
        [Fact]
        public void Tag_HoldsBookNameAndPageRange()
        {
            var tag = new Tag(
                id: 101,
                subjectName: "Robins Physiology",
                chapterNumber: 1,
                chapterName: "Cell Injury",
                topicName: "Causes of Cell Injury",
                bookName: "Robins Pathologic Basis of Disease",
                pageRange: "23-27"
            );

            Assert.Equal("Robins Pathologic Basis of Disease", tag.book_name);
            Assert.Equal("23-27", tag.page_range);
            Assert.Equal("Robins Pathologic Basis of Disease", tag.BookName);
            Assert.Equal("23-27", tag.PageRange);
            Assert.Contains("Robins Pathologic Basis of Disease", tag.ToString());
            Assert.Contains("23-27", tag.ToString());
        }

        [Fact]
        public void PresetTagGenerator_ParsesCanonicalJsonTree()
        {
            var generator = new PresetTagGenerator();
            string json = generator.CreateStarterPresetJson();

            var tree = generator.Parse(json);

            Assert.NotNull(tree);
            Assert.True(tree.ContainsKey("Subject1"));

            var subject1 = tree["Subject1"];
            Assert.Equal("Robins Physiology", subject1.short_name);
            Assert.Equal("Robins Pathologic Basis of Disease", subject1.full_name);
            Assert.True(subject1.Chapters.ContainsKey("chapter1") || subject1.Chapters.ContainsKey("Chapter 1"));

            var ch1 = subject1.Chapters.GetValueOrDefault("chapter1") ?? subject1.Chapters["Chapter 1"];
            Assert.Equal("Cell Injury", ch1.name);
            Assert.Equal(1, ch1.ChapterNumber);
            Assert.True(ch1.Topics.ContainsKey("topic_1"));

            var topic1 = ch1.Topics["topic_1"];
            Assert.Equal("Causes of Cell Injury", topic1.name);
            Assert.Equal("23", topic1.start_page);
            Assert.Equal("27", topic1.end_page);
            Assert.Equal(23, topic1.StartPageNumber);
            Assert.Equal(27, topic1.EndPageNumber);
        }

        [Fact]
        public void PresetTagGenerator_SearchesPageRange_SingleMatch()
        {
            var generator = new PresetTagGenerator();
            var tree = generator.Parse(generator.CreateStarterPresetJson());

            // Pages 23 to 25 fall squarely inside topic_1 (23-27)
            var results = generator.SearchByPageRange(tree, 23, 25);

            Assert.NotEmpty(results);
            var first = results.First();
            Assert.Equal("Robins Physiology", first.SubjectShortName);
            Assert.Equal("Cell Injury", first.ChapterName);
            Assert.Equal("Causes of Cell Injury", first.TopicName);
            Assert.Equal(23, first.StartPage);
            Assert.Equal(27, first.EndPage);
        }

        [Fact]
        public void PresetTagGenerator_SearchesPageRange_MultiMatch()
        {
            var generator = new PresetTagGenerator();
            var tree = generator.Parse(generator.CreateStarterPresetJson());

            // Pages 26 to 27 overlap topic_1 (23-27) AND topic_2 (26-32)
            var results = generator.SearchByPageRange(tree, 26, 27);

            Assert.True(results.Count >= 2);
            Assert.Contains(results, r => r.TopicName == "Causes of Cell Injury");
            Assert.Contains(results, r => r.TopicName == "Mechanisms of Cell Injury");
        }

        [Fact]
        public void PresetTagGenerator_SearchesPageRange_NotFound()
        {
            var generator = new PresetTagGenerator();
            var tree = generator.Parse(generator.CreateStarterPresetJson());

            // Pages 9999 to 10000 do not exist in preset
            var results = generator.SearchByPageRange(tree, 9999, 10000);

            Assert.Empty(results);
        }

        [Fact]
        public void PresetTagDatabase_DynamicLoadsAndSearches()
        {
            var db = new PresetTagDatabase();
            db.InitializeDatabase();

            var search = db.Search(105, 110);
            Assert.NotEmpty(search);
            Assert.Equal("Guyton Medical Physiology", search[0].SubjectShortName);
            Assert.Equal("Physiology of Cardiac Muscle", search[0].TopicName);
        }
    }
}

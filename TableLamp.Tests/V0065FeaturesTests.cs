using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0065FeaturesTests
    {
        [Fact]
        public void PresetTagGenerator_CreateBlankSubjectPresetJson_CreatesOnlyDummySubject()
        {
            var generator = new PresetTagGenerator();
            string blankJson = generator.CreateBlankSubjectPresetJson();

            Assert.NotNull(blankJson);
            Assert.Contains("Subject1", blankJson);
            Assert.Contains("New Subject", blankJson);

            var parsed = generator.Parse(blankJson);
            Assert.Single(parsed);
            Assert.True(parsed.ContainsKey("Subject1"));

            var subject = parsed["Subject1"];
            Assert.Equal("New Subject", subject.short_name);
            Assert.Equal("New Subject Full Name", subject.full_name);
            Assert.Equal("1st", subject.edition);

            // Verified: Dummy subject only, no chapters or topics pre-filled
            Assert.True(subject.Chapters == null || subject.Chapters.Count == 0);
        }

        [Fact]
        public void CustomPresetTagDatabase_IsSingleton_AndHasSeparateStoragePath()
        {
            var customDb1 = CustomPresetTagDatabase.Instance;
            var customDb2 = CustomPresetTagDatabase.Instance;

            Assert.Same(customDb1, customDb2);
            Assert.Contains("custom_preset_tags.json", customDb1.StoragePath, StringComparison.OrdinalIgnoreCase);

            var curatedDb = PresetTagDatabase.Instance;
            Assert.NotEqual(curatedDb.StoragePath, customDb1.StoragePath);
            Assert.Contains("preset_tags.json", curatedDb.StoragePath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CustomPresetTagDatabase_SaveAndSearch_OperatesIndependentlyFromCurated()
        {
            var customDb = new CustomPresetTagDatabase();

            var customTree = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubjectCustom1"] = new PresetSubject
                {
                    Key = "SubjectCustom1",
                    short_name = "Biochemistry Custom",
                    full_name = "Harper's Illustrated Biochemistry",
                    Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["chapter1"] = new PresetChapter
                        {
                            Key = "chapter1",
                            name = "Biomolecules & Water",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["topic_1"] = new PresetTopic
                                {
                                    Key = "topic_1",
                                    name = "Water & pH",
                                    start_page = "15",
                                    end_page = "22"
                                }
                            }
                        }
                    }
                }
            };

            customDb.SaveTree(customTree);

            // Search in Custom Database
            var results = customDb.Search(16, 20);
            Assert.Single(results);
            Assert.Equal("Water & pH", results[0].TopicName);
            Assert.Equal("Biochemistry Custom", results[0].SubjectShortName);

            // Verify Curated Database doesn't have this custom subject
            var curatedResults = PresetTagDatabase.Instance.Search(16, 20);
            Assert.DoesNotContain(curatedResults, r => r.SubjectShortName == "Biochemistry Custom");
        }

        [Fact]
        public void CustomPresetTagDatabase_ImportJsonFiles_AndDirectory_MergesSuccessfully()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "TableLamp_CustomDB_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string json1 = @"
{
  ""SubjectCustomA"": {
    ""short_name"": ""Immunology Custom"",
    ""Chapters"": {
      ""chapter1"": {
        ""name"": ""Innate Immunity"",
        ""Topics"": {
          ""topic_1"": { ""name"": ""Complement System"", ""start_page"": ""50"", ""end_page"": ""60"" }
        }
      }
    }
  }
}";
                string file1 = Path.Combine(tempDir, "immunology.json");
                File.WriteAllText(file1, json1);

                var customDb = new CustomPresetTagDatabase();
                var (found, success, failed) = customDb.ImportDirectory(tempDir);

                Assert.Equal(1, found);
                Assert.Equal(1, success);
                Assert.Equal(0, failed);

                var searchResults = customDb.Search(52, 58);
                Assert.Contains(searchResults, r => r.TopicName == "Complement System");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void CustomPresetTagDatabase_FormatDatabase_ClearsAllData()
        {
            var customDb = new CustomPresetTagDatabase();

            var sampleTree = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubjectTest"] = new PresetSubject
                {
                    short_name = "Test Subject",
                    Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ch1"] = new PresetChapter
                        {
                            name = "Test Chapter",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["top1"] = new PresetTopic { name = "Topic 1", start_page = "1", end_page = "5" }
                            }
                        }
                    }
                }
            };

            customDb.SaveTree(sampleTree);
            Assert.True(customDb.SubjectCount > 0);

            customDb.FormatDatabase();
            Assert.Equal(0, customDb.SubjectCount);
            Assert.Equal(0, customDb.ChapterCount);
            Assert.Equal(0, customDb.TopicCount);
        }

        [Fact]
        public void CustomTagService_MaintainsDistinction_BetweenCuratedAndCustomSubjects()
        {
            PresetTagDatabase.Instance.ReloadFromJson(new PresetTagGenerator().CreateStarterPresetJson());
            var service = CustomTagService.Instance;

            // Curated subjects should be recognized
            Assert.True(service.IsCuratedSubject("Robins Physiology"));
            Assert.True(service.IsCuratedSubject("Guyton Medical Physiology"));

            // An unknown custom subject should NOT be curated
            Assert.False(service.IsCuratedSubject("Arbitrary Custom Subject XYZ"));
        }

        [Fact]
        public void CustomPresetTags_CanBeAssignedToBasicSessionBundle_WithIsCuratedFalse()
        {
            var customTag = new Tag
            {
                id = 1,
                subject_name = "My Custom Pathology",
                chapter_number = 3,
                chapter_name = "Custom Chapter 3",
                topic_name = "Custom Topic A",
                book_name = "Custom Pathology Reference",
                page_range = "40-55",
                session_type = 0
            };

            var session = new BasicSessionBundle(
                initialQuestions: null,
                isCurated: false,
                nextReviewDate: DateTime.UtcNow.AddDays(3),
                sessionName: "Custom Pathology Study Session",
                tag: customTag,
                tags: new[] { customTag }
            );

            Assert.False(session.IsCurated);
            Assert.Equal("Custom Pathology Study Session", session.SessionName);
            Assert.NotNull(session.Tag);
            Assert.Equal("My Custom Pathology", session.Tag!.subject_name);
            Assert.Equal(3, session.Tag.chapter_number);
            Assert.Equal("Custom Chapter 3", session.Tag.chapter_name);
            Assert.Equal("Custom Topic A", session.Tag.topic_name);
            Assert.Equal("40-55", session.Tag.page_range);
        }
    }
}

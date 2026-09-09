using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V005FeaturesTests : IDisposable
    {
        private readonly string _tempTestDir;

        public V005FeaturesTests()
        {
            _tempTestDir = Path.Combine(Path.GetTempPath(), "TableLamp_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempTestDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempTestDir))
                {
                    Directory.Delete(_tempTestDir, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        [Fact]
        public void PresetChapter_ChapterNumber_ParsesIntegerFromChapterXFormat()
        {
            var chapter1 = new PresetChapter { Key = "chapter1", name = "Introduction" };
            var chapter12 = new PresetChapter { Key = "chapter12", name = "Endocrine System" };
            var chapterLegacy = new PresetChapter { Key = "Chapter 5", name = "Metabolism" };

            Assert.Equal(1, chapter1.ChapterNumber);
            Assert.Equal(12, chapter12.ChapterNumber);
            Assert.Equal(5, chapterLegacy.ChapterNumber);
        }

        [Fact]
        public void StarterPreset_UsesChapterXFormat()
        {
            var generator = new PresetTagGenerator();
            string json = generator.CreateStarterPresetJson();
            var tree = generator.Parse(json);

            var robins = tree.Values.First(s => s.short_name == "Robins Physiology");
            Assert.True(robins.Chapters.ContainsKey("chapter1"));
            Assert.True(robins.Chapters.ContainsKey("chapter2"));
            Assert.False(robins.Chapters.ContainsKey("Chapter 1"));
        }

        [Fact]
        public void PresetTagDatabase_MergeTree_UnionsSubjectsAndChapters()
        {
            var db = PresetTagDatabase.Instance;
            db.ResetToStarterPresets();
            int initialCount = db.SubjectCount;

            var newTree = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomSubject"] = new PresetSubject
                {
                    Key = "CustomSubject",
                    short_name = "Neuroanatomy",
                    full_name = "Clinical Neuroanatomy",
                    edition = "8th",
                    Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["chapter1"] = new PresetChapter
                        {
                            Key = "chapter1",
                            name = "Cranial Nerves",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["topic_1"] = new PresetTopic
                                {
                                    name = "Optic Nerve Pathways",
                                    start_page = "10",
                                    end_page = "25"
                                }
                            }
                        }
                    }
                }
            };

            db.MergeTree(newTree);

            Assert.True(db.SubjectCount >= initialCount + 1);
            Assert.NotNull(db.GetTree().GetValueOrDefault("CustomSubject"));
            Assert.Equal("Neuroanatomy", db.GetTree()["CustomSubject"].short_name);
        }

        [Fact]
        public void PresetTagDatabase_ImportJsonFiles_SuccessfullyImportsMultipleFiles()
        {
            var generator = new PresetTagGenerator();

            string file1 = Path.Combine(_tempTestDir, "test1.json");
            string file2 = Path.Combine(_tempTestDir, "test2.json");

            var tree1 = new Dictionary<string, PresetSubject>
            {
                ["SubjA"] = new PresetSubject { Key = "SubjA", short_name = "Subject A" }
            };
            var tree2 = new Dictionary<string, PresetSubject>
            {
                ["SubjB"] = new PresetSubject { Key = "SubjB", short_name = "Subject B" }
            };

            File.WriteAllText(file1, generator.GenerateJson(tree1));
            File.WriteAllText(file2, generator.GenerateJson(tree2));

            var db = PresetTagDatabase.Instance;
            var (success, failed) = db.ImportJsonFiles(new[] { file1, file2 });

            Assert.Equal(2, success);
            Assert.Equal(0, failed);
            Assert.NotNull(db.GetTree().GetValueOrDefault("SubjA"));
            Assert.NotNull(db.GetTree().GetValueOrDefault("SubjB"));
        }

        [Fact]
        public void PresetTagDatabase_ImportDirectory_RecursivelyFindsAndImportsJsonFiles()
        {
            var generator = new PresetTagGenerator();

            string subDir = Path.Combine(_tempTestDir, "nested", "subfolder");
            Directory.CreateDirectory(subDir);

            string fileRoot = Path.Combine(_tempTestDir, "root.json");
            string fileNested = Path.Combine(subDir, "nested.json");

            var treeRoot = new Dictionary<string, PresetSubject>
            {
                ["FolderRootSubj"] = new PresetSubject { Key = "FolderRootSubj", short_name = "Root Subject" }
            };
            var treeNested = new Dictionary<string, PresetSubject>
            {
                ["FolderNestedSubj"] = new PresetSubject { Key = "FolderNestedSubj", short_name = "Nested Subject" }
            };

            File.WriteAllText(fileRoot, generator.GenerateJson(treeRoot));
            File.WriteAllText(fileNested, generator.GenerateJson(treeNested));

            var db = PresetTagDatabase.Instance;
            var (total, success, failed) = db.ImportDirectory(_tempTestDir);

            Assert.Equal(2, total);
            Assert.Equal(2, success);
            Assert.Equal(0, failed);
            Assert.NotNull(db.GetTree().GetValueOrDefault("FolderRootSubj"));
            Assert.NotNull(db.GetTree().GetValueOrDefault("FolderNestedSubj"));
        }

        [Fact]
        public void PresetTagDatabase_FormatDatabase_ClearsAllPresets()
        {
            var db = PresetTagDatabase.Instance;
            try
            {
                db.FormatDatabase();

                Assert.Equal(0, db.SubjectCount);
                Assert.Equal(0, db.ChapterCount);
                Assert.Equal(0, db.TopicCount);
            }
            finally
            {
                // In v0.0.7.3 starter presets are removed; ResetToStarterPresets resets to empty
                db.ResetToStarterPresets();
                Assert.True(db.IsEmpty);
            }
        }
    }
}

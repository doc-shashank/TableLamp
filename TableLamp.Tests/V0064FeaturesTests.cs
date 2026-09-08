using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0064FeaturesTests
    {
        [Fact]
        public void AppSettingsService_WorkspaceAndJsonPath_GetSetCorrectly()
        {
            var service = new AppSettingsService();

            string testWorkspace = @"C:\TestWorkspace\Folder1";
            string testJson = @"C:\TestWorkspace\Folder1\chapter1.json";

            service.LastWorkspacePath = testWorkspace;
            service.LastOpenedJsonPath = testJson;

            Assert.Equal(testWorkspace, service.LastWorkspacePath);
            Assert.Equal(testJson, service.LastOpenedJsonPath);

            // Nullable tests
            service.LastWorkspacePath = null;
            service.LastOpenedJsonPath = null;

            Assert.Null(service.LastWorkspacePath);
            Assert.Null(service.LastOpenedJsonPath);
        }

        [Fact]
        public void DevToolsWorkspaceDatabase_ScanAndDuplicateChapterDetection_WorksAcrossFiles()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "TableLamp_V0064_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var generator = new PresetTagGenerator();

                // File 1: Subject1 with chapter 1 "Cell Injury"
                var tree1 = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Subject1"] = new PresetSubject
                    {
                        Key = "Subject1",
                        short_name = "Pathology",
                        Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["chapter1"] = new PresetChapter
                            {
                                Key = "chapter1",
                                name = "Cell Injury",
                                Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["topic_1"] = new PresetTopic { Key = "topic_1", name = "Necrosis" }
                                }
                            }
                        }
                    }
                };

                // File 2: Subject1 with chapter 2 "Inflammation"
                var tree2 = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Subject1"] = new PresetSubject
                    {
                        Key = "Subject1",
                        short_name = "Pathology",
                        Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["chapter2"] = new PresetChapter
                            {
                                Key = "chapter2",
                                name = "Inflammation",
                                Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["topic_1"] = new PresetTopic { Key = "topic_1", name = "Acute Inflammation" }
                                }
                            }
                        }
                    }
                };

                string file1Path = Path.Combine(tempDir, "file1.json");
                string file2Path = Path.Combine(tempDir, "file2.json");

                File.WriteAllText(file1Path, generator.GenerateJson(tree1));
                File.WriteAllText(file2Path, generator.GenerateJson(tree2));

                var db = new DevToolsWorkspaceDatabase();
                db.ScanWorkspace(tempDir);

                // Check duplicate chapter by name in file 2 matching file 1
                bool isDupName = db.IsDuplicateChapter(file2Path, "Subject1", "Cell Injury", 3, out string? dupFile);
                Assert.True(isDupName);
                Assert.Equal(file1Path, dupFile);

                // Check duplicate chapter by number in file 2 matching file 1 (chapter 1)
                bool isDupNum = db.IsDuplicateChapter(file2Path, "Subject1", "Unique Chapter", 1, out dupFile);
                Assert.True(isDupNum);
                Assert.Equal(file1Path, dupFile);

                // Same file ignore: checking file 1 against itself should not flag duplicate
                bool selfCheck = db.IsDuplicateChapter(file1Path, "Subject1", "Cell Injury", 1, out dupFile);
                Assert.False(selfCheck);

                // Unique chapter should pass
                bool isUnique = db.IsDuplicateChapter(file2Path, "Subject1", "Tissue Repair", 3, out dupFile);
                Assert.False(isUnique);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void DevToolsWorkspaceDatabase_DuplicateTopicDetection_WorksAcrossFiles()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "TableLamp_V0064_TopicTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var generator = new PresetTagGenerator();

                // File 1: Subject1, chapter1, Topic "Apoptosis"
                var tree1 = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Subject1"] = new PresetSubject
                    {
                        Key = "Subject1",
                        Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["chapter1"] = new PresetChapter
                            {
                                Key = "chapter1",
                                name = "Cell Injury",
                                Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["topic_1"] = new PresetTopic { Key = "topic_1", name = "Apoptosis" }
                                }
                            }
                        }
                    }
                };

                // File 2: Subject1, chapter1 (same subject & chapter in another file)
                var tree2 = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Subject1"] = new PresetSubject
                    {
                        Key = "Subject1",
                        Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["chapter1"] = new PresetChapter
                            {
                                Key = "chapter1",
                                name = "Cell Injury",
                                Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["topic_2"] = new PresetTopic { Key = "topic_2", name = "Gangrene" }
                                }
                            }
                        }
                    }
                };

                string file1Path = Path.Combine(tempDir, "partA.json");
                string file2Path = Path.Combine(tempDir, "partB.json");

                File.WriteAllText(file1Path, generator.GenerateJson(tree1));
                File.WriteAllText(file2Path, generator.GenerateJson(tree2));

                var db = new DevToolsWorkspaceDatabase();
                db.ScanWorkspace(tempDir);

                // Try adding "Apoptosis" to partB under chapter1 -> duplicate detected from partA
                bool isDup = db.IsDuplicateTopic(file2Path, "Subject1", "chapter1", "Apoptosis", out string? dupFile);
                Assert.True(isDup);
                Assert.Equal(file1Path, dupFile);

                // Self check on file 1
                bool selfDup = db.IsDuplicateTopic(file1Path, "Subject1", "chapter1", "Apoptosis", out dupFile);
                Assert.False(selfDup);

                // Add unique topic -> allowed
                bool uniqueTopic = db.IsDuplicateTopic(file2Path, "Subject1", "chapter1", "Autophagy", out dupFile);
                Assert.False(uniqueTopic);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void DevToolsWorkspaceDatabase_UpdateBufferAndRemoveFile_UpdatesIndexCorrectly()
        {
            var db = new DevToolsWorkspaceDatabase();
            string mockPath = @"C:\MockWorkspace\data.json";

            var tree = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
            {
                ["Subject1"] = new PresetSubject
                {
                    Key = "Subject1",
                    short_name = "Pharmacology",
                    Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["chapter1"] = new PresetChapter
                        {
                            Key = "chapter1",
                            name = "Pharmacokinetics",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["topic_1"] = new PresetTopic { Key = "topic_1", name = "Absorption" }
                            }
                        }
                    }
                }
            };

            db.UpdateFileBuffer(mockPath, tree);
            Assert.Single(db.Files);

            bool dup = db.IsDuplicateTopic(@"C:\MockWorkspace\other.json", "Pharmacology", "Pharmacokinetics", "Absorption", out string? conflict);
            Assert.True(dup);
            Assert.Equal(mockPath, conflict);

            // Now remove file from DB
            db.RemoveFile(mockPath);
            Assert.Empty(db.Files);

            bool dupAfterRemoval = db.IsDuplicateTopic(@"C:\MockWorkspace\other.json", "Pharmacology", "Pharmacokinetics", "Absorption", out _);
            Assert.False(dupAfterRemoval);
        }

        [Fact]
        public void PresetTagGenerator_StarterPreset_GeneratesParsableStructure()
        {
            var generator = new PresetTagGenerator();
            string starter = generator.CreateStarterPresetJson();

            Assert.False(string.IsNullOrWhiteSpace(starter));
            var parsed = generator.Parse(starter);

            Assert.NotNull(parsed);
            Assert.NotEmpty(parsed);

            var firstSubj = parsed.Values.First();
            Assert.NotNull(firstSubj.Chapters);
            Assert.NotEmpty(firstSubj.Chapters);

            var firstCh = firstSubj.Chapters.Values.First();
            Assert.NotNull(firstCh.Topics);
            Assert.NotEmpty(firstCh.Topics);
        }
    }
}

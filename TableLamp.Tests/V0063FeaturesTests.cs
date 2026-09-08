using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0063FeaturesTests
    {
        [Fact]
        public void AppSettingsService_DefaultDurationAndClamping_WorksCorrectly()
        {
            var service = new AppSettingsService();
            Assert.True(service.NotificationDurationSeconds >= 1 && service.NotificationDurationSeconds <= 60);

            bool changedFired = false;
            service.SettingsChanged += () => changedFired = true;

            // Set to valid value
            service.NotificationDurationSeconds = 8;
            Assert.Equal(8, service.NotificationDurationSeconds);
            Assert.True(changedFired);

            // Test clamping under min
            service.NotificationDurationSeconds = -5;
            Assert.Equal(1, service.NotificationDurationSeconds);

            // Test clamping over max
            service.NotificationDurationSeconds = 120;
            Assert.Equal(60, service.NotificationDurationSeconds);

            // Reset back to reasonable default
            service.NotificationDurationSeconds = 5;
            Assert.Equal(5, service.NotificationDurationSeconds);
        }

        [Fact]
        public void ViewCanvasPage_SessionDetailsAndTypeIcons_AccurateValues()
        {
            var selfTag = new Tag(1, "Pathology", 2, "Cell Injury", "Necrosis", "Robins", "10-25", Tag.SelfSession);
            var selfBundle = new BasicSessionBundle(null, true, null, "Pathology Review", selfTag, new[] { selfTag });

            Assert.Equal("Pathology Review", selfBundle.DisplayTitle);
            Assert.Equal("Self Session", selfBundle.SessionTypeName);
            Assert.True(selfBundle.IsSelfSession);
            Assert.False(selfBundle.IsClassSession);
            Assert.Equal("\uE77B", selfBundle.SessionTypeGlyph);
            Assert.Equal("Ch.2: Cell Injury", selfBundle.ChaptersSummary);
            Assert.Equal("Necrosis", selfBundle.TopicsSummary);

            var classTag = new Tag(2, "Neurology", 1, "Cortex", "Synapses", "Kandel", "50-60", Tag.ClassSession);
            var classBundle = new BasicSessionBundle(null, false, null, "Neuro Lecture", classTag, new[] { classTag });

            Assert.Equal("Neuro Lecture", classBundle.DisplayTitle);
            Assert.Equal("Class Session", classBundle.SessionTypeName);
            Assert.True(classBundle.IsClassSession);
            Assert.False(classBundle.IsSelfSession);
            Assert.Equal("\uE7BE", classBundle.SessionTypeGlyph);
        }

        [Fact]
        public void CustomPresets_TreeDeletion_RemovesNodeAndDescendants()
        {
            var gen = new PresetTagGenerator();
            string json = @"{
                ""Subject1"": {
                    ""short_name"": ""Physics"",
                    ""full_name"": ""Halliday Physics"",
                    ""edition"": ""10th"",
                    ""chapters"": {
                        ""chapter_1"": {
                            ""chapter_number"": 1,
                            ""name"": ""Mechanics"",
                            ""topics"": {
                                ""topic_1"": {
                                    ""name"": ""Kinematics"",
                                    ""start_page"": ""1"",
                                    ""end_page"": ""20""
                                }
                            }
                        }
                    }
                }
            }";

            var tree = gen.Parse(json);
            Assert.True(tree.ContainsKey("Subject1"));
            Assert.True(tree["Subject1"].Chapters!.ContainsKey("chapter_1"));
            Assert.True(tree["Subject1"].Chapters!["chapter_1"].Topics!.ContainsKey("topic_1"));

            // 1. Delete Topic
            var ch = tree["Subject1"].Chapters!["chapter_1"];
            ch.Topics!.Remove("topic_1");
            Assert.False(ch.Topics.ContainsKey("topic_1"));

            // 2. Delete Chapter
            tree["Subject1"].Chapters!.Remove("chapter_1");
            Assert.False(tree["Subject1"].Chapters.ContainsKey("chapter_1"));

            // 3. Delete Subject
            tree.Remove("Subject1");
            Assert.False(tree.ContainsKey("Subject1"));
        }

        [Fact]
        public void SessionService_DeleteSession_RemovesFromCollection()
        {
            var service = new SessionService();
            string testId = "v0063-test-session-" + Guid.NewGuid();

            var tag = new Tag(1, "TestSubject", 1, "TestChapter", "TestTopic", null, null, Tag.SelfSession);
            var bundle = new BasicSessionBundle(null, false, null, "Temporary Session", tag, new[] { tag })
            {
                Id = testId
            };

            service.SaveSession(bundle);
            Assert.NotNull(service.GetSessionById(testId));

            bool deleted = service.DeleteSession(testId);
            Assert.True(deleted);
            Assert.Null(service.GetSessionById(testId));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V004FeaturesTests
    {
        [Fact]
        public void Bundle_GeneratesTimeSeededId_ByDefault()
        {
            var bundle = new Bundle();

            Assert.NotNull(bundle.Id);
            Assert.StartsWith("BND-", bundle.Id);

            string[] parts = bundle.Id.Split('-');
            Assert.Equal(3, parts.Length);
            Assert.True(long.TryParse(parts[1], out long ticks));
            Assert.True(ticks > 0);
            Assert.True(int.TryParse(parts[2], out int randVal));
            Assert.True(randVal >= 100000 && randVal <= 999999);
        }

        [Fact]
        public void BasicSessionBundle_InheritsTimeSeededId_AndCanHaveSessionName()
        {
            var session = new BasicSessionBundle(null, false, null, "Biochemistry Exam Review", null);

            Assert.NotNull(session.Id);
            Assert.StartsWith("BND-", session.Id);
            Assert.Equal("Biochemistry Exam Review", session.SessionName);
            Assert.Equal("Biochemistry Exam Review", session.DisplayTitle);
        }

        [Fact]
        public void CustomTagService_DetectsCuratedPresetSubject()
        {
            var service = CustomTagService.Instance;

            Assert.True(service.IsCuratedSubject("Robins Physiology"));
            Assert.True(service.IsCuratedSubject("robins physiology"));
            Assert.True(service.IsCuratedSubject("Robins Pathologic Basis of Disease"));
            Assert.False(service.IsCuratedSubject("Astronomy & Cosmology"));
        }

        [Fact]
        public void CustomTagService_BlocksCustomTag_WithCuratedSubjectName()
        {
            var service = CustomTagService.Instance;

            var conflictingTag = new Tag
            {
                subject_name = "Robins Physiology",
                chapter_number = 99,
                chapter_name = "Custom Non-Existent Chapter",
                topic_name = "Custom Topic"
            };

            bool success = service.ValidateAndSaveCustomTag(conflictingTag, out string? error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("curated preset subject", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CustomTagService_AllowsAndStoresValidCustomTag()
        {
            var service = CustomTagService.Instance;

            var validTag = new Tag
            {
                subject_name = "Organic Chemistry",
                chapter_number = 3,
                chapter_name = "Alkenes and Alkynes",
                topic_name = "Electrophilic Addition"
            };

            bool success = service.ValidateAndSaveCustomTag(validTag, out string? error);

            Assert.True(success);
            Assert.Null(error);

            var customTags = service.GetCustomTags();
            Assert.Contains(customTags, t => t.subject_name == "Organic Chemistry" && t.chapter_name == "Alkenes and Alkynes");
        }

        [Fact]
        public void SimpleQuestion_UpdateText_PreservesImageElement()
        {
            var question = new SimpleQuestion("Initial Question Text", "C:\\img.png");

            Assert.True(question.HasText);
            Assert.True(question.HasImage);

            question.UpdateText("Updated Question Text");

            Assert.True(question.HasText);
            Assert.True(question.HasImage);
            Assert.Equal("Updated Question Text", question.TextElement?.simple_text);
            Assert.Equal("C:\\img.png", question.ImageElement?.simple_image?.ToString());
        }

        [Fact]
        public void SimpleQuestion_UpdateImage_PreservesTextElement()
        {
            var question = new SimpleQuestion("Preserved Text", "C:\\old_img.png");

            question.UpdateImage("C:\\new_img.png");

            Assert.True(question.HasText);
            Assert.True(question.HasImage);
            Assert.Equal("Preserved Text", question.TextElement?.simple_text);
            Assert.Equal("C:\\new_img.png", question.ImageElement?.simple_image?.ToString());
        }

        [Fact]
        public void PresetTagGenerator_AddChapterAndTopic_RegeneratesValidJson()
        {
            var generator = new PresetTagGenerator();
            string starterJson = generator.CreateStarterPresetJson();
            var tree = generator.Parse(starterJson);

            var firstSubject = tree.Values.First();
            string newChKey = "Chapter 99";
            var newCh = new PresetChapter
            {
                Key = newChKey,
                name = "Advanced Dynamics",
                Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                {
                    ["topic_1"] = new PresetTopic
                    {
                        Key = "topic_1",
                        name = "Rotational Equilibrium",
                        start_page = "100",
                        end_page = "120"
                    }
                }
            };
            firstSubject.Chapters[newChKey] = newCh;

            string regeneratedJson = generator.GenerateJson(tree);
            Assert.Contains("Chapter 99", regeneratedJson);
            Assert.Contains("Advanced Dynamics", regeneratedJson);
            Assert.Contains("Rotational Equilibrium", regeneratedJson);

            // Verify reparsing
            var reparsed = generator.Parse(regeneratedJson);
            Assert.True(reparsed[firstSubject.Key].Chapters.ContainsKey("Chapter 99"));
            Assert.Equal("Advanced Dynamics", reparsed[firstSubject.Key].Chapters["Chapter 99"].name);
        }
    }
}

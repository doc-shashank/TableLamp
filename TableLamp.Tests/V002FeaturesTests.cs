using System;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V002FeaturesTests
    {
        [Fact]
        public void SimpleQuestion_SupportsIndependentElements_TextOnly()
        {
            var q = new SimpleQuestion("Only text question", null, null);

            Assert.True(q.HasText);
            Assert.False(q.HasImage);
            Assert.Equal("Only text question", q.TextElement?.simple_text);
            Assert.Null(q.ImageElement);
            Assert.Single(q.question_element_array);
        }

        [Fact]
        public void SimpleQuestion_SupportsIndependentElements_ImageOnly()
        {
            var q = new SimpleQuestion(null, "C:/images/diagram.png", null);

            Assert.False(q.HasText);
            Assert.True(q.HasImage);
            Assert.Null(q.TextElement);
            Assert.Equal("C:/images/diagram.png", q.ImageElement?.simple_image);
            Assert.Single(q.question_element_array);
            Assert.Equal("[Image Question]", q.SummaryText);
        }

        [Fact]
        public void SimpleQuestion_SupportsIndependentElements_BothTextAndImage()
        {
            var q = new SimpleQuestion("Diagram analysis prompt", "C:/images/chart.png", null);

            Assert.True(q.HasText);
            Assert.True(q.HasImage);
            Assert.Equal("Diagram analysis prompt", q.TextElement?.simple_text);
            Assert.Equal("C:/images/chart.png", q.ImageElement?.simple_image);
            Assert.Equal(2, q.question_element_array.Length);
        }

        [Fact]
        public void BasicSessionBundle_DisplayTitle_GeneratesCorrectly()
        {
            var tag = new Tag(10, "Biology", 4, "Genetics", "Mendelian Inheritance");
            var sessionWithTag = new BasicSessionBundle(null, false, null, null, tag);

            Assert.Equal("Biology Ch.4 Genetics", sessionWithTag.DisplayTitle);

            var sessionWithName = new BasicSessionBundle(null, false, null, "Midterm Exam Prep", tag);
            Assert.Equal("Midterm Exam Prep", sessionWithName.DisplayTitle);
        }

        [Fact]
        public void SessionService_StoresAndQueriesSessionsCorrectly()
        {
            var service = new SessionService();
            service.SeedSampleData();

            var all = service.GetAllSessions();
            Assert.True(all.Count >= 3);

            // Test Query by Date (today)
            var todaySessions = service.GetSessionsByDate(DateTime.UtcNow);
            Assert.NotEmpty(todaySessions);

            // Test Saving a New Session
            var newTag = new Tag(99, "Astronomy", 1, "Solar System", "Planets");
            var q1 = new SimpleQuestion("Name the terrestrial planets in our solar system.", null, newTag);
            var customSession = new BasicSessionBundle(new[] { q1 }, false, DateTime.UtcNow.AddDays(5), "Planetary Science", newTag)
            {
                Id = "custom-test-session"
            };

            service.SaveSession(customSession);

            var retrieved = service.GetSessionById("custom-test-session");
            Assert.NotNull(retrieved);
            Assert.Equal("Planetary Science", retrieved.DisplayTitle);
            Assert.Equal(1, retrieved.QuestionCount);

            // Test Deleting Session
            bool deleted = service.DeleteSession("custom-test-session");
            Assert.True(deleted);
            Assert.Null(service.GetSessionById("custom-test-session"));
        }
    }
}

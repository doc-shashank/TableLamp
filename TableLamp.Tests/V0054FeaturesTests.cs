using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0054FeaturesTests
    {
        [Fact]
        public void BasicSessionBundle_MultiTag_CalculatesChaptersAndTopicsSummaries()
        {
            var tag1 = new Tag(1, "Robins Pathology", 1, "Cell Injury", "Causes of Cell Injury", "Robins", "1-15", Tag.SelfSession);
            var tag2 = new Tag(2, "Robins Pathology", 1, "Cell Injury", "Reversible Injury", "Robins", "16-30", Tag.SelfSession);
            var tag3 = new Tag(3, "Robins Pathology", 2, "Inflammation", "Acute Inflammation", "Robins", "31-45", Tag.SelfSession);

            var session = new BasicSessionBundle(
                initialQuestions: null,
                isCurated: false,
                nextReviewDate: null,
                sessionName: "Pathology Review",
                tag: tag1,
                tags: new List<Tag> { tag1, tag2, tag3 }
            );

            Assert.Equal(3, session.Tags.Count);
            // Chapters summary should contain unique chapter representations
            Assert.Contains("Ch.1: Cell Injury", session.ChaptersSummary);
            Assert.Contains("Ch.2: Inflammation", session.ChaptersSummary);

            // Topics summary should contain all distinct topics
            Assert.Contains("Causes of Cell Injury", session.TopicsSummary);
            Assert.Contains("Reversible Injury", session.TopicsSummary);
            Assert.Contains("Acute Inflammation", session.TopicsSummary);
        }

        [Fact]
        public void BasicSessionBundle_TagAndTags_DualSynchronization()
        {
            var tagA = new Tag(10, "Physics", 1, "Kinematics", "Motion", null, null, Tag.SelfSession);
            var tagB = new Tag(20, "Physics", 2, "Forces", "Newton Laws", null, null, Tag.SelfSession);

            var session = new BasicSessionBundle
            {
                SessionName = "Dual Sync Test"
            };

            // Setting Tag should populate Tags[0]
            session.Tag = tagA;
            Assert.Single(session.Tags);
            Assert.Equal(tagA, session.Tags[0]);
            Assert.Equal(tagA, session.Tag);

            // Setting Tags should update Tag getter
            session.Tags = new List<Tag> { tagB, tagA };
            // Tag should return the first item from Tags
            Assert.Equal(tagB, session.Tag);

            // Re-assigning Tag updates index 0
            var tagC = new Tag(30, "Physics", 3, "Energy", "Work-Energy Theorem", null, null, Tag.SelfSession);
            session.Tag = tagC;
            Assert.Equal(tagC, session.Tags[0]);
            Assert.Equal(tagC, session.Tag);
        }

        [Fact]
        public void CustomTagService_IsCuratedSubject_CorrectlyIdentifiesCuratedVsCustom()
        {
            var customTagService = CustomTagService.Instance;

            // Null, empty, or whitespace should return false
            Assert.False(customTagService.IsCuratedSubject(null));
            Assert.False(customTagService.IsCuratedSubject(""));
            Assert.False(customTagService.IsCuratedSubject("   "));

            // Check against actual preset subjects from PresetTagDatabase
            var tree = PresetTagDatabase.Instance.GetTree();
            if (tree != null && tree.Count > 0)
            {
                var firstCurated = tree.Keys.First();
                Assert.True(customTagService.IsCuratedSubject(firstCurated));
                Assert.True(customTagService.IsCuratedSubject(firstCurated.ToUpperInvariant()));
                Assert.True(customTagService.IsCuratedSubject(firstCurated.ToLowerInvariant()));
                Assert.True(customTagService.IsCuratedSubject($"  {firstCurated}  "));
            }

            // Arbitrary custom subject name must not be identified as curated
            Assert.False(customTagService.IsCuratedSubject("TotallyNonExistentCuratedSubject_XYZ_9999"));
        }

        [Fact]
        public void SessionService_MultiTagSession_SerializesAndRestoresAllTags()
        {
            var service = SessionService.Instance;

            var tag1 = new Tag(101, "Biochemistry", 5, "Enzymes", "Kinetics", "Harper", "50-60", Tag.ClassSession);
            var tag2 = new Tag(102, "Biochemistry", 5, "Enzymes", "Inhibition", "Harper", "61-70", Tag.ClassSession);

            var session = new BasicSessionBundle(
                initialQuestions: null,
                isCurated: false,
                nextReviewDate: DateTime.UtcNow.AddDays(3),
                sessionName: "V0054 MultiTag Test Session",
                tag: tag1,
                tags: new List<Tag> { tag1, tag2 }
            );

            service.SaveSession(session);

            var retrieved = service.GetSessionById(session.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("V0054 MultiTag Test Session", retrieved!.SessionName);
            Assert.NotNull(retrieved.Tags);
            Assert.True(retrieved.Tags.Count >= 2);
            Assert.Contains(retrieved.Tags, t => t.topic_name == "Kinetics");
            Assert.Contains(retrieved.Tags, t => t.topic_name == "Inhibition");
            Assert.Equal(Tag.ClassSession, retrieved.SessionType);

            // Clean up test session
            service.DeleteSession(session.Id);
        }
    }
}

using System;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0052FeaturesTests
    {
        [Fact]
        public void Tag_SessionType_DefaultsToSelfSession_Zero()
        {
            var tag = new Tag();
            Assert.Equal(0, tag.session_type);
            Assert.Equal(Tag.SelfSession, tag.SessionType);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 0)]  // Non-1 clamped to 0
        [InlineData(-1, 0)] // Non-1 clamped to 0
        public void Tag_SessionType_ConstrainedToZeroAndOne(int input, int expected)
        {
            var tag = new Tag { session_type = input };
            Assert.Equal(expected, tag.session_type);
            Assert.Equal(expected, tag.SessionType);
        }

        [Fact]
        public void Tag_Constructor_AcceptsSessionType()
        {
            var tagSelf = new Tag(1, "Physics", 1, "Kinematics", "Motion", null, null, Tag.SelfSession);
            Assert.Equal(Tag.SelfSession, tagSelf.session_type);
            Assert.Contains("[Self]", tagSelf.ToString());

            var tagClass = new Tag(2, "Biology", 3, "Genetics", "Mendel", null, null, Tag.ClassSession);
            Assert.Equal(Tag.ClassSession, tagClass.session_type);
            Assert.Contains("[Class]", tagClass.ToString());
        }

        [Fact]
        public void BasicSessionBundle_SessionTypeProperties_ReflectTagSessionType()
        {
            var tagSelf = new Tag(1, "Physics", 1, "Kinematics", "Motion", null, null, Tag.SelfSession);
            var sessionSelf = new BasicSessionBundle(null, false, null, "Self Study", tagSelf);

            Assert.Equal(0, sessionSelf.SessionType);
            Assert.Equal("Self Session", sessionSelf.SessionTypeName);
            Assert.True(sessionSelf.IsSelfSession);
            Assert.False(sessionSelf.IsClassSession);

            var tagClass = new Tag(2, "Biology", 2, "Cells", "Mitosis", null, null, Tag.ClassSession);
            var sessionClass = new BasicSessionBundle(null, false, null, "Lecture Notes", tagClass);

            Assert.Equal(1, sessionClass.SessionType);
            Assert.Equal("Class Session", sessionClass.SessionTypeName);
            Assert.False(sessionClass.IsSelfSession);
            Assert.True(sessionClass.IsClassSession);
        }

        [Fact]
        public void BasicSessionBundle_WithoutTag_DefaultsToSelfSession()
        {
            var session = new BasicSessionBundle(null, false, null, "Untagged Session", null);
            Assert.Equal(0, session.SessionType);
            Assert.Equal("Self Session", session.SessionTypeName);
            Assert.True(session.IsSelfSession);
            Assert.False(session.IsClassSession);
        }

        [Fact]
        public void CalendarSeparation_FiltersSelfAndClassSessionsCorrectly()
        {
            var date = DateTime.Today;
            var tagSelf1 = new Tag(1, "Math", 1, "Algebra", null, null, null, Tag.SelfSession);
            var tagSelf2 = new Tag(2, "Physics", 2, "Optics", null, null, null, Tag.SelfSession);
            var tagClass1 = new Tag(3, "Chemistry", 3, "Acids", null, null, null, Tag.ClassSession);

            var s1 = new BasicSessionBundle(null, false, null, "Self 1", tagSelf1) { creation_date = date.AddHours(9) };
            var s2 = new BasicSessionBundle(null, false, null, "Self 2", tagSelf2) { creation_date = date.AddHours(11) };
            var s3 = new BasicSessionBundle(null, false, null, "Class 1", tagClass1) { creation_date = date.AddHours(14) };

            var all = new[] { s1, s2, s3 };

            var selfSessions = all.Where(s => s.SessionType == Tag.SelfSession).ToList();
            var classSessions = all.Where(s => s.SessionType == Tag.ClassSession).ToList();

            Assert.Equal(2, selfSessions.Count);
            Assert.Contains(s1, selfSessions);
            Assert.Contains(s2, selfSessions);

            Assert.Single(classSessions);
            Assert.Contains(s3, classSessions);
        }

        [Fact]
        public void Dashboard_RecentSessions_RemainsUnaffectedBySessionType()
        {
            var service = SessionService.Instance;
            service.SeedSampleData();

            var recent = service.GetRecentSessions(10);
            Assert.NotEmpty(recent);

            // Recent sessions contains both self and class sessions without separation/filtering
            bool hasSelf = recent.Any(s => s.SessionType == Tag.SelfSession);
            bool hasClass = recent.Any(s => s.SessionType == Tag.ClassSession);

            Assert.True(hasSelf, "Recent sessions should include Self sessions.");
            Assert.True(hasClass, "Recent sessions should include Class sessions.");
        }
    }
}

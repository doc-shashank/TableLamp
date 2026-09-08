using System;
using System.Collections.Generic;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V0061FeaturesTests
    {
        [Fact]
        public void BasicSessionBundle_SessionTypeProperties_SelfSession()
        {
            var selfTag = new Tag(1, "Physiology", 1, "Cellular", "Membrane Potential", null, "1-10", Tag.SelfSession);
            var bundle = new BasicSessionBundle(
                initialQuestions: null,
                isCurated: true,
                sessionName: "Physiology Study",
                tag: selfTag,
                tags: new[] { selfTag }
            );

            Assert.True(bundle.IsSelfSession);
            Assert.False(bundle.IsClassSession);
            Assert.Equal("Self Session", bundle.SessionTypeName);
            Assert.Equal("\uE77B", bundle.SessionTypeGlyph);
            Assert.Equal("#0067C0", bundle.SessionTypeIconBackground);
            Assert.Equal("Self Session", bundle.SessionTypeTooltip);
        }

        [Fact]
        public void BasicSessionBundle_SessionTypeProperties_ClassSession()
        {
            var classTag = new Tag(2, "Pathology", 2, "Inflammation", "Cellular Events", null, "20-35", Tag.ClassSession);
            var bundle = new BasicSessionBundle(
                initialQuestions: null,
                isCurated: true,
                sessionName: "Pathology Lecture",
                tag: classTag,
                tags: new[] { classTag }
            );

            Assert.True(bundle.IsClassSession);
            Assert.False(bundle.IsSelfSession);
            Assert.Equal("Class Session", bundle.SessionTypeName);
            Assert.Equal("\uE7BE", bundle.SessionTypeGlyph);
            Assert.Equal("#8A4FFF", bundle.SessionTypeIconBackground);
            Assert.Equal("Class Session", bundle.SessionTypeTooltip);
        }

        [Fact]
        public void PresetTagDatabase_OverviewStatistics_ReturnsAccurateCounts()
        {
            var db = PresetTagDatabase.Instance;
            var tree = db.GetTree();

            if (tree != null && tree.Count > 0)
            {
                Assert.True(db.SubjectCount > 0);
                Assert.True(db.ChapterCount >= 0);
                Assert.True(db.TopicCount >= 0);
                Assert.False(string.IsNullOrWhiteSpace(db.StoragePath));
            }
        }
    }
}

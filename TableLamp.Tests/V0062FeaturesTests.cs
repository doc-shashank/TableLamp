using System;
using System.Collections.Generic;
using System.Linq;
using TableLamp.Models;
using Xunit;

namespace TableLamp.Tests
{
    public class V0062FeaturesTests
    {
        [Fact]
        public void SessionFiltering_ByType_FiltersAccurately()
        {
            var self1 = new Tag(1, "Biology", 1, "Cells", "Mitosis", null, null, Tag.SelfSession);
            var self2 = new Tag(2, "Chemistry", 1, "Atoms", "Orbitals", null, null, Tag.SelfSession);
            var class1 = new Tag(3, "Physics", 1, "Kinematics", "Velocity", null, null, Tag.ClassSession);
            var class2 = new Tag(4, "Math", 1, "Calculus", "Derivatives", null, null, Tag.ClassSession);

            var bSelf1 = new BasicSessionBundle(null, true, null, "Bio Self", self1, new[] { self1 });
            var bSelf2 = new BasicSessionBundle(null, false, null, "Chem Self", self2, new[] { self2 });
            var bClass1 = new BasicSessionBundle(null, true, null, "Physics Class", class1, new[] { class1 });
            var bClass2 = new BasicSessionBundle(null, false, null, "Math Class", class2, new[] { class2 });

            var sessions = new List<BasicSessionBundle> { bSelf1, bSelf2, bClass1, bClass2 };

            // 1. All filter
            var allResult = sessions.ToList();
            Assert.Equal(4, allResult.Count);

            // 2. Self filter
            var selfResult = sessions.Where(s => s.IsSelfSession).ToList();
            Assert.Equal(2, selfResult.Count);
            Assert.All(selfResult, s => Assert.True(s.IsSelfSession));

            // 3. Class filter
            var classResult = sessions.Where(s => s.IsClassSession).ToList();
            Assert.Equal(2, classResult.Count);
            Assert.All(classResult, s => Assert.True(s.IsClassSession));
        }

        [Fact]
        public void SessionSorting_NewestAndOldest_SortsAccurately()
        {
            var tag = new Tag(1, "History", 1, "WWI", "Treaty", null, null, Tag.SelfSession);

            var olderSession = new BasicSessionBundle(null, true, null, "Older Session", tag, new[] { tag })
            {
                creation_date = DateTime.UtcNow.AddHours(-10)
            };
            var midSession = new BasicSessionBundle(null, true, null, "Mid Session", tag, new[] { tag })
            {
                creation_date = DateTime.UtcNow.AddHours(-5)
            };
            var newerSession = new BasicSessionBundle(null, true, null, "Newer Session", tag, new[] { tag })
            {
                creation_date = DateTime.UtcNow.AddHours(-1)
            };

            var sessions = new List<BasicSessionBundle> { midSession, olderSession, newerSession };

            // Sort Newest First (Descending)
            var newestSorted = sessions.OrderByDescending(s => s.creation_date).ToList();
            Assert.Equal("Newer Session", newestSorted[0].SessionName);
            Assert.Equal("Mid Session", newestSorted[1].SessionName);
            Assert.Equal("Older Session", newestSorted[2].SessionName);

            // Sort Oldest First (Ascending)
            var oldestSorted = sessions.OrderBy(s => s.creation_date).ToList();
            Assert.Equal("Older Session", oldestSorted[0].SessionName);
            Assert.Equal("Mid Session", oldestSorted[1].SessionName);
            Assert.Equal("Newer Session", oldestSorted[2].SessionName);
        }

        [Fact]
        public void CombinedSearchAndTypeFilter_AppliesBothCriteria()
        {
            var tag1 = new Tag(1, "Robins Pathology", 1, "Cell Injury", "Hypoxia", null, null, Tag.SelfSession);
            var tag2 = new Tag(2, "Robins Pathology", 1, "Cell Injury", "Ischemia", null, null, Tag.ClassSession);
            var tag3 = new Tag(3, "Physiology", 1, "Cardio", "Cardiac Output", null, null, Tag.ClassSession);

            var s1 = new BasicSessionBundle(null, true, null, "Pathology Review 1", tag1, new[] { tag1 });
            var s2 = new BasicSessionBundle(null, true, null, "Pathology Lecture 1", tag2, new[] { tag2 });
            var s3 = new BasicSessionBundle(null, true, null, "Physio Lecture 1", tag3, new[] { tag3 });

            var sessions = new List<BasicSessionBundle> { s1, s2, s3 };

            // Query "Pathology" + Class filter
            string query = "Pathology";
            var result = sessions
                .Where(s => s.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            (s.Tag?.subject_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .Where(s => s.IsClassSession)
                .ToList();

            Assert.Single(result);
            Assert.Equal("Pathology Lecture 1", result[0].SessionName);
            Assert.True(result[0].IsClassSession);
        }
    }
}

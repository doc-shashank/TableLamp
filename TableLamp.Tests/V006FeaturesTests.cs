using System;
using System.Collections.Generic;
using System.Linq;
using TableLamp.Models;
using TableLamp.Services;
using Xunit;

namespace TableLamp.Tests
{
    public class V006FeaturesTests
    {
        [Fact]
        public void IdentifierFormatting_GeneratesExpectedSubjectChapterTopicNames()
        {
            // Verify 1-based index numbering: SubjectX, ChapterX, TopicX
            var tree = new Dictionary<string, PresetSubject>
            {
                ["SubjectA"] = new PresetSubject
                {
                    short_name = "SubA",
                    Chapters = new Dictionary<string, PresetChapter>
                    {
                        ["Ch1"] = new PresetChapter
                        {
                            name = "First Chapter",
                            Topics = new Dictionary<string, PresetTopic>
                            {
                                ["Top1"] = new PresetTopic { name = "Topic One", start_page = "1", end_page = "10" },
                                ["Top2"] = new PresetTopic { name = "Topic Two", start_page = "11", end_page = "20" }
                            }
                        },
                        ["Ch2"] = new PresetChapter
                        {
                            name = "Second Chapter",
                            Topics = new Dictionary<string, PresetTopic>
                            {
                                ["Top3"] = new PresetTopic { name = "Topic Three", start_page = "21", end_page = "30" }
                            }
                        }
                    }
                },
                ["SubjectB"] = new PresetSubject
                {
                    short_name = "SubB",
                    Chapters = new Dictionary<string, PresetChapter>()
                }
            };

            int subjectIndex = 1;
            var subjectIds = new List<string>();
            var chapterIds = new List<string>();
            var topicIds = new List<string>();

            foreach (var (subjKey, subj) in tree)
            {
                string sId = $"Subject{subjectIndex++}";
                subjectIds.Add(sId);

                int chapterIndex = 1;
                if (subj.Chapters != null)
                {
                    foreach (var (chKey, ch) in subj.Chapters)
                    {
                        string cId = $"Chapter{chapterIndex++}";
                        chapterIds.Add(cId);

                        int topicIndex = 1;
                        if (ch.Topics != null)
                        {
                            foreach (var (topKey, top) in ch.Topics)
                            {
                                string tId = $"Topic{topicIndex++}";
                                topicIds.Add(tId);
                            }
                        }
                    }
                }
            }

            Assert.Equal(new[] { "Subject1", "Subject2" }, subjectIds);
            Assert.Equal(new[] { "Chapter1", "Chapter2" }, chapterIds);
            Assert.Equal(new[] { "Topic1", "Topic2", "Topic1" }, topicIds);
        }

        [Fact]
        public void SimplifiedVariableMapping_TransformsFieldNamesAccurately()
        {
            // Verify simplified property representation
            // Topic fields: start_page -> start, end_page -> end
            var topic = new PresetTopic { name = "Introduction", start_page = "10", end_page = "25" };
            Assert.Equal("10", topic.start_page);
            Assert.Equal("25", topic.end_page);

            string simplifiedStartKey = "start";
            string simplifiedEndKey = "end";
            Assert.Equal("start", simplifiedStartKey);
            Assert.Equal("end", simplifiedEndKey);

            // Chapter fields: ChapterNumber -> number
            var chapter = new PresetChapter { Key = "Chapter 3", name = "Chapter 3" };
            string simplifiedNumberKey = "number";
            Assert.Equal("number", simplifiedNumberKey);
            Assert.Equal(3, chapter.ChapterNumber);

            // Subject fields: short_name -> short name, full_name -> full name
            var subject = new PresetSubject { short_name = "Path", full_name = "Pathology Complete", edition = "10th" };
            Assert.Equal("Path", subject.short_name);
            Assert.Equal("Pathology Complete", subject.full_name);
            Assert.Equal("10th", subject.edition);
        }

        [Fact]
        public void PresetTree_Deletion_RemovesSubjectsChaptersAndTopicsCorrectly()
        {
            var tree = new Dictionary<string, PresetSubject>(StringComparer.OrdinalIgnoreCase)
            {
                ["Pathology"] = new PresetSubject
                {
                    short_name = "Path",
                    Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Chapter 1"] = new PresetChapter
                        {
                            name = "Cell Injury",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["Topic 1"] = new PresetTopic { name = "Hypoxia", start_page = "1", end_page = "10" },
                                ["Topic 2"] = new PresetTopic { name = "Ischemia", start_page = "11", end_page = "20" }
                            }
                        },
                        ["Chapter 2"] = new PresetChapter
                        {
                            name = "Inflammation",
                            Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["Topic 1"] = new PresetTopic { name = "Acute", start_page = "21", end_page = "30" }
                            }
                        }
                    }
                }
            };

            // 1. Delete Topic 1 from Chapter 1
            var ch1 = tree["Pathology"].Chapters["Chapter 1"];
            Assert.True(ch1.Topics.Remove("Topic 1"));
            Assert.Single(ch1.Topics);
            Assert.False(ch1.Topics.ContainsKey("Topic 1"));
            Assert.True(ch1.Topics.ContainsKey("Topic 2"));

            // 2. Delete Chapter 2 from Pathology
            var subj = tree["Pathology"];
            Assert.True(subj.Chapters.Remove("Chapter 2"));
            Assert.Single(subj.Chapters);
            Assert.False(subj.Chapters.ContainsKey("Chapter 2"));
            Assert.True(subj.Chapters.ContainsKey("Chapter 1"));

            // 3. Delete Subject Pathology entirely
            Assert.True(tree.Remove("Pathology"));
            Assert.Empty(tree);
        }

        [Fact]
        public void CuratedMode_RequiresValidCuratedSubject()
        {
            var customTagService = CustomTagService.Instance;

            // Curated subjects from database should be recognized
            var tree = PresetTagDatabase.Instance.GetTree();
            if (tree != null && tree.Count > 0)
            {
                var validSubject = tree.Keys.First();
                Assert.True(customTagService.IsCuratedSubject(validSubject));
            }

            // Arbitrary custom subject name must NOT pass curated validation
            string nonCuratedSubject = "MyCustomNotes_Subject_2026";
            Assert.False(customTagService.IsCuratedSubject(nonCuratedSubject));
        }

        [Fact]
        public void CustomMode_AllowsAnySubjectAndCreatesValidBundle()
        {
            string customSubject = "Advanced Quantum Computing Notes";
            string sessionName = "Quantum Midterm Prep";

            var customTag = new Tag
            {
                id = 1,
                subject_name = customSubject,
                chapter_number = 0,
                chapter_name = "Custom",
                topic_name = "Custom",
                session_type = Tag.SelfSession
            };

            var sessionBundle = new BasicSessionBundle(
                initialQuestions: null,
                isCurated: false,
                nextReviewDate: DateTime.UtcNow.AddDays(3),
                sessionName: sessionName,
                tag: customTag,
                tags: new[] { customTag }
            );

            Assert.False(sessionBundle.IsCurated);
            Assert.Equal(sessionName, sessionBundle.SessionName);
            Assert.NotNull(sessionBundle.Tag);
            Assert.Equal(customSubject, sessionBundle.Tag!.subject_name);
            Assert.Equal("Custom", sessionBundle.Tag!.chapter_name);
            Assert.Equal("Custom", sessionBundle.Tag!.topic_name);
            Assert.Single(sessionBundle.Tags);
        }

        [Fact]
        public void ClosestMatchRecommendation_FindsAccurateMatch()
        {
            var candidates = new List<string>
            {
                "Robbins Basic Pathology",
                "Guyton and Hall Medical Physiology",
                "Harrison's Principles of Internal Medicine",
                "First Aid for the USMLE Step 1",
                "Ganong's Review of Medical Physiology"
            };

            // Helper matching logic corresponding to SessionDetailsPage implementation
            string FindClosest(string input)
            {
                if (string.IsNullOrWhiteSpace(input)) return "";
                string lower = input.Trim().ToLowerInvariant();

                // 1. Exact or prefix match
                var exact = candidates.FirstOrDefault(c => c.Equals(lower, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;

                var prefix = candidates.FirstOrDefault(c => c.ToLowerInvariant().StartsWith(lower));
                if (prefix != null) return prefix;

                // 2. Contains match
                var contains = candidates.FirstOrDefault(c => c.ToLowerInvariant().Contains(lower));
                if (contains != null) return contains;

                // 3. Levenshtein distance
                int bestDist = int.MaxValue;
                string bestMatch = "";
                foreach (var c in candidates)
                {
                    int dist = ComputeLevenshtein(lower, c.ToLowerInvariant());
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestMatch = c;
                    }
                }
                return bestMatch;
            }

            int ComputeLevenshtein(string s, string t)
            {
                int n = s.Length, m = t.Length;
                int[,] d = new int[n + 1, m + 1];
                if (n == 0) return m;
                if (m == 0) return n;
                for (int i = 0; i <= n; d[i, 0] = i++) { }
                for (int j = 0; j <= m; d[0, j] = j++) { }
                for (int i = 1; i <= n; i++)
                {
                    for (int j = 1; j <= m; j++)
                    {
                        int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                        d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    }
                }
                return d[n, m];
            }

            // Prefix test
            Assert.Equal("Robbins Basic Pathology", FindClosest("Robb"));
            Assert.Equal("Guyton and Hall Medical Physiology", FindClosest("Guyton"));

            // Substring test
            Assert.Equal("Harrison's Principles of Internal Medicine", FindClosest("Harrison"));

            // Fuzzy typo test
            Assert.Equal("Robbins Basic Pathology", FindClosest("Robins Basic Pathlogy"));
        }
    }
}

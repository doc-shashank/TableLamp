using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TableLamp.Models;

namespace TableLamp.Services
{
    /// <summary>
    /// Service managing BasicSessionBundle storage, persistence, and queries.
    /// </summary>
    public class SessionService
    {
        private static SessionService? _instance;
        public static SessionService Instance => _instance ??= new SessionService();

        private readonly List<BasicSessionBundle> _sessions = new();
        private readonly string _storagePath;

        public event Action? SessionsChanged;

        public SessionService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "TableLamp");
            Directory.CreateDirectory(dir);
            _storagePath = Path.Combine(dir, "sessions.json");

            LoadSessions();
        }

        public IReadOnlyList<BasicSessionBundle> GetAllSessions()
        {
            return _sessions.OrderByDescending(s => s.creation_date).ToList().AsReadOnly();
        }

        public IReadOnlyList<BasicSessionBundle> GetRecentSessions(int count = 5)
        {
            return _sessions.OrderByDescending(s => s.creation_date).Take(count).ToList().AsReadOnly();
        }

        public IReadOnlyList<BasicSessionBundle> GetSessionsByDate(DateTime date)
        {
            return _sessions.Where(s => s.creation_date.Date == date.Date)
                            .OrderByDescending(s => s.creation_date)
                            .ToList()
                            .AsReadOnly();
        }

        public BasicSessionBundle? GetSessionById(string id)
        {
            return _sessions.FirstOrDefault(s => s.Id == id);
        }

        public void SaveSession(BasicSessionBundle session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            int existingIndex = _sessions.FindIndex(s => s.Id == session.Id);
            if (existingIndex >= 0)
            {
                _sessions[existingIndex] = session;
            }
            else
            {
                _sessions.Insert(0, session);
            }

            Persist();
            SessionsChanged?.Invoke();
        }

        public bool DeleteSession(string id)
        {
            int removed = _sessions.RemoveAll(s => s.Id == id);
            if (removed > 0)
            {
                Persist();
                SessionsChanged?.Invoke();
                return true;
            }
            return false;
        }

        private void LoadSessions()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string json = File.ReadAllText(_storagePath);
                    var list = DeserializeSessions(json);
                    if (list != null && list.Count > 0)
                    {
                        _sessions.Clear();
                        _sessions.AddRange(list);
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to seed data on error
            }

            SeedSampleData();
        }

        private void Persist()
        {
            try
            {
                var dtos = _sessions.Select(ToDto).ToList();
                string json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storagePath, json);
            }
            catch (Exception)
            {
                // In-memory cache continues to work seamlessly if disk write fails
            }
        }

        public void SeedSampleData()
        {
            _sessions.Clear();

            // Sample 1: Physics (Today)
            var tag1 = new Tag(1, "Physics", 1, "Kinematics", "Projectile Motion");
            var q1 = new SimpleQuestion("State the independence of horizontal and vertical velocities in ideal projectile motion.", null, tag1);
            var q2 = new SimpleQuestion("What is the formula for maximum height attained by a projectile launched at angle θ?", null, tag1);
            var session1 = new BasicSessionBundle(new[] { q1, q2 }, false, DateTime.UtcNow.AddDays(2), "Kinematics Warmup", tag1)
            {
                Id = "sample-1",
                creation_date = DateTime.UtcNow
            };

            // Sample 2: Chemistry (Yesterday)
            var tag2 = new Tag(2, "Chemistry", 3, "Chemical Bonding", "Ionic vs Covalent");
            var q3 = new SimpleQuestion("Explain why ionic compounds have higher melting points compared to molecular covalent compounds.", null, tag2);
            var session2 = new BasicSessionBundle(new[] { q3 }, false, DateTime.UtcNow.AddDays(4), "Chemical Bonding Review", tag2)
            {
                Id = "sample-2",
                creation_date = DateTime.UtcNow.AddDays(-1)
            };

            // Sample 3: Mathematics (2 Days ago)
            var tag3 = new Tag(3, "Mathematics", 5, "Differential Calculus", "Chain Rule");
            var q4 = new SimpleQuestion("Compute the derivative of f(x) = sin(x^2 + 3x).", null, tag3);
            var q5 = new SimpleQuestion("State the conditions under which Rolle's Theorem is applicable.", null, tag3);
            var session3 = new BasicSessionBundle(new[] { q4, q5 }, false, DateTime.UtcNow.AddDays(7), "Calculus Mastery", tag3)
            {
                Id = "sample-3",
                creation_date = DateTime.UtcNow.AddDays(-2)
            };

            _sessions.Add(session1);
            _sessions.Add(session2);
            _sessions.Add(session3);

            Persist();
        }

        #region DTO Serialization Helpers

        private class SessionDto
        {
            public string Id { get; set; } = "";
            public string? SessionName { get; set; }
            public DateTime CreationDate { get; set; }
            public DateTime? NextReviewDate { get; set; }
            public bool IsCurated { get; set; }
            public TagDto? Tag { get; set; }
            public List<QuestionDto> Questions { get; set; } = new();
        }

        private class TagDto
        {
            public int Id { get; set; }
            public string? SubjectName { get; set; }
            public int ChapterNumber { get; set; }
            public string? ChapterName { get; set; }
            public string? TopicName { get; set; }
        }

        private class QuestionDto
        {
            public string Id { get; set; } = "";
            public string? Text { get; set; }
            public string? ImagePath { get; set; }
            public bool IsEditable { get; set; }
        }

        private SessionDto ToDto(BasicSessionBundle s)
        {
            return new SessionDto
            {
                Id = s.Id,
                SessionName = s.SessionName,
                CreationDate = s.creation_date,
                NextReviewDate = s.next_review_date,
                IsCurated = s.isCurated,
                Tag = s.Tag == null ? null : new TagDto
                {
                    Id = s.Tag.id,
                    SubjectName = s.Tag.subject_name,
                    ChapterNumber = s.Tag.chapter_number,
                    ChapterName = s.Tag.chapter_name,
                    TopicName = s.Tag.topic_name
                },
                Questions = s.questions.OfType<SimpleQuestion>().Select(q => new QuestionDto
                {
                    Id = q.Id,
                    Text = q.TextElement?.simple_text ?? q.TextElement?.formatted_text,
                    ImagePath = q.ImageElement?.simple_image?.ToString(),
                    IsEditable = q.isEditable
                }).ToList()
            };
        }

        private List<BasicSessionBundle>? DeserializeSessions(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<SessionDto>>(json);
            if (dtos == null) return null;

            var result = new List<BasicSessionBundle>();
            foreach (var d in dtos)
            {
                var tag = d.Tag == null ? null : new Tag(d.Tag.Id, d.Tag.SubjectName, d.Tag.ChapterNumber, d.Tag.ChapterName, d.Tag.TopicName);
                var questions = d.Questions.Select(q => new SimpleQuestion(q.Text, q.ImagePath, tag, q.IsEditable) { Id = q.Id }).ToList();
                var session = new BasicSessionBundle(questions, d.IsCurated, d.NextReviewDate, d.SessionName, tag)
                {
                    Id = d.Id,
                    creation_date = d.CreationDate
                };
                result.Add(session);
            }
            return result;
        }

        #endregion
    }
}

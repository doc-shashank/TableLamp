using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TableLamp.Models;

namespace TableLamp.Services
{
    /// <summary>
    /// Dedicated SQLite database managing card-level spaced repetition state, schedules,
    /// and historical recall logs in an independent database file: spaced_repetition.db.
    /// </summary>
    public class SpacedRepetitionDatabase : IDisposable
    {
        private static SpacedRepetitionDatabase? _instance;
        public static SpacedRepetitionDatabase Instance => _instance ??= new SpacedRepetitionDatabase();

        private readonly string _connectionString;
        private readonly object _lock = new();
        private SqliteConnection? _keepAliveConnection; // Used when in-memory database is specified (:memory:)

        /// <summary>
        /// Default constructor: initializes database in %LocalAppData%/TableLamp/spaced_repetition.db.
        /// </summary>
        public SpacedRepetitionDatabase()
            : this(GetDefaultDbPath())
        {
        }

        /// <summary>
        /// Constructor taking an explicit file path or SQLite connection string (e.g. "Data Source=:memory:").
        /// </summary>
        public SpacedRepetitionDatabase(string pathOrConnectionString)
        {
            if (pathOrConnectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
            {
                string memDbName = $"MemDb_{Guid.NewGuid():N}";
                _connectionString = $"Data Source={memDbName};Mode=Memory;Cache=Shared";
                _keepAliveConnection = new SqliteConnection(_connectionString);
                _keepAliveConnection.Open();
            }
            else if (pathOrConnectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                _connectionString = pathOrConnectionString;
            }
            else
            {
                string? dir = Path.GetDirectoryName(pathOrConnectionString);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                _connectionString = $"Data Source={pathOrConnectionString};Mode=ReadWriteCreate";
            }

            InitializeDatabase();
        }

        private static string GetDefaultDbPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "TableLamp");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "spaced_repetition.db");
        }

        private SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private void InitializeDatabase()
        {
            lock (_lock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS QuestionReviewState (
                        QuestionId TEXT PRIMARY KEY NOT NULL,
                        SessionId TEXT NOT NULL,
                        IntervalDays REAL NOT NULL,
                        EaseFactor REAL NOT NULL,
                        LastReviewedAt TEXT NOT NULL,
                        NextDueDate TEXT NOT NULL,
                        IsGraduated INTEGER NOT NULL,
                        TotalRepetitions INTEGER NOT NULL,
                        LapsesCount INTEGER NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS IX_QuestionReviewState_SessionId ON QuestionReviewState(SessionId);
                    CREATE INDEX IF NOT EXISTS IX_QuestionReviewState_NextDueDate ON QuestionReviewState(NextDueDate);

                    CREATE TABLE IF NOT EXISTS ReviewHistory (
                        Id TEXT PRIMARY KEY NOT NULL,
                        QuestionId TEXT NOT NULL,
                        SessionId TEXT NOT NULL,
                        Rating INTEGER NOT NULL,
                        RatingName TEXT NOT NULL,
                        ReviewedAt TEXT NOT NULL,
                        PreviousInterval REAL NOT NULL,
                        NewInterval REAL NOT NULL,
                        PreviousEase REAL NOT NULL,
                        NewEase REAL NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS IX_ReviewHistory_QuestionId ON ReviewHistory(QuestionId);
                    CREATE INDEX IF NOT EXISTS IX_ReviewHistory_SessionId ON ReviewHistory(SessionId);
                ";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Converts any string ID (like 'BND-638...' or custom key) deterministically into a Guid.
        /// </summary>
        public static Guid DeterministicGuid(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Guid.NewGuid();
            if (Guid.TryParse(id, out var parsed)) return parsed;

            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(id));
            return new Guid(hash);
        }

        public QuestionReviewState? GetCardState(Guid questionId)
        {
            lock (_lock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT QuestionId, SessionId, IntervalDays, EaseFactor, LastReviewedAt, NextDueDate, IsGraduated, TotalRepetitions, LapsesCount
                    FROM QuestionReviewState
                    WHERE QuestionId = $qid LIMIT 1;
                ";
                cmd.Parameters.AddWithValue("$qid", questionId.ToString("D"));

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return ReadState(reader);
                }
                return null;
            }
        }

        public Task<QuestionReviewState?> GetCardStateAsync(Guid questionId)
        {
            return Task.Run(() => GetCardState(questionId));
        }

        public QuestionReviewState GetOrCreateCardState(Guid questionId, Guid sessionId)
        {
            var existing = GetCardState(questionId);
            if (existing != null) return existing;

            var newState = new QuestionReviewState
            {
                QuestionId = questionId,
                SessionId = sessionId,
                IntervalDays = 1.0,
                EaseFactor = 2.5,
                LastReviewedAt = DateTimeOffset.UtcNow,
                NextDueDate = DateTimeOffset.UtcNow.AddDays(1.0),
                IsGraduated = false,
                TotalRepetitions = 0,
                LapsesCount = 0
            };

            SaveCardState(newState);
            return newState;
        }

        public Task<QuestionReviewState> GetOrCreateCardStateAsync(Guid questionId, Guid sessionId)
        {
            return Task.Run(() => GetOrCreateCardState(questionId, sessionId));
        }

        public void SaveCardState(QuestionReviewState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            lock (_lock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO QuestionReviewState (
                        QuestionId, SessionId, IntervalDays, EaseFactor, LastReviewedAt, NextDueDate, IsGraduated, TotalRepetitions, LapsesCount
                    ) VALUES (
                        $qid, $sid, $interval, $ease, $lastRev, $nextDue, $graduated, $reps, $lapses
                    )
                    ON CONFLICT(QuestionId) DO UPDATE SET
                        SessionId = excluded.SessionId,
                        IntervalDays = excluded.IntervalDays,
                        EaseFactor = excluded.EaseFactor,
                        LastReviewedAt = excluded.LastReviewedAt,
                        NextDueDate = excluded.NextDueDate,
                        IsGraduated = excluded.IsGraduated,
                        TotalRepetitions = excluded.TotalRepetitions,
                        LapsesCount = excluded.LapsesCount;
                ";

                cmd.Parameters.AddWithValue("$qid", state.QuestionId.ToString("D"));
                cmd.Parameters.AddWithValue("$sid", state.SessionId.ToString("D"));
                cmd.Parameters.AddWithValue("$interval", state.IntervalDays);
                cmd.Parameters.AddWithValue("$ease", state.EaseFactor);
                cmd.Parameters.AddWithValue("$lastRev", state.LastReviewedAt.ToString("o"));
                cmd.Parameters.AddWithValue("$nextDue", state.NextDueDate.ToString("o"));
                cmd.Parameters.AddWithValue("$graduated", state.IsGraduated ? 1 : 0);
                cmd.Parameters.AddWithValue("$reps", state.TotalRepetitions);
                cmd.Parameters.AddWithValue("$lapses", state.LapsesCount);

                cmd.ExecuteNonQuery();
            }
        }

        public Task SaveCardStateAsync(QuestionReviewState state)
        {
            return Task.Run(() => SaveCardState(state));
        }

        public void RecordReviewHistory(
            QuestionReviewState state,
            RecallRating rating,
            double prevInterval,
            double prevEase)
        {
            ArgumentNullException.ThrowIfNull(state);

            lock (_lock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO ReviewHistory (
                        Id, QuestionId, SessionId, Rating, RatingName, ReviewedAt, PreviousInterval, NewInterval, PreviousEase, NewEase
                    ) VALUES (
                        $id, $qid, $sid, $rating, $ratingName, $revAt, $prevI, $newI, $prevE, $newE
                    );
                ";

                cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
                cmd.Parameters.AddWithValue("$qid", state.QuestionId.ToString("D"));
                cmd.Parameters.AddWithValue("$sid", state.SessionId.ToString("D"));
                cmd.Parameters.AddWithValue("$rating", (int)rating);
                cmd.Parameters.AddWithValue("$ratingName", rating.ToString());
                cmd.Parameters.AddWithValue("$revAt", state.LastReviewedAt.ToString("o"));
                cmd.Parameters.AddWithValue("$prevI", prevInterval);
                cmd.Parameters.AddWithValue("$newI", state.IntervalDays);
                cmd.Parameters.AddWithValue("$prevE", prevEase);
                cmd.Parameters.AddWithValue("$newE", state.EaseFactor);

                cmd.ExecuteNonQuery();
            }
        }

        public Task RecordReviewHistoryAsync(
            QuestionReviewState state,
            RecallRating rating,
            double prevInterval,
            double prevEase)
        {
            return Task.Run(() => RecordReviewHistory(state, rating, prevInterval, prevEase));
        }

        public IReadOnlyList<QuestionReviewState> GetStatesForSession(Guid sessionId)
        {
            var list = new List<QuestionReviewState>();
            lock (_lock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT QuestionId, SessionId, IntervalDays, EaseFactor, LastReviewedAt, NextDueDate, IsGraduated, TotalRepetitions, LapsesCount
                    FROM QuestionReviewState
                    WHERE SessionId = $sid;
                ";
                cmd.Parameters.AddWithValue("$sid", sessionId.ToString("D"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(ReadState(reader));
                }
            }
            return list.AsReadOnly();
        }

        public Task<IReadOnlyList<QuestionReviewState>> GetStatesForSessionAsync(Guid sessionId)
        {
            return Task.Run(() => GetStatesForSession(sessionId));
        }

        public IReadOnlyList<QuestionReviewState> GetAllDueCards(DateTimeOffset? asOf = null)
        {
            DateTimeOffset now = asOf ?? DateTimeOffset.UtcNow;
            string nowIso = now.ToString("o");

            var list = new List<QuestionReviewState>();
            lock (_lock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT QuestionId, SessionId, IntervalDays, EaseFactor, LastReviewedAt, NextDueDate, IsGraduated, TotalRepetitions, LapsesCount
                    FROM QuestionReviewState
                    WHERE IsGraduated = 0 AND NextDueDate <= $now
                    ORDER BY NextDueDate ASC;
                ";
                cmd.Parameters.AddWithValue("$now", nowIso);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(ReadState(reader));
                }
            }
            return list.AsReadOnly();
        }

        public Task<IReadOnlyList<QuestionReviewState>> GetAllDueCardsAsync(DateTimeOffset? asOf = null)
        {
            return Task.Run(() => GetAllDueCards(asOf));
        }

        private static QuestionReviewState ReadState(SqliteDataReader reader)
        {
            return new QuestionReviewState
            {
                QuestionId = Guid.Parse(reader.GetString(0)),
                SessionId = Guid.Parse(reader.GetString(1)),
                IntervalDays = reader.GetDouble(2),
                EaseFactor = reader.GetDouble(3),
                LastReviewedAt = DateTimeOffset.Parse(reader.GetString(4)),
                NextDueDate = DateTimeOffset.Parse(reader.GetString(5)),
                IsGraduated = reader.GetInt32(6) == 1,
                TotalRepetitions = reader.GetInt32(7),
                LapsesCount = reader.GetInt32(8)
            };
        }

        public void Dispose()
        {
            _keepAliveConnection?.Dispose();
            _keepAliveConnection = null;
        }
    }
}

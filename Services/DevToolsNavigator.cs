using System;
using System.Collections.Generic;
using System.Linq;

namespace TableLamp.Services
{
    public class DevElementPosition
    {
        public string Type { get; set; } = ""; // "Subject", "Chapter", "Topic"
        public string SubjectKey { get; set; } = "";
        public string? ChapterKey { get; set; }
        public string? TopicKey { get; set; }
        public int LineIndex { get; set; }
        public int EndLineIndex { get; set; }
        public int CharOffset { get; set; }
        public int EndCharOffset { get; set; }
        public string DisplayLabel { get; set; } = "";
    }

    public class DevContextResult
    {
        public int LineIndex { get; set; }
        public string ElementType { get; set; } = "Root"; // "Subject", "Chapter", "Topic", "Root"
        public string? SubjectKey { get; set; }
        public string? ChapterKey { get; set; }
        public string? TopicKey { get; set; }
        public string LineContent { get; set; } = "";
        public int CaretOffset { get; set; }
    }

    public static class DevToolsNavigator
    {
        /// <summary>
        /// Scans a serialized JSON presets string line-by-line and records the exact character offset
        /// and line index of each Subject, Chapter, and Topic in document order.
        /// </summary>
        public static List<DevElementPosition> ScanElementPositions(string text)
        {
            var list = new List<DevElementPosition>();
            if (string.IsNullOrEmpty(text)) return list;

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int currentOffset = 0;
            string? currentSubj = null;
            string? currentCh = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                // 1. Subject Header: Starts with 2 spaces and quote, ends with ": {", not Chapters/chapter/topic
                if (line.StartsWith("  \"") && trimmed.EndsWith(": {") &&
                    !trimmed.Contains("\"Chapters\"") &&
                    !trimmed.StartsWith("\"chapter", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("\"topic_", StringComparison.OrdinalIgnoreCase))
                {
                    int q1 = line.IndexOf('"');
                    int q2 = line.IndexOf('"', q1 + 1);
                    if (q1 >= 0 && q2 > q1)
                    {
                        currentSubj = line.Substring(q1 + 1, q2 - q1 - 1);
                        currentCh = null;
                        list.Add(new DevElementPosition
                        {
                            Type = "Subject",
                            SubjectKey = currentSubj,
                            LineIndex = i,
                            CharOffset = currentOffset + q1,
                            DisplayLabel = $"Subject: {currentSubj}"
                        });
                    }
                }
                // 2. Chapter Header: starts with "chapter" or "Chapter" (not "Chapters") and ends with ": {"
                else if (!trimmed.StartsWith("\"Chapters\"", StringComparison.OrdinalIgnoreCase) &&
                         (trimmed.StartsWith("\"chapter", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("\"Chapter", StringComparison.OrdinalIgnoreCase))
                         && trimmed.EndsWith(": {"))
                {
                    int q1 = line.IndexOf('"');
                    int q2 = line.IndexOf('"', q1 + 1);
                    if (q1 >= 0 && q2 > q1 && currentSubj != null)
                    {
                        currentCh = line.Substring(q1 + 1, q2 - q1 - 1);
                        list.Add(new DevElementPosition
                        {
                            Type = "Chapter",
                            SubjectKey = currentSubj,
                            ChapterKey = currentCh,
                            LineIndex = i,
                            CharOffset = currentOffset + q1,
                            DisplayLabel = $"Chapter: {currentCh} ({currentSubj})"
                        });
                    }
                }
                // 3. Topic Header: starts with "topic_" and ends with ": {"
                else if (trimmed.StartsWith("\"topic_", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(": {"))
                {
                    int q1 = line.IndexOf('"');
                    int q2 = line.IndexOf('"', q1 + 1);
                    if (q1 >= 0 && q2 > q1 && currentSubj != null && currentCh != null)
                    {
                        string topKey = line.Substring(q1 + 1, q2 - q1 - 1);
                        list.Add(new DevElementPosition
                        {
                            Type = "Topic",
                            SubjectKey = currentSubj,
                            ChapterKey = currentCh,
                            TopicKey = topKey,
                            LineIndex = i,
                            CharOffset = currentOffset + q1,
                            DisplayLabel = $"Topic: {topKey} ({currentCh})"
                        });
                    }
                }

                // Advance currentOffset
                currentOffset += line.Length;
                if (i < lines.Length - 1)
                {
                    if (currentOffset < text.Length && text[currentOffset] == '\r') currentOffset++;
                    if (currentOffset < text.Length && text[currentOffset] == '\n') currentOffset++;
                }
            }

            // Compute EndLineIndex for each element
            for (int k = 0; k < list.Count; k++)
            {
                var elem = list[k];
                int braceBalance = 0;
                bool foundFirstBrace = false;
                elem.EndLineIndex = lines.Length - 1;

                for (int j = elem.LineIndex; j < lines.Length; j++)
                {
                    foreach (char ch in lines[j])
                    {
                        if (ch == '{') { braceBalance++; foundFirstBrace = true; }
                        else if (ch == '}') { braceBalance--; }
                    }
                    if (foundFirstBrace && braceBalance <= 0)
                    {
                        elem.EndLineIndex = j;
                        break;
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Detects the active hierarchy context (Subject, Chapter, Topic) at a given caret position in JSON text.
        /// </summary>
        public static DevContextResult DetectContextAtCaret(string text, int caret)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new DevContextResult { ElementType = "Root" };
            }

            caret = Math.Clamp(caret, 0, text.Length);

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int charCounter = 0;
            int currentLineIndex = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                int lineLen = lines[i].Length;
                int sepLen = 1;
                if (charCounter + lineLen < text.Length && text[charCounter + lineLen] == '\r')
                {
                    if (charCounter + lineLen + 1 < text.Length && text[charCounter + lineLen + 1] == '\n')
                        sepLen = 2;
                }

                if (caret <= charCounter + lineLen)
                {
                    currentLineIndex = i;
                    break;
                }

                charCounter += lineLen + sepLen;
                if (i == lines.Length - 1) currentLineIndex = i;
            }

            string currentLine = currentLineIndex < lines.Length ? lines[currentLineIndex] : "";
            var elements = ScanElementPositions(text);

            if (elements.Count == 0)
            {
                return new DevContextResult
                {
                    LineIndex = currentLineIndex,
                    ElementType = "Root",
                    LineContent = currentLine,
                    CaretOffset = caret
                };
            }

            // 1. Check if inside any Topic
            var topicElem = elements.FirstOrDefault(e => e.Type == "Topic" && currentLineIndex >= e.LineIndex && currentLineIndex <= e.EndLineIndex);
            if (topicElem != null)
            {
                return new DevContextResult
                {
                    LineIndex = currentLineIndex,
                    ElementType = "Topic",
                    SubjectKey = topicElem.SubjectKey,
                    ChapterKey = topicElem.ChapterKey,
                    TopicKey = topicElem.TopicKey,
                    LineContent = currentLine,
                    CaretOffset = caret
                };
            }

            // 2. Check if inside any Chapter
            var chElem = elements.FirstOrDefault(e => e.Type == "Chapter" && currentLineIndex >= e.LineIndex && currentLineIndex <= e.EndLineIndex);
            if (chElem != null)
            {
                return new DevContextResult
                {
                    LineIndex = currentLineIndex,
                    ElementType = "Chapter",
                    SubjectKey = chElem.SubjectKey,
                    ChapterKey = chElem.ChapterKey,
                    TopicKey = null,
                    LineContent = currentLine,
                    CaretOffset = caret
                };
            }

            // 3. Check if inside any Subject
            var subjElem = elements.FirstOrDefault(e => e.Type == "Subject" && currentLineIndex >= e.LineIndex && currentLineIndex <= e.EndLineIndex);
            if (subjElem != null)
            {
                return new DevContextResult
                {
                    LineIndex = currentLineIndex,
                    ElementType = "Subject",
                    SubjectKey = subjElem.SubjectKey,
                    ChapterKey = null,
                    TopicKey = null,
                    LineContent = currentLine,
                    CaretOffset = caret
                };
            }

            // 4. Closest prior element if within document bounds
            var closestPrior = elements.LastOrDefault(e => e.LineIndex <= currentLineIndex);
            if (closestPrior != null)
            {
                return new DevContextResult
                {
                    LineIndex = currentLineIndex,
                    ElementType = closestPrior.Type,
                    SubjectKey = closestPrior.SubjectKey,
                    ChapterKey = closestPrior.ChapterKey,
                    TopicKey = closestPrior.TopicKey,
                    LineContent = currentLine,
                    CaretOffset = caret
                };
            }

            return new DevContextResult
            {
                LineIndex = currentLineIndex,
                ElementType = "Root",
                SubjectKey = elements[0].SubjectKey,
                LineContent = currentLine,
                CaretOffset = caret
            };
        }

        /// <summary>
        /// Finds current element index based on exact key hierarchy matching, or falls back to closest caret position.
        /// Avoids duplicate key collisions across chapters (e.g. topic_1 in multiple chapters).
        /// </summary>
        public static int FindCurrentElementIndex(List<DevElementPosition> elements, string? activeSubj, string? activeCh, string? activeTop, int caret = 0)
        {
            if (elements.Count == 0) return 0;

            if (!string.IsNullOrEmpty(activeTop) && !string.IsNullOrEmpty(activeCh) && !string.IsNullOrEmpty(activeSubj))
            {
                int idx = elements.FindIndex(e => e.Type == "Topic" &&
                                                  string.Equals(e.SubjectKey, activeSubj, StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals(e.ChapterKey, activeCh, StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals(e.TopicKey, activeTop, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) return idx;
            }

            if (!string.IsNullOrEmpty(activeCh) && !string.IsNullOrEmpty(activeSubj))
            {
                int idx = elements.FindIndex(e => e.Type == "Chapter" &&
                                                  string.Equals(e.SubjectKey, activeSubj, StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals(e.ChapterKey, activeCh, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) return idx;
            }

            if (!string.IsNullOrEmpty(activeSubj))
            {
                int idx = elements.FindIndex(e => e.Type == "Subject" &&
                                                  string.Equals(e.SubjectKey, activeSubj, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) return idx;
            }

            int pos = elements.FindLastIndex(e => e.CharOffset <= caret);
            return pos >= 0 ? pos : 0;
        }

        /// <summary>
        /// Calculates the next index with wrap-around.
        /// </summary>
        public static int GetNextIndex(int currentIndex, int count)
        {
            if (count <= 0) return 0;
            return (currentIndex + 1) % count;
        }

        /// <summary>
        /// Calculates the previous index with wrap-around.
        /// </summary>
        public static int GetPreviousIndex(int currentIndex, int count)
        {
            if (count <= 0) return 0;
            return (currentIndex - 1 + count) % count;
        }
    }
}

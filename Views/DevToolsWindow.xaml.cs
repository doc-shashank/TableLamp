using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TableLamp.Models;
using TableLamp.Services;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace TableLamp.Views
{
    public sealed partial class DevToolsWindow : Window
    {
        private AppWindow? _appWindow;
        private readonly PresetTagGenerator _generator = new();
        private Dictionary<string, PresetSubject> _tree = new(StringComparer.OrdinalIgnoreCase);

        private string? _activeSubjectKey;
        private string? _activeChapterKey;
        private string? _activeTopicKey;
        private bool _isUpdatingPreviewText;

        public DevToolsWindow()
        {
            this.InitializeComponent();

            ConfigureDevWindow();
            InitializeEditor();
        }

        private void ConfigureDevWindow()
        {
            try
            {
                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                if (hwnd != IntPtr.Zero)
                {
                    WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    _appWindow = AppWindow.GetFromWindowId(windowId);

                    if (_appWindow != null)
                    {
                        _appWindow.Title = "Table Lamp - Developer Tools & Preset Tag Generator";

                        // Maximize the dev tools window per requirement
                        if (_appWindow.Presenter is OverlappedPresenter presenter)
                        {
                            presenter.Maximize();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback for non-standard environments
            }
        }

        private void InitializeEditor()
        {
            // Load current presets from database
            string currentJson = PresetTagDatabase.Instance.ExportJson();
            _tree = _generator.Parse(currentJson);
            SetJsonText(currentJson);

            // Wire selection events in JSON preview textarea
            JsonPreviewBox.SelectionChanged += OnJsonPreviewSelectionChanged;
            JsonPreviewBox.KeyUp += (s, e) => DetectContextFromSelection();
            JsonPreviewBox.PointerReleased += (s, e) => DetectContextFromSelection();

            // Wire toolbar actions
            SaveToDatabaseButton.Click += OnSaveToDatabaseClicked;
            LoadStarterButton.Click += OnLoadStarterClicked;
            CopyJsonButton.Click += OnCopyJsonClicked;
            ExportJsonButton.Click += OnExportJsonClicked;
            CloseButton.Click += (s, e) => this.Close();

            // Wire Subject panel actions
            UpdateSubjectButton.Click += OnUpdateSubjectClicked;
            AddChapterButton.Click += OnAddChapterClicked;

            // Wire Chapter panel actions
            UpdateChapterButton.Click += OnUpdateChapterClicked;
            AddTopicFromChapterButton.Click += OnAddTopicFromChapterClicked;

            // Wire Topic panel actions
            UpdateTopicButton.Click += OnUpdateTopicClicked;
            AddTopicButton.Click += OnAddTopicClicked;

            // Wire Root / General actions
            AddSubjectButton.Click += OnAddSubjectClicked;

            // Initial context detection
            DetectContextFromSelection();
        }

        private void SetJsonText(string json)
        {
            _isUpdatingPreviewText = true;
            JsonPreviewBox.Text = json;
            _isUpdatingPreviewText = false;
        }

        private void OnJsonPreviewSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_isUpdatingPreviewText)
            {
                DetectContextFromSelection();
            }
        }

        private void DetectContextFromSelection()
        {
            string fullText = JsonPreviewBox.Text;
            if (string.IsNullOrEmpty(fullText))
            {
                ShowGeneralPanel("Empty Database");
                return;
            }

            int selStart = JsonPreviewBox.SelectionStart;
            var lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int currentLineIndex = 0;
            int charPos = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                int lineLen = lines[i].Length + Environment.NewLine.Length;
                if (selStart >= charPos && selStart <= charPos + lineLen)
                {
                    currentLineIndex = i;
                    break;
                }
                charPos += lineLen;
            }

            ContextLineNumberText.Text = $"Line {currentLineIndex + 1}";
            string currentLine = lines[Math.Min(currentLineIndex, lines.Length - 1)].Trim();

            // Trace hierarchy from beginning to current line to find current subject, chapter, topic
            string? foundSubj = null;
            string? foundCh = null;
            string? foundTop = null;

            for (int i = 0; i <= currentLineIndex && i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                // Check indentation / prefix patterns
                if (line.StartsWith("  \"") && line.Contains("\": {") && !line.Contains("\"Chapters\""))
                {
                    int quote1 = line.IndexOf('"');
                    int quote2 = line.IndexOf('"', quote1 + 1);
                    if (quote1 >= 0 && quote2 > quote1)
                    {
                        foundSubj = line.Substring(quote1 + 1, quote2 - quote1 - 1);
                        foundCh = null;
                        foundTop = null;
                    }
                }
                else if (line.Contains("\"Chapter ") && line.Contains("\": {"))
                {
                    int quote1 = line.IndexOf("\"Chapter ");
                    int quote2 = line.IndexOf('"', quote1 + 1);
                    if (quote1 >= 0 && quote2 > quote1)
                    {
                        foundCh = line.Substring(quote1 + 1, quote2 - quote1 - 1);
                        foundTop = null;
                    }
                }
                else if (line.Contains("\"topic_") && line.Contains("\": {"))
                {
                    int quote1 = line.IndexOf("\"topic_");
                    int quote2 = line.IndexOf('"', quote1 + 1);
                    if (quote1 >= 0 && quote2 > quote1)
                    {
                        foundTop = line.Substring(quote1 + 1, quote2 - quote1 - 1);
                    }
                }
            }

            _activeSubjectKey = foundSubj ?? _tree.Keys.FirstOrDefault();
            _activeChapterKey = foundCh;
            _activeTopicKey = foundTop;

            // Determine if current line is Topic, Chapter, or Subject context
            bool isTopicLine = currentLine.Contains("\"topic_") ||
                               currentLine.StartsWith("\"start_page\"") ||
                               currentLine.StartsWith("\"end_page\"") ||
                               (foundTop != null && currentLine.StartsWith("\"name\""));

            bool isChapterLine = !isTopicLine &&
                                 (currentLine.Contains("\"Chapter ") ||
                                  currentLine.Contains("\"Chapters\"") ||
                                  (foundCh != null && foundTop == null && currentLine.StartsWith("\"name\"")));

            bool isSubjectLine = !isTopicLine && !isChapterLine &&
                                 (currentLine.StartsWith("\"short_name\"") ||
                                  currentLine.StartsWith("\"full_name\"") ||
                                  currentLine.StartsWith("\"edition\"") ||
                                  (foundSubj != null && currentLine.Contains($"\"{foundSubj}\"")));

            if (isTopicLine && foundSubj != null && foundCh != null && _tree.TryGetValue(foundSubj, out var s) &&
                s.Chapters.TryGetValue(foundCh, out var c))
            {
                PresetTopic? topic = null;
                string topKey = foundTop ?? c.Topics.Keys.FirstOrDefault() ?? "topic_1";
                if (!c.Topics.TryGetValue(topKey, out topic))
                {
                    topic = c.Topics.Values.FirstOrDefault();
                }

                if (topic != null)
                {
                    ShowTopicPanel(foundCh, topic.Key, topic);
                    return;
                }
            }

            if ((isChapterLine || foundCh != null) && foundSubj != null && _tree.TryGetValue(foundSubj, out var subj) &&
                foundCh != null && subj.Chapters.TryGetValue(foundCh, out var chapter))
            {
                ShowChapterPanel(foundSubj, chapter.Key, chapter);
                return;
            }

            if (_activeSubjectKey != null && _tree.TryGetValue(_activeSubjectKey, out var activeSubject))
            {
                ShowSubjectPanel(activeSubject.Key, activeSubject);
                return;
            }

            ShowGeneralPanel("Root / Non-selection");
        }

        private void ShowSubjectPanel(string subjectKey, PresetSubject subject)
        {
            ContextTitleText.Text = $"Selected: Subject [{subject.short_name ?? subjectKey}]";
            SubjectPanel.Visibility = Visibility.Visible;
            ChapterPanel.Visibility = Visibility.Collapsed;
            TopicPanel.Visibility = Visibility.Collapsed;
            GeneralPanel.Visibility = Visibility.Collapsed;

            SubjectKeyBox.Text = subjectKey;
            SubjectShortNameBox.Text = subject.short_name ?? "";
            SubjectFullNameBox.Text = subject.full_name ?? "";
            SubjectEditionBox.Text = subject.edition ?? "";
        }

        private void ShowChapterPanel(string subjectKey, string chapterKey, PresetChapter chapter)
        {
            ContextTitleText.Text = $"Selected: Chapter [{chapter.name ?? chapterKey}]";
            SubjectPanel.Visibility = Visibility.Collapsed;
            ChapterPanel.Visibility = Visibility.Visible;
            TopicPanel.Visibility = Visibility.Collapsed;
            GeneralPanel.Visibility = Visibility.Collapsed;

            ChapterParentSubjectBox.Text = subjectKey;
            ChapterKeyBox.Text = chapterKey;
            ChapterNameBox.Text = chapter.name ?? "";
        }

        private void ShowTopicPanel(string chapterKey, string topicKey, PresetTopic topic)
        {
            ContextTitleText.Text = $"Selected: Topic [{topic.name ?? topicKey}]";
            SubjectPanel.Visibility = Visibility.Collapsed;
            ChapterPanel.Visibility = Visibility.Collapsed;
            TopicPanel.Visibility = Visibility.Visible;
            GeneralPanel.Visibility = Visibility.Collapsed;

            TopicParentChapterBox.Text = chapterKey;
            TopicKeyBox.Text = topicKey;
            TopicNameBox.Text = topic.name ?? "";
            TopicStartPageBox.Text = topic.start_page ?? "";
            TopicEndPageBox.Text = topic.end_page ?? "";
        }

        private void ShowGeneralPanel(string context)
        {
            ContextTitleText.Text = $"Selected: {context}";
            SubjectPanel.Visibility = Visibility.Collapsed;
            ChapterPanel.Visibility = Visibility.Collapsed;
            TopicPanel.Visibility = Visibility.Collapsed;
            GeneralPanel.Visibility = Visibility.Visible;
        }

        private void OnUpdateSubjectClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeSubjectKey) || !_tree.TryGetValue(_activeSubjectKey, out var subject))
                return;

            subject.short_name = SubjectShortNameBox.Text.Trim();
            subject.full_name = SubjectFullNameBox.Text.Trim();
            subject.edition = SubjectEditionBox.Text.Trim();

            RefreshJsonPreview(targetKeyToSelect: $"\"{_activeSubjectKey}\"");
            ShowStatus($"Updated subject '{subject.short_name}'.", InfoBarSeverity.Success);
        }

        private void OnAddChapterClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeSubjectKey) || !_tree.TryGetValue(_activeSubjectKey, out var subject))
            {
                _activeSubjectKey = _tree.Keys.FirstOrDefault();
                if (_activeSubjectKey == null || !_tree.TryGetValue(_activeSubjectKey, out subject))
                {
                    ShowStatus("Please create a subject first.", InfoBarSeverity.Warning);
                    return;
                }
            }

            int nextNum = subject.Chapters.Count + 1;
            string newChKey = $"Chapter {nextNum}";
            while (subject.Chapters.ContainsKey(newChKey))
            {
                nextNum++;
                newChKey = $"Chapter {nextNum}";
            }

            var newChapter = new PresetChapter
            {
                Key = newChKey,
                name = $"Chapter {nextNum} Name",
                Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
            };

            // Add default first topic so the chapter is complete
            newChapter.Topics["topic_1"] = new PresetTopic
            {
                Key = "topic_1",
                name = "Overview",
                start_page = "1",
                end_page = "10"
            };

            subject.Chapters[newChKey] = newChapter;
            _activeChapterKey = newChKey;

            // Move preview selection to the newly added chapter line
            RefreshJsonPreview(targetKeyToSelect: $"\"{newChKey}\"");

            // Display the fields that can change its main attributes
            ShowChapterPanel(_activeSubjectKey, newChKey, newChapter);
            ChapterNameBox.Focus(FocusState.Programmatic);
            ChapterNameBox.SelectAll();

            ShowStatus($"Added '{newChKey}' to '{_activeSubjectKey}'. Fields ready to edit.", InfoBarSeverity.Success);
        }

        private void OnUpdateChapterClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeSubjectKey) || !_tree.TryGetValue(_activeSubjectKey, out var subject))
                return;

            if (string.IsNullOrEmpty(_activeChapterKey) || !subject.Chapters.TryGetValue(_activeChapterKey, out var chapter))
                return;

            chapter.name = ChapterNameBox.Text.Trim();
            RefreshJsonPreview(targetKeyToSelect: $"\"{_activeChapterKey}\"");
            ShowStatus($"Updated chapter '{chapter.name}'.", InfoBarSeverity.Success);
        }

        private void OnAddTopicFromChapterClicked(object sender, RoutedEventArgs e)
        {
            AddNewTopicInternal();
        }

        private void OnAddTopicClicked(object sender, RoutedEventArgs e)
        {
            AddNewTopicInternal();
        }

        private void AddNewTopicInternal()
        {
            if (string.IsNullOrEmpty(_activeSubjectKey) || !_tree.TryGetValue(_activeSubjectKey, out var subject))
            {
                ShowStatus("No active subject selected.", InfoBarSeverity.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_activeChapterKey) || !subject.Chapters.TryGetValue(_activeChapterKey, out var chapter))
            {
                chapter = subject.Chapters.Values.FirstOrDefault();
                if (chapter == null)
                {
                    ShowStatus("Please add a chapter before adding topics.", InfoBarSeverity.Warning);
                    return;
                }
                _activeChapterKey = chapter.Key;
            }

            int nextNum = chapter.Topics.Count + 1;
            string newTopKey = $"topic_{nextNum}";
            while (chapter.Topics.ContainsKey(newTopKey))
            {
                nextNum++;
                newTopKey = $"topic_{nextNum}";
            }

            var newTopic = new PresetTopic
            {
                Key = newTopKey,
                name = $"New Topic {nextNum}",
                start_page = "1",
                end_page = "10"
            };

            chapter.Topics[newTopKey] = newTopic;
            _activeTopicKey = newTopKey;

            // Move selection to new topic and display its editable fields
            RefreshJsonPreview(targetKeyToSelect: $"\"{newTopKey}\"");
            ShowTopicPanel(chapter.Key, newTopKey, newTopic);

            TopicNameBox.Focus(FocusState.Programmatic);
            TopicNameBox.SelectAll();

            // The "Add Topic" button is retained in TopicPanel!
            ShowStatus($"Added '{newTopKey}' to '{chapter.Key}'. 'Add Topic' button retained.", InfoBarSeverity.Success);
        }

        private void OnUpdateTopicClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeSubjectKey) || !_tree.TryGetValue(_activeSubjectKey, out var subject))
                return;

            if (string.IsNullOrEmpty(_activeChapterKey) || !subject.Chapters.TryGetValue(_activeChapterKey, out var chapter))
                return;

            if (string.IsNullOrEmpty(_activeTopicKey) || !chapter.Topics.TryGetValue(_activeTopicKey, out var topic))
                return;

            topic.name = TopicNameBox.Text.Trim();
            topic.start_page = TopicStartPageBox.Text.Trim();
            topic.end_page = TopicEndPageBox.Text.Trim();

            RefreshJsonPreview(targetKeyToSelect: $"\"{_activeTopicKey}\"");
            ShowStatus($"Updated topic '{topic.name}'.", InfoBarSeverity.Success);
        }

        private void OnAddSubjectClicked(object sender, RoutedEventArgs e)
        {
            int num = _tree.Count + 1;
            string newKey = $"Subject {num}";
            var newSubject = new PresetSubject
            {
                Key = newKey,
                short_name = $"Subject {num}",
                full_name = $"Full Subject {num} Name",
                edition = "1st",
                Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
            };

            newSubject.Chapters["Chapter 1"] = new PresetChapter
            {
                Key = "Chapter 1",
                name = "Introduction",
                Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                {
                    ["topic_1"] = new PresetTopic { Key = "topic_1", name = "Overview", start_page = "1", end_page = "10" }
                }
            };

            _tree[newKey] = newSubject;
            _activeSubjectKey = newKey;

            RefreshJsonPreview(targetKeyToSelect: $"\"{newKey}\"");
            ShowSubjectPanel(newKey, newSubject);
            SubjectShortNameBox.Focus(FocusState.Programmatic);

            ShowStatus($"Added new subject '{newKey}'.", InfoBarSeverity.Success);
        }

        private void RefreshJsonPreview(string? targetKeyToSelect = null)
        {
            string newJson = _generator.GenerateJson(_tree);
            SetJsonText(newJson);

            if (!string.IsNullOrEmpty(targetKeyToSelect))
            {
                int index = newJson.IndexOf(targetKeyToSelect, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    JsonPreviewBox.Focus(FocusState.Programmatic);
                    JsonPreviewBox.SelectionStart = index;
                    JsonPreviewBox.SelectionLength = targetKeyToSelect.Length;
                }
            }
        }

        private void OnSaveToDatabaseClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                PresetTagDatabase.Instance.SaveTree(_tree);
                ShowStatus("Presets successfully saved and applied to PresetTagDatabase!", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to save: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void OnLoadStarterClicked(object sender, RoutedEventArgs e)
        {
            string starterJson = _generator.CreateStarterPresetJson();
            _tree = _generator.Parse(starterJson);
            SetJsonText(starterJson);
            DetectContextFromSelection();
            ShowStatus("Canonical starter presets loaded.", InfoBarSeverity.Success);
        }

        private void OnCopyJsonClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var package = new DataPackage();
                package.SetText(JsonPreviewBox.Text);
                Clipboard.SetContent(package);
                ShowStatus("Presets JSON copied to clipboard.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Copy failed: {ex.Message}", InfoBarSeverity.Warning);
            }
        }

        private void OnExportJsonClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string exportPath = Path.Combine(localFolder, "TableLamp", "presets_export.json");
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
                File.WriteAllText(exportPath, JsonPreviewBox.Text);
                ShowStatus($"Exported presets file to: {exportPath}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Export failed: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}

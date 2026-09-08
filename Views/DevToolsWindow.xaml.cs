using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TableLamp.Models;
using TableLamp.Services;
using WinRT.Interop;

namespace TableLamp.Views
{
    public sealed partial class DevToolsWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static DevToolsWindow? _activeInstance;
        public static DevToolsWindow? ActiveInstance => _activeInstance;

        public static DevToolsWindow GetOrCreateInstance()
        {
            if (_activeInstance != null)
            {
                _activeInstance.Activate();
                IntPtr hwnd = WindowNative.GetWindowHandle(_activeInstance);
                if (hwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hwnd);
                }
                return _activeInstance;
            }

            _activeInstance = new DevToolsWindow();
            _activeInstance.Closed += (s, e) =>
            {
                _activeInstance._caretMonitorTimer?.Stop();
                _activeInstance = null;
            };
            return _activeInstance;
        }

        private AppWindow? _appWindow;
        private readonly PresetTagGenerator _generator = new();
        private Dictionary<string, PresetSubject> _tree = new(StringComparer.OrdinalIgnoreCase);

        private string? _activeSubjectKey;
        private string? _activeChapterKey;
        private string? _activeTopicKey;
        private string? _activeFilePath;
        private string? _activeWorkspaceDir;

        private bool _isUpdatingPreviewText;
        private string _previewMode = "Simplified"; // "Simplified" or "JSON"
        private readonly HashSet<string> _expandedNodes = new(StringComparer.OrdinalIgnoreCase);

        // Navigation history
        private readonly List<string> _navigationHistory = new();
        private int _historyIndex = -1;
        private bool _isNavigatingHistory;

        // Real-time Caret Monitor
        private DispatcherTimer? _caretMonitorTimer;
        private int _lastReportedCaret = -1;
        private int _lastReportedSelLen = -1;

        public DevToolsWindow()
        {
            this.InitializeComponent();

            ConfigureDevWindow();
            WireCaptionButtons();
            InitializeEditor();
        }

        private void WireCaptionButtons()
        {
            WindowMinimizeButton.Click += (s, e) =>
            {
                if (_appWindow?.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                }
            };

            WindowExitButton.Click += (s, e) =>
            {
                this.Close();
            };

            WindowExitButton.PointerEntered += (s, e) =>
            {
                WindowExitButton.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 232, 17, 35));
                if (WindowExitButton.Content is FontIcon icon)
                {
                    icon.Foreground = new SolidColorBrush(Colors.White);
                }
            };

            WindowExitButton.PointerExited += (s, e) =>
            {
                WindowExitButton.Background = new SolidColorBrush(Colors.Transparent);
                if (WindowExitButton.Content is FontIcon icon)
                {
                    icon.ClearValue(FontIcon.ForegroundProperty);
                }
            };
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

                        // Border-only window without native title bar or native caption buttons
                        if (_appWindow.Presenter is OverlappedPresenter presenter)
                        {
                            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                            presenter.Maximize();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback
            }
        }

        private void InitializeEditor()
        {
            string currentJson = PresetTagDatabase.Instance.ExportJson();
            _tree = _generator.Parse(currentJson);
            SetJsonText(currentJson);

            // Wire selection and editing events in JSON preview textarea
            JsonPreviewBox.SelectionChanged += OnJsonPreviewSelectionChanged;
            JsonPreviewBox.KeyUp += (s, e) => DetectContextFromSelection();
            JsonPreviewBox.PointerReleased += (s, e) => DetectContextFromSelection();
            JsonPreviewBox.TextChanged += OnJsonPreviewTextChanged;
            JsonPreviewBox.LostFocus += (s, e) => SyncTreeFromPreviewText();

            // WinUI 3 handled event subscriptions to guarantee pointer and keyboard caret responsiveness
            JsonPreviewBox.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => DetectContextFromSelection());
            }), true);

            JsonPreviewBox.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => DetectContextFromSelection());
            }), true);

            JsonPreviewBox.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler((s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => DetectContextFromSelection());
            }), true);

            JsonPreviewBox.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => DetectContextFromSelection());
            }), true);

            // Lightweight periodic monitor ensuring caret movements (arrows, clicks, drag) immediately update attribute editor
            _caretMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _caretMonitorTimer.Tick += (s, e) =>
            {
                if (_isUpdatingPreviewText) return;
                int currentCaret = JsonPreviewBox.SelectionStart;
                int currentSelLen = JsonPreviewBox.SelectionLength;
                if (currentCaret != _lastReportedCaret || currentSelLen != _lastReportedSelLen)
                {
                    _lastReportedCaret = currentCaret;
                    _lastReportedSelLen = currentSelLen;
                    DetectContextFromSelection();
                }
            };
            _caretMonitorTimer.Start();

            // Wire toolbar actions
            StartFromScratchButton.Click += OnStartFromScratchClicked;
            OpenJsonFileButton.Click += OnOpenJsonFileClicked;
            SaveJsonButton.Click += OnSaveJsonClicked;

            // Wire File menu actions
            MenuNewWorkspace.Click += OnNewWorkspaceClicked;
            MenuOpenWorkspace.Click += OnOpenWorkspaceClicked;
            MenuNewJsonFile.Click += OnNewJsonFileClicked;
            MenuOpenJsonFile.Click += OnOpenJsonFileClicked;
            MenuSaveJsonFile.Click += OnSaveJsonClicked;

            // Workspace context flyout
            ContextNewJsonFile.Click += OnNewJsonFileClicked;
            ContextNewFolder.Click += OnNewFolderClicked;
            ContextRefreshWorkspace.Click += (s, e) => RefreshWorkspaceFiles();
            CloseWorkspaceButton.Click += (s, e) => CloseWorkspace();
            WorkspaceFilesListView.ItemClick += OnWorkspaceFileClicked;
            WorkspaceFilesListView.IsItemClickEnabled = true;

            // Wire Navigation buttons (Next / Previous Element)
            NextElementButton.Click += OnNextElementClicked;
            PreviousElementButton.Click += OnPreviousElementClicked;

            // History back & forward buttons
            HistoryBackButton.Click += OnHistoryBackClicked;
            HistoryForwardButton.Click += OnHistoryForwardClicked;

            // Wire Preview mode switcher
            SimplifiedModeButton.Click += (s, e) => SwitchPreviewMode("Simplified");
            JsonModeButton.Click += (s, e) => SwitchPreviewMode("JSON");

            // Wire Delete buttons
            DeleteSubjectButton.Click += (s, e) => DeleteSubject(_activeSubjectKey);
            DeleteChapterButton.Click += (s, e) => DeleteChapter(_activeSubjectKey, _activeChapterKey);
            DeleteTopicButton.Click += (s, e) => DeleteTopic(_activeSubjectKey, _activeChapterKey, _activeTopicKey);

            // Wire Subject panel actions
            UpdateSubjectButton.Click += OnUpdateSubjectClicked;
            AddChapterButton.Click += OnAddChapterClicked;

            // Wire Chapter panel actions
            ChapterParentSubjectButton.Click += OnChapterParentSubjectClicked;
            UpdateChapterButton.Click += OnUpdateChapterClicked;
            AddTopicFromChapterButton.Click += OnAddTopicClicked;

            // Wire Topic panel actions
            UpdateTopicButton.Click += OnUpdateTopicClicked;
            AddTopicButton.Click += OnAddTopicClicked;

            // Wire Root / General actions
            AddSubjectButton.Click += OnAddSubjectClicked;

            // Initially expand all subjects in simplified preview
            foreach (var key in _tree.Keys)
            {
                _expandedNodes.Add(key);
            }

            // Default to Simplified mode
            SwitchPreviewMode("Simplified");

            // Initial detection
            DetectContextFromSelection();
        }

        private void SetJsonText(string json)
        {
            _isUpdatingPreviewText = true;
            JsonPreviewBox.Text = json;
            _isUpdatingPreviewText = false;
        }

        private DispatcherTimer? _previewDebounceTimer;

        private void OnJsonPreviewTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingPreviewText) return;

            if (_previewDebounceTimer == null)
            {
                _previewDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                _previewDebounceTimer.Tick += (s, ev) =>
                {
                    _previewDebounceTimer.Stop();
                    SyncTreeFromPreviewText();
                };
            }
            _previewDebounceTimer.Stop();
            _previewDebounceTimer.Start();
        }

        private void SyncTreeFromPreviewText()
        {
            if (_isUpdatingPreviewText) return;

            string text = JsonPreviewBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                var parsed = _generator.Parse(text);
                if (parsed != null && parsed.Count > 0)
                {
                    _tree = parsed;
                    DetectContextFromSelection();
                }
            }
            catch
            {
                // Ignore incomplete JSON while user is actively typing
            }
        }

        private void OnJsonPreviewSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_isUpdatingPreviewText)
            {
                DetectContextFromSelection();
            }
        }

        /// <summary>
        /// Accurately detects line index and hierarchy context and updates the attribute editor card.
        /// </summary>
        private void DetectContextFromSelection()
        {
            string fullText = JsonPreviewBox.Text;
            if (string.IsNullOrEmpty(fullText))
            {
                ShowGeneralPanel("Empty Database");
                return;
            }

            int caret = JsonPreviewBox.SelectionStart;
            var context = DevToolsNavigator.DetectContextAtCaret(fullText, caret);

            int currentLineIndex = context.LineIndex;
            string trimmedCurrentLine = context.LineContent.Trim();

            // Update Active Line Indicator without selecting whole line text
            ContextLineNumberText.Text = $"Line {currentLineIndex + 1}";
            ActiveLineGutterText.Text = $"Line {currentLineIndex + 1}:";
            ActiveLineContentSnippetText.Text = string.IsNullOrWhiteSpace(trimmedCurrentLine) ? "(empty line)" : trimmedCurrentLine;

            _activeSubjectKey = context.SubjectKey ?? _tree.Keys.FirstOrDefault();
            _activeChapterKey = context.ChapterKey;
            _activeTopicKey = context.TopicKey;

            // Route to contextual attribute editor card based on detected hierarchy
            if (context.ElementType == "Topic" && context.SubjectKey != null && context.ChapterKey != null && context.TopicKey != null &&
                _tree.TryGetValue(context.SubjectKey, out var subjForTopic) &&
                subjForTopic.Chapters != null && subjForTopic.Chapters.TryGetValue(context.ChapterKey, out var chForTopic) &&
                chForTopic.Topics != null && chForTopic.Topics.TryGetValue(context.TopicKey, out var topicObj))
            {
                ShowTopicPanel(context.ChapterKey, topicObj.Key, topicObj);
                RecordHistoryToken($"topic:{context.SubjectKey}:{context.ChapterKey}:{topicObj.Key}");
                return;
            }

            if (context.ElementType == "Chapter" && context.SubjectKey != null && context.ChapterKey != null &&
                _tree.TryGetValue(context.SubjectKey, out var subjForChapter) &&
                subjForChapter.Chapters != null && subjForChapter.Chapters.TryGetValue(context.ChapterKey, out var chObj))
            {
                ShowChapterPanel(context.SubjectKey, chObj.Key, chObj);
                RecordHistoryToken($"chapter:{context.SubjectKey}:{chObj.Key}");
                return;
            }

            if (_activeSubjectKey != null && _tree.TryGetValue(_activeSubjectKey, out var activeSubject))
            {
                ShowSubjectPanel(activeSubject.Key, activeSubject);
                RecordHistoryToken($"subject:{activeSubject.Key}");
                return;
            }

            ShowGeneralPanel("Root Overview");
        }

        private void ShowSubjectPanel(string subjectKey, PresetSubject subject)
        {
            int sIdx = _tree.Keys.ToList().IndexOf(subjectKey) + 1;
            string displayId = $"Subject{(sIdx > 0 ? sIdx : 1)}";

            ContextTitleText.Text = $"Selected: {displayId} [{subject.short_name ?? subjectKey}]";
            SubjectPanel.Visibility = Visibility.Visible;
            ChapterPanel.Visibility = Visibility.Collapsed;
            TopicPanel.Visibility = Visibility.Collapsed;
            GeneralPanel.Visibility = Visibility.Collapsed;

            SubjectKeyBox.Text = displayId;
            SubjectKeyBox.IsReadOnly = true;
            SubjectShortNameBox.Text = subject.short_name ?? "";
            SubjectFullNameBox.Text = subject.full_name ?? "";
            SubjectEditionBox.Text = subject.edition ?? "";
        }

        private void ShowChapterPanel(string subjectKey, string chapterKey, PresetChapter chapter)
        {
            int cIdx = 1;
            if (_tree.TryGetValue(subjectKey, out var subj) && subj.Chapters != null)
            {
                cIdx = subj.Chapters.Keys.ToList().IndexOf(chapterKey) + 1;
            }
            string displayId = $"Chapter{(cIdx > 0 ? cIdx : 1)}";

            ContextTitleText.Text = $"Selected: {displayId} [{chapter.name ?? chapterKey}]";
            SubjectPanel.Visibility = Visibility.Collapsed;
            ChapterPanel.Visibility = Visibility.Visible;
            TopicPanel.Visibility = Visibility.Collapsed;
            GeneralPanel.Visibility = Visibility.Collapsed;

            string parentShortName = subjectKey;
            if (_tree.TryGetValue(subjectKey, out var s) && !string.IsNullOrWhiteSpace(s.short_name))
            {
                parentShortName = s.short_name;
            }
            ChapterParentSubjectText.Text = $"{parentShortName} (Click to navigate)";

            ChapterKeyBox.Text = displayId;
            ChapterKeyBox.IsReadOnly = true;
            ChapterNumberInput.Value = chapter.ChapterNumber;
            ChapterNameBox.Text = chapter.name ?? "";
        }

        private void ShowTopicPanel(string chapterKey, string topicKey, PresetTopic topic)
        {
            int tIdx = 1;
            if (!string.IsNullOrEmpty(_activeSubjectKey) && _tree.TryGetValue(_activeSubjectKey, out var subj) && subj.Chapters != null && subj.Chapters.TryGetValue(chapterKey, out var ch) && ch.Topics != null)
            {
                tIdx = ch.Topics.Keys.ToList().IndexOf(topicKey) + 1;
            }
            string displayId = $"Topic{(tIdx > 0 ? tIdx : 1)}";

            ContextTitleText.Text = $"Selected: {displayId} [{topic.name ?? topicKey}]";
            SubjectPanel.Visibility = Visibility.Collapsed;
            ChapterPanel.Visibility = Visibility.Collapsed;
            TopicPanel.Visibility = Visibility.Visible;
            GeneralPanel.Visibility = Visibility.Collapsed;

            TopicParentChapterBox.Text = chapterKey;
            TopicKeyBox.Text = displayId;
            TopicKeyBox.IsReadOnly = true;
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

        #region History Navigation

        private void RecordHistoryToken(string token)
        {
            if (_isNavigatingHistory) return;

            if (_historyIndex >= 0 && _historyIndex < _navigationHistory.Count && _navigationHistory[_historyIndex] == token)
                return;

            // Truncate forward history if navigating after a fork
            if (_historyIndex >= 0 && _historyIndex < _navigationHistory.Count - 1)
            {
                _navigationHistory.RemoveRange(_historyIndex + 1, _navigationHistory.Count - (_historyIndex + 1));
            }

            _navigationHistory.Add(token);
            _historyIndex = _navigationHistory.Count - 1;
            UpdateHistoryButtonsState();
        }

        private void OnHistoryBackClicked(object sender, RoutedEventArgs e)
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                NavigateToHistoryToken(_navigationHistory[_historyIndex]);
            }
        }

        private void OnHistoryForwardClicked(object sender, RoutedEventArgs e)
        {
            if (_historyIndex < _navigationHistory.Count - 1)
            {
                _historyIndex++;
                NavigateToHistoryToken(_navigationHistory[_historyIndex]);
            }
        }

        private void NavigateToHistoryToken(string token)
        {
            _isNavigatingHistory = true;
            try
            {
                var parts = token.Split(':');
                if (parts.Length >= 2)
                {
                    string subj = parts.Length > 1 ? parts[1] : "";
                    string? ch = parts.Length > 2 ? parts[2] : null;
                    string? top = parts.Length > 3 ? parts[3] : null;

                    var elements = ScanElementPositions(JsonPreviewBox.Text);
                    var match = elements.FirstOrDefault(e =>
                        (top != null && e.Type == "Topic" && string.Equals(e.SubjectKey, subj, StringComparison.OrdinalIgnoreCase) && string.Equals(e.ChapterKey, ch, StringComparison.OrdinalIgnoreCase) && string.Equals(e.TopicKey, top, StringComparison.OrdinalIgnoreCase)) ||
                        (top == null && ch != null && e.Type == "Chapter" && string.Equals(e.SubjectKey, subj, StringComparison.OrdinalIgnoreCase) && string.Equals(e.ChapterKey, ch, StringComparison.OrdinalIgnoreCase)) ||
                        (top == null && ch == null && e.Type == "Subject" && string.Equals(e.SubjectKey, subj, StringComparison.OrdinalIgnoreCase)));

                    if (match != null)
                    {
                        NavigateToElement(match);
                    }
                }
            }
            finally
            {
                _isNavigatingHistory = false;
                UpdateHistoryButtonsState();
            }
        }

        private void UpdateHistoryButtonsState()
        {
            HistoryBackButton.IsEnabled = _historyIndex > 0;
            HistoryForwardButton.IsEnabled = _historyIndex < _navigationHistory.Count - 1;
        }

        #endregion

        #region Next & Previous Element Navigation

        public static List<DevElementPosition> ScanElementPositions(string text) => DevToolsNavigator.ScanElementPositions(text);

        public static int FindCurrentElementIndex(List<DevElementPosition> elements, string? activeSubj, string? activeCh, string? activeTop, int caret) =>
            DevToolsNavigator.FindCurrentElementIndex(elements, activeSubj, activeCh, activeTop, caret);

        private void OnNextElementClicked(object sender, RoutedEventArgs e)
        {
            var elements = DevToolsNavigator.ScanElementPositions(JsonPreviewBox.Text);
            if (elements.Count == 0) return;

            int caret = JsonPreviewBox.SelectionStart;
            int currentIndex = DevToolsNavigator.FindCurrentElementIndex(elements, _activeSubjectKey, _activeChapterKey, _activeTopicKey, caret);
            int nextIndex = DevToolsNavigator.GetNextIndex(currentIndex, elements.Count);

            NavigateToElement(elements[nextIndex]);
        }

        private void OnPreviousElementClicked(object sender, RoutedEventArgs e)
        {
            var elements = DevToolsNavigator.ScanElementPositions(JsonPreviewBox.Text);
            if (elements.Count == 0) return;

            int caret = JsonPreviewBox.SelectionStart;
            int currentIndex = DevToolsNavigator.FindCurrentElementIndex(elements, _activeSubjectKey, _activeChapterKey, _activeTopicKey, caret);
            int prevIndex = DevToolsNavigator.GetPreviousIndex(currentIndex, elements.Count);

            NavigateToElement(elements[prevIndex]);
        }

        private void NavigateToElement(DevElementPosition elem)
        {
            _activeSubjectKey = elem.SubjectKey;
            _activeChapterKey = elem.ChapterKey;
            _activeTopicKey = elem.TopicKey;

            // Auto-expand in Simplified view
            if (!string.IsNullOrEmpty(elem.SubjectKey))
            {
                _expandedNodes.Add(elem.SubjectKey);
            }
            if (!string.IsNullOrEmpty(elem.SubjectKey) && !string.IsNullOrEmpty(elem.ChapterKey))
            {
                _expandedNodes.Add($"{elem.SubjectKey}/{elem.ChapterKey}");
            }
            if (!string.IsNullOrEmpty(elem.SubjectKey) && !string.IsNullOrEmpty(elem.ChapterKey) && !string.IsNullOrEmpty(elem.TopicKey))
            {
                _expandedNodes.Add($"{elem.SubjectKey}/{elem.ChapterKey}/{elem.TopicKey}");
            }

            if (_previewMode == "Simplified")
            {
                RenderSimplifiedTree();
            }

            JsonPreviewBox.Focus(FocusState.Programmatic);
            JsonPreviewBox.SelectionStart = elem.CharOffset;
            JsonPreviewBox.SelectionLength = 0; // Do NOT select the whole text

            DetectContextFromSelection();
        }

        #endregion

        #region Contextual Panel Actions

        private void OnChapterParentSubjectClicked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_activeSubjectKey))
            {
                var elements = ScanElementPositions(JsonPreviewBox.Text);
                var subjElem = elements.FirstOrDefault(el => el.Type == "Subject" && string.Equals(el.SubjectKey, _activeSubjectKey, StringComparison.OrdinalIgnoreCase));
                if (subjElem != null)
                {
                    NavigateToElement(subjElem);
                }
            }
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
            string newChKey = $"chapter{nextNum}";
            while (subject.Chapters.ContainsKey(newChKey))
            {
                nextNum++;
                newChKey = $"chapter{nextNum}";
            }

            var newChapter = new PresetChapter
            {
                Key = newChKey,
                name = $"Chapter {nextNum} Name",
                Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                {
                    ["topic_1"] = new PresetTopic
                    {
                        Key = "topic_1",
                        name = "Overview",
                        start_page = "1",
                        end_page = "10"
                    }
                }
            };

            subject.Chapters[newChKey] = newChapter;
            _activeChapterKey = newChKey;

            RefreshJsonPreview(targetKeyToSelect: $"\"{newChKey}\"");
            ShowChapterPanel(_activeSubjectKey, newChKey, newChapter);
            ChapterNameBox.Focus(FocusState.Programmatic);
            ChapterNameBox.SelectAll();

            ShowStatus($"Added '{newChKey}' to '{_activeSubjectKey}'.", InfoBarSeverity.Success);
        }

        private void OnUpdateChapterClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeSubjectKey) || !_tree.TryGetValue(_activeSubjectKey, out var subject))
                return;

            if (string.IsNullOrEmpty(_activeChapterKey) || !subject.Chapters.TryGetValue(_activeChapterKey, out var chapter))
                return;

            int chapterNum = double.IsNaN(ChapterNumberInput.Value) ? chapter.ChapterNumber : (int)ChapterNumberInput.Value;
            string targetKey = $"chapter{chapterNum}";

            // If chapter key changed, update dictionary key
            if (!string.Equals(_activeChapterKey, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                subject.Chapters.Remove(_activeChapterKey);
                chapter.Key = targetKey;
                subject.Chapters[targetKey] = chapter;
                _activeChapterKey = targetKey;
            }

            chapter.name = ChapterNameBox.Text.Trim();
            RefreshJsonPreview(targetKeyToSelect: $"\"{_activeChapterKey}\"");
            ShowStatus($"Updated chapter '{chapter.name}'.", InfoBarSeverity.Success);
        }

        private void OnAddTopicClicked(object sender, RoutedEventArgs e)
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

            RefreshJsonPreview(targetKeyToSelect: $"\"{newTopKey}\"");
            ShowTopicPanel(chapter.Key, newTopKey, newTopic);
            TopicNameBox.Focus(FocusState.Programmatic);
            TopicNameBox.SelectAll();

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
            string newKey = $"Subject{num}";
            var newSubject = new PresetSubject
            {
                Key = newKey,
                short_name = $"Subject {num}",
                full_name = $"Full Subject {num} Name",
                edition = "1st",
                Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                {
                    ["chapter1"] = new PresetChapter
                    {
                        Key = "chapter1",
                        name = "Introduction",
                        Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["topic_1"] = new PresetTopic { Key = "topic_1", name = "Overview", start_page = "1", end_page = "10" }
                        }
                    }
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

            if (_previewMode == "Simplified")
            {
                RenderSimplifiedTree();
            }

            var elements = ScanElementPositions(newJson);
            DevElementPosition? target = null;

            if (!string.IsNullOrEmpty(_activeTopicKey) && !string.IsNullOrEmpty(_activeChapterKey) && !string.IsNullOrEmpty(_activeSubjectKey))
            {
                target = elements.FirstOrDefault(el => el.Type == "Topic" &&
                                                       string.Equals(el.SubjectKey, _activeSubjectKey, StringComparison.OrdinalIgnoreCase) &&
                                                       string.Equals(el.ChapterKey, _activeChapterKey, StringComparison.OrdinalIgnoreCase) &&
                                                       string.Equals(el.TopicKey, _activeTopicKey, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrEmpty(_activeChapterKey) && !string.IsNullOrEmpty(_activeSubjectKey))
            {
                target = elements.FirstOrDefault(el => el.Type == "Chapter" &&
                                                       string.Equals(el.SubjectKey, _activeSubjectKey, StringComparison.OrdinalIgnoreCase) &&
                                                       string.Equals(el.ChapterKey, _activeChapterKey, StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrEmpty(_activeSubjectKey))
            {
                target = elements.FirstOrDefault(el => el.Type == "Subject" &&
                                                       string.Equals(el.SubjectKey, _activeSubjectKey, StringComparison.OrdinalIgnoreCase));
            }

            if (target != null)
            {
                NavigateToElement(target);
            }
            else if (!string.IsNullOrEmpty(targetKeyToSelect))
            {
                int index = newJson.IndexOf(targetKeyToSelect, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    JsonPreviewBox.Focus(FocusState.Programmatic);
                    JsonPreviewBox.SelectionStart = index;
                    JsonPreviewBox.SelectionLength = 0;
                    DetectContextFromSelection();
                }
            }
        }

        #endregion

        #region Simplified Tree & Element Deletion

        private void SwitchPreviewMode(string mode)
        {
            _previewMode = mode;
            if (mode == "Simplified")
            {
                SimplifiedModeButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                JsonModeButton.Style = (Style)Application.Current.Resources["SubtleButtonStyle"];

                SimplifiedTreeScrollViewer.Visibility = Visibility.Visible;
                ActiveLineIndicatorBorder.Visibility = Visibility.Collapsed;
                JsonPreviewBox.Visibility = Visibility.Collapsed;

                SyncTreeFromPreviewText();
                RenderSimplifiedTree();
            }
            else
            {
                SimplifiedModeButton.Style = (Style)Application.Current.Resources["SubtleButtonStyle"];
                JsonModeButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];

                SimplifiedTreeScrollViewer.Visibility = Visibility.Collapsed;
                ActiveLineIndicatorBorder.Visibility = Visibility.Visible;
                JsonPreviewBox.Visibility = Visibility.Visible;

                SetJsonText(_generator.GenerateJson(_tree));
                DetectContextFromSelection();
            }
        }

        private void RenderSimplifiedTree()
        {
            SimplifiedTreeContainer.Children.Clear();

            if (_tree == null || _tree.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "Preset tag database is empty. Click 'Start from Scratch' or 'Add New Subject'.",
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    Margin = new Thickness(12)
                };
                SimplifiedTreeContainer.Children.Add(emptyText);
                return;
            }

            int sIdx = 1;
            foreach (var (subjKey, subj) in _tree)
            {
                string subjDisplayId = $"Subject{sIdx}";
                string subjNodeKey = subjKey;
                bool isSubjExpanded = _expandedNodes.Contains(subjNodeKey);
                bool isSubjSelected = string.Equals(_activeSubjectKey, subjKey, StringComparison.OrdinalIgnoreCase)
                                   && string.IsNullOrEmpty(_activeChapterKey)
                                   && string.IsNullOrEmpty(_activeTopicKey);

                var subjItem = CreateLevelNode(
                    displayId: subjDisplayId,
                    levelName: subj.short_name,
                    levelType: "Subject",
                    indent: 0,
                    isExpanded: isSubjExpanded,
                    isSelected: isSubjSelected,
                    onClick: () =>
                    {
                        ToggleNodeExpansion(subjNodeKey);
                        SelectSubject(subjKey);
                    },
                    onAddChild: () => AddChapterToSubject(subjKey),
                    onDelete: () => DeleteSubject(subjKey)
                );
                SimplifiedTreeContainer.Children.Add(subjItem);

                if (isSubjExpanded)
                {
                    // Unhighlighted properties indented 24px (simplified variable names)
                    if (!string.IsNullOrWhiteSpace(subj.short_name))
                        SimplifiedTreeContainer.Children.Add(CreatePropertyNode($"short name: {subj.short_name}", 24));
                    if (!string.IsNullOrWhiteSpace(subj.full_name))
                        SimplifiedTreeContainer.Children.Add(CreatePropertyNode($"full name: {subj.full_name}", 24));
                    if (!string.IsNullOrWhiteSpace(subj.edition))
                        SimplifiedTreeContainer.Children.Add(CreatePropertyNode($"edition: {subj.edition}", 24));

                    if (subj.Chapters != null)
                    {
                        int cIdx = 1;
                        foreach (var (chKey, ch) in subj.Chapters)
                        {
                            string chDisplayId = $"Chapter{cIdx}";
                            string chNodeKey = $"{subjKey}/{chKey}";
                            bool isChExpanded = _expandedNodes.Contains(chNodeKey);
                            bool isChSelected = string.Equals(_activeSubjectKey, subjKey, StringComparison.OrdinalIgnoreCase)
                                             && string.Equals(_activeChapterKey, chKey, StringComparison.OrdinalIgnoreCase)
                                             && string.IsNullOrEmpty(_activeTopicKey);

                            var chItem = CreateLevelNode(
                                displayId: chDisplayId,
                                levelName: ch.name,
                                levelType: "Chapter",
                                indent: 24,
                                isExpanded: isChExpanded,
                                isSelected: isChSelected,
                                onClick: () =>
                                {
                                    ToggleNodeExpansion(chNodeKey);
                                    SelectChapter(subjKey, chKey);
                                },
                                onAddChild: () => AddTopicToChapter(subjKey, chKey),
                                onDelete: () => DeleteChapter(subjKey, chKey)
                            );
                            SimplifiedTreeContainer.Children.Add(chItem);

                            if (isChExpanded)
                            {
                                // Unhighlighted properties indented 48px (simplified variable names)
                                if (!string.IsNullOrWhiteSpace(ch.name))
                                    SimplifiedTreeContainer.Children.Add(CreatePropertyNode($"name: {ch.name}", 48));
                                if (ch.ChapterNumber > 0)
                                    SimplifiedTreeContainer.Children.Add(CreatePropertyNode($"number: {ch.ChapterNumber}", 48));

                                if (ch.Topics != null)
                                {
                                    int tIdx = 1;
                                    foreach (var (topKey, top) in ch.Topics)
                                    {
                                        string topDisplayId = $"Topic{tIdx}";
                                        string topNodeKey = $"{subjKey}/{chKey}/{topKey}";
                                        bool isTopExpanded = _expandedNodes.Contains(topNodeKey);
                                        bool isTopSelected = string.Equals(_activeSubjectKey, subjKey, StringComparison.OrdinalIgnoreCase)
                                                          && string.Equals(_activeChapterKey, chKey, StringComparison.OrdinalIgnoreCase)
                                                          && string.Equals(_activeTopicKey, topKey, StringComparison.OrdinalIgnoreCase);

                                        var topItem = CreateLevelNode(
                                            displayId: topDisplayId,
                                            levelName: top.name,
                                            levelType: "Topic",
                                            indent: 48,
                                            isExpanded: isTopExpanded,
                                            isSelected: isTopSelected,
                                            onClick: () =>
                                            {
                                                ToggleNodeExpansion(topNodeKey);
                                                SelectTopic(subjKey, chKey, topKey);
                                            },
                                            onAddChild: null,
                                            onDelete: () => DeleteTopic(subjKey, chKey, topKey)
                                        );
                                        SimplifiedTreeContainer.Children.Add(topItem);

                                        if (isTopExpanded)
                                        {
                                            // Unhighlighted properties indented 72px (simplified variable names: name, start, end)
                                            if (!string.IsNullOrWhiteSpace(top.name))
                                                SimplifiedTreeContainer.Children.Add(CreatePropertyNode($"name: {top.name}", 72));
                                            if (!string.IsNullOrWhiteSpace(top.start_page))
                                                SimplifiedTreeContainer.Children.Add(CreatePropertyNode($"start: {top.start_page}", 72));
                                            if (!string.IsNullOrWhiteSpace(top.end_page))
                                                SimplifiedTreeContainer.Children.Add(CreatePropertyNode($"end: {top.end_page}", 72));
                                        }
                                        tIdx++;
                                    }
                                }
                            }
                            cIdx++;
                        }
                    }
                }
                sIdx++;
            }
        }

        private FrameworkElement CreateLevelNode(
            string displayId,
            string? levelName,
            string levelType,
            int indent,
            bool isExpanded,
            bool isSelected,
            Action onClick,
            Action? onAddChild,
            Action onDelete)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(indent, 2, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                BorderThickness = new Thickness(1),
                IsHitTestVisible = true
            };

            if (isSelected)
            {
                if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accent) && accent is Brush ab)
                {
                    border.Background = ab;
                    border.BorderBrush = ab;
                }
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("LayerFillColorDefaultBrush", out object? layer) && layer is Brush lb)
                {
                    border.Background = lb;
                }
                if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? stroke) && stroke is Brush sb)
                {
                    border.BorderBrush = sb;
                }
            }

            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            var chevron = new FontIcon
            {
                Glyph = isExpanded ? "\uE70D" : "\uE76C",
                FontSize = 10,
                Foreground = isSelected ? new SolidColorBrush(Microsoft.UI.Colors.White)
                                        : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            sp.Children.Add(chevron);

            var idBlock = new TextBlock
            {
                Text = displayId,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12,
                Foreground = isSelected ? new SolidColorBrush(Microsoft.UI.Colors.White)
                                        : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            };
            sp.Children.Add(idBlock);

            if (!string.IsNullOrWhiteSpace(levelName))
            {
                var nameBlock = new TextBlock
                {
                    Text = $"({levelName})",
                    FontSize = 11,
                    Foreground = isSelected ? new SolidColorBrush(ColorHelper.FromArgb(220, 255, 255, 255))
                                            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                };
                sp.Children.Add(nameBlock);
            }

            border.Child = sp;

            border.Tapped += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };

            var flyout = new MenuFlyout();
            if (onAddChild != null)
            {
                string addText = levelType == "Subject" ? "Add New Chapter" : "Add New Topic";
                var addItem = new MenuFlyoutItem { Text = addText, Icon = new FontIcon { Glyph = "\uE710" } };
                addItem.Click += (s, e) => onAddChild();
                flyout.Items.Add(addItem);
                flyout.Items.Add(new MenuFlyoutSeparator());
            }

            var deleteItem = new MenuFlyoutItem
            {
                Text = $"Delete {levelType}",
                Icon = new FontIcon { Glyph = "\uE74D" }
            };
            deleteItem.Click += (s, e) => onDelete();
            flyout.Items.Add(deleteItem);

            border.ContextFlyout = flyout;

            return border;
        }

        private FrameworkElement CreatePropertyNode(string propertyText, int indent)
        {
            return new TextBlock
            {
                Text = $"- {propertyText}",
                FontSize = 11,
                Margin = new Thickness(indent, 1, 0, 1),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New")
            };
        }

        private void ToggleNodeExpansion(string nodeKey)
        {
            if (_expandedNodes.Contains(nodeKey))
                _expandedNodes.Remove(nodeKey);
            else
                _expandedNodes.Add(nodeKey);

            RenderSimplifiedTree();
        }

        private void SelectSubject(string subjectKey)
        {
            _activeSubjectKey = subjectKey;
            _activeChapterKey = null;
            _activeTopicKey = null;

            if (_tree.TryGetValue(subjectKey, out var subj))
            {
                ShowSubjectPanel(subjectKey, subj);
                RecordHistoryToken($"subject:{subjectKey}");
            }
            RenderSimplifiedTree();
        }

        private void SelectChapter(string subjectKey, string chapterKey)
        {
            _activeSubjectKey = subjectKey;
            _activeChapterKey = chapterKey;
            _activeTopicKey = null;

            if (_tree.TryGetValue(subjectKey, out var subj) && subj.Chapters != null && subj.Chapters.TryGetValue(chapterKey, out var ch))
            {
                ShowChapterPanel(subjectKey, chapterKey, ch);
                RecordHistoryToken($"chapter:{subjectKey}:{chapterKey}");
            }
            RenderSimplifiedTree();
        }

        private void SelectTopic(string subjectKey, string chapterKey, string topicKey)
        {
            _activeSubjectKey = subjectKey;
            _activeChapterKey = chapterKey;
            _activeTopicKey = topicKey;

            if (_tree.TryGetValue(subjectKey, out var subj) && subj.Chapters != null && subj.Chapters.TryGetValue(chapterKey, out var ch) && ch.Topics != null && ch.Topics.TryGetValue(topicKey, out var top))
            {
                ShowTopicPanel(chapterKey, topicKey, top);
                RecordHistoryToken($"topic:{subjectKey}:{chapterKey}:{topicKey}");
            }
            RenderSimplifiedTree();
        }

        private void AddChapterToSubject(string subjectKey)
        {
            _activeSubjectKey = subjectKey;
            _expandedNodes.Add(subjectKey);
            OnAddChapterClicked(null!, null!);
        }

        private void AddTopicToChapter(string subjectKey, string chapterKey)
        {
            _activeSubjectKey = subjectKey;
            _activeChapterKey = chapterKey;
            _expandedNodes.Add(subjectKey);
            _expandedNodes.Add($"{subjectKey}/{chapterKey}");
            OnAddTopicClicked(null!, null!);
        }

        private void DeleteSubject(string? subjectKey)
        {
            if (string.IsNullOrEmpty(subjectKey) || !_tree.ContainsKey(subjectKey)) return;

            _tree.Remove(subjectKey);
            _expandedNodes.Remove(subjectKey);

            _activeSubjectKey = _tree.Keys.FirstOrDefault();
            _activeChapterKey = null;
            _activeTopicKey = null;

            RefreshJsonPreview();
            if (_activeSubjectKey != null && _tree.TryGetValue(_activeSubjectKey, out var nextSubj))
            {
                ShowSubjectPanel(_activeSubjectKey, nextSubj);
            }
            else
            {
                ShowGeneralPanel("Root Overview");
            }
            ShowStatus("Subject deleted.", InfoBarSeverity.Informational);
        }

        private void DeleteChapter(string? subjectKey, string? chapterKey)
        {
            if (string.IsNullOrEmpty(subjectKey) || string.IsNullOrEmpty(chapterKey)) return;
            if (!_tree.TryGetValue(subjectKey, out var subject) || subject.Chapters == null) return;

            subject.Chapters.Remove(chapterKey);
            _expandedNodes.Remove($"{subjectKey}/{chapterKey}");

            _activeSubjectKey = subjectKey;
            _activeChapterKey = null;
            _activeTopicKey = null;

            RefreshJsonPreview();
            ShowSubjectPanel(subjectKey, subject);
            ShowStatus("Chapter deleted.", InfoBarSeverity.Informational);
        }

        private void DeleteTopic(string? subjectKey, string? chapterKey, string? topicKey)
        {
            if (string.IsNullOrEmpty(subjectKey) || string.IsNullOrEmpty(chapterKey) || string.IsNullOrEmpty(topicKey)) return;
            if (!_tree.TryGetValue(subjectKey, out var subject) || subject.Chapters == null) return;
            if (!subject.Chapters.TryGetValue(chapterKey, out var chapter) || chapter.Topics == null) return;

            chapter.Topics.Remove(topicKey);
            _expandedNodes.Remove($"{subjectKey}/{chapterKey}/{topicKey}");

            _activeSubjectKey = subjectKey;
            _activeChapterKey = chapterKey;
            _activeTopicKey = null;

            RefreshJsonPreview();
            ShowChapterPanel(subjectKey, chapterKey, chapter);
            ShowStatus("Topic deleted.", InfoBarSeverity.Informational);
        }

        #endregion

        #region File & Workspace Actions

        private void OnStartFromScratchClicked(object sender, RoutedEventArgs e)
        {
            _tree.Clear();
            var dummySubj = new PresetSubject
            {
                Key = "Subject1",
                short_name = "Dummy Subject",
                full_name = "Dummy Subject Book",
                edition = "1st",
                Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                {
                    ["chapter1"] = new PresetChapter
                    {
                        Key = "chapter1",
                        name = "Dummy Chapter 1",
                        Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["topic_1"] = new PresetTopic
                            {
                                Key = "topic_1",
                                name = "Dummy Topic 1",
                                start_page = "1",
                                end_page = "10"
                            }
                        }
                    }
                }
            };

            _tree["Subject1"] = dummySubj;
            _activeSubjectKey = "Subject1";
            _activeChapterKey = "chapter1";
            _activeTopicKey = "topic_1";
            _activeFilePath = null;
            ActiveFileBadgeText.Text = "(Scratch Preset)";

            string json = _generator.GenerateJson(_tree);
            SetJsonText(json);
            RefreshJsonPreview();
            ShowStatus("Started afresh with dummy subject, chapter, and topic.", InfoBarSeverity.Success);
        }

        private async void OnOpenJsonFileClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".json");

                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    LoadJsonFile(file.Path);
                }
            }
            catch (Exception)
            {
                // Fallback manual input
                await ShowManualPathDialog("Open JSON File", false, path => LoadJsonFile(path));
            }
        }

        private void LoadJsonFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                ShowStatus($"File not found: {filePath}", InfoBarSeverity.Error);
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                _tree = _generator.Parse(json);
                _activeFilePath = filePath;
                ActiveFileBadgeText.Text = Path.GetFileName(filePath);
                SetJsonText(json);
                DetectContextFromSelection();
                ShowStatus($"Loaded JSON file: {Path.GetFileName(filePath)}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load file: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async void OnSaveJsonClicked(object sender, RoutedEventArgs e)
        {
            SyncTreeFromPreviewText();

            if (!string.IsNullOrEmpty(_activeFilePath))
            {
                try
                {
                    string json = !string.IsNullOrWhiteSpace(JsonPreviewBox.Text) ? JsonPreviewBox.Text : _generator.GenerateJson(_tree);
                    File.WriteAllText(_activeFilePath, json);
                    ShowStatus($"Saved JSON to: {Path.GetFileName(_activeFilePath)}", InfoBarSeverity.Success);
                    return;
                }
                catch (Exception ex)
                {
                    ShowStatus($"Save error: {ex.Message}", InfoBarSeverity.Error);
                }
            }

            // If no active file, prompt where to save
            try
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("JSON File", new List<string>() { ".json" });
                picker.SuggestedFileName = "presets.json";

                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    _activeFilePath = file.Path;
                    ActiveFileBadgeText.Text = Path.GetFileName(file.Path);
                    string json = _generator.GenerateJson(_tree);
                    File.WriteAllText(_activeFilePath, json);
                    ShowStatus($"Saved JSON to: {_activeFilePath}", InfoBarSeverity.Success);
                    RefreshWorkspaceFiles();
                }
            }
            catch (Exception)
            {
                await ShowManualPathDialog("Save JSON File", false, path =>
                {
                    _activeFilePath = path;
                    File.WriteAllText(path, _generator.GenerateJson(_tree));
                    ActiveFileBadgeText.Text = Path.GetFileName(path);
                    ShowStatus($"Saved JSON to: {path}", InfoBarSeverity.Success);
                });
            }
        }

        private async void OnNewWorkspaceClicked(object sender, RoutedEventArgs e)
        {
            await PromptForDirectoryAndOpenWorkspace("Create / Select New Workspace Directory");
        }

        private async void OnOpenWorkspaceClicked(object sender, RoutedEventArgs e)
        {
            await PromptForDirectoryAndOpenWorkspace("Open Workspace Directory");
        }

        private async System.Threading.Tasks.Task PromptForDirectoryAndOpenWorkspace(string title)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hwnd);

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    OpenWorkspace(folder.Path);
                }
            }
            catch (Exception)
            {
                await ShowManualPathDialog(title, true, dir => OpenWorkspace(dir));
            }
        }

        private void OpenWorkspace(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            _activeWorkspaceDir = directoryPath;
            WorkspacePathText.Text = directoryPath;
            WorkspaceCard.Visibility = Visibility.Visible;
            RefreshWorkspaceFiles();
            ShowStatus($"Workspace opened: {directoryPath}", InfoBarSeverity.Success);
        }

        private void CloseWorkspace()
        {
            _activeWorkspaceDir = null;
            WorkspaceCard.Visibility = Visibility.Collapsed;
        }

        private void RefreshWorkspaceFiles()
        {
            if (string.IsNullOrEmpty(_activeWorkspaceDir) || !Directory.Exists(_activeWorkspaceDir))
                return;

            var files = Directory.GetFiles(_activeWorkspaceDir, "*.json")
                                 .Select(Path.GetFileName)
                                 .ToList();

            WorkspaceFilesListView.ItemsSource = files;
        }

        private void OnWorkspaceFileClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string filename && !string.IsNullOrEmpty(_activeWorkspaceDir))
            {
                string fullPath = Path.Combine(_activeWorkspaceDir, filename);
                LoadJsonFile(fullPath);
            }
        }

        private async void OnNewJsonFileClicked(object sender, RoutedEventArgs e)
        {
            var textBox = new TextBox { PlaceholderText = "chapter_topics.json", Width = 300 };
            var dialog = new ContentDialog
            {
                Title = "New JSON File",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Enter file name:" },
                        textBox
                    }
                },
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string name = textBox.Text.Trim();
                if (string.IsNullOrEmpty(name)) name = "new_presets.json";
                if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name += ".json";

                string targetDir = _activeWorkspaceDir ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string fullPath = Path.Combine(targetDir, name);

                string starter = _generator.CreateStarterPresetJson();
                File.WriteAllText(fullPath, starter);
                RefreshWorkspaceFiles();
                LoadJsonFile(fullPath);
            }
        }

        private async void OnNewFolderClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeWorkspaceDir)) return;

            var textBox = new TextBox { PlaceholderText = "SubFolder", Width = 300 };
            var dialog = new ContentDialog
            {
                Title = "New Folder",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Enter folder name:" },
                        textBox
                    }
                },
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string name = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    string path = Path.Combine(_activeWorkspaceDir, name);
                    Directory.CreateDirectory(path);
                    RefreshWorkspaceFiles();
                    ShowStatus($"Created folder: {name}", InfoBarSeverity.Success);
                }
            }
        }

        private async System.Threading.Tasks.Task ShowManualPathDialog(string title, bool isFolder, Action<string> onConfirmed)
        {
            var textBox = new TextBox
            {
                PlaceholderText = isFolder ? @"C:\PresetsWorkspace" : @"C:\Presets\file.json",
                Width = 400
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = isFolder ? "Enter directory path:" : "Enter full file path:" },
                        textBox
                    }
                },
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string path = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    onConfirmed(path);
                }
            }
        }

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            NotificationCard.Show(message, severity);
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Models;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class SessionDetailsPage : Page
    {
        private List<PresetSearchResult> _matchingResults = new();
        private int _selectedSessionType = 0; // 0 = self-session, 1 = class-session
        private string _sessionMode = "Curated"; // "Curated" or "Custom"

        public SessionDetailsPage()
        {
            this.InitializeComponent();

            SelfSessionButton.Click += (s, e) => SetSessionType(0);
            ClassSessionButton.Click += (s, e) => SetSessionType(1);

            BackButton.Click += (s, e) => NavigateBack();
            CancelButton.Click += (s, e) => NavigateBack();
            NextButton.Click += OnStartClicked;
            SearchPresetButton.Click += OnSearchPresetClicked;

            OpenDevToolsButton.Click += (s, e) =>
            {
                var devWindow = DevToolsWindow.GetOrCreateInstance();
                devWindow.Activate();
            };

            SubjectBox.TextChanged += (s, e) => OnSubjectTextChanged();
            SubjectRecommendationButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(SubjectRecommendationText.Text))
                {
                    SubjectBox.Text = SubjectRecommendationText.Text;
                    SubjectBox.SelectionStart = SubjectBox.Text.Length;
                }
            };

            SetSessionType(0);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string mode && mode.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                _sessionMode = "Custom";
            }
            else
            {
                _sessionMode = "Curated";
            }

            ApplySessionModeUI();
        }

        private void ApplySessionModeUI()
        {
            if (_sessionMode == "Curated")
            {
                HeaderModeBadgeText.Text = "Curated";
                if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accent) && accent is Brush b)
                {
                    HeaderModeBadgeBorder.Background = b;
                }
                HeaderModeBadgeText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                HeaderModeBadgeBorder.BorderThickness = new Thickness(0);

                PageRangeCard.Opacity = 1.0;
                PageRangeHintText.Text = "(Enabled for Curated Subjects)";
                CustomTagInfoCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeaderModeBadgeText.Text = "Custom";
                if (Application.Current.Resources.TryGetValue("LayerFillColorDefaultBrush", out object? layer) && layer is Brush lb)
                {
                    HeaderModeBadgeBorder.Background = lb;
                }
                if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? stroke) && stroke is Brush sb)
                {
                    HeaderModeBadgeBorder.BorderBrush = sb;
                }
                HeaderModeBadgeBorder.BorderThickness = new Thickness(1);
                if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? txt) && txt is Brush tb)
                {
                    HeaderModeBadgeText.Foreground = tb;
                }

                PageRangeCard.Opacity = 0.55;
                StartPageBox.IsEnabled = false;
                EndPageBox.IsEnabled = false;
                SearchPresetButton.IsEnabled = false;
                PageRangeHintText.Text = "(Disabled in Custom Mode)";
                CustomTagInfoCard.Visibility = Visibility.Visible;
                CustomInfoTitleText.Text = "Custom Tag Mode Active";
                CustomInfoSubtitleText.Text = "Custom tag mode allows any subject name, but chapter and topic fields cannot be filled directly. Please update the App's database with custom tag JSONs via DevTools to assign preset chapters and topics.";
            }

            OnSubjectTextChanged();
        }

        private void SetSessionType(int sessionType)
        {
            _selectedSessionType = sessionType == 1 ? 1 : 0;

            if (_selectedSessionType == 0)
            {
                SelfSessionButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                ClassSessionButton.Style = (Style)Application.Current.Resources["SubtleButtonStyle"];
                NewSessionHeaderSubtext.Text = "Sessions created for self-study";
            }
            else
            {
                SelfSessionButton.Style = (Style)Application.Current.Resources["SubtleButtonStyle"];
                ClassSessionButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                NewSessionHeaderSubtext.Text = "Sessions based on notes from classrooms/lectures.";
            }
        }

        private void OnSubjectTextChanged()
        {
            string typedSubject = SubjectBox.Text.Trim();
            bool isCurated = CustomTagService.Instance.IsCuratedSubject(typedSubject);

            // In Curated Mode: page range is enabled only if typed subject is in curated database
            if (_sessionMode == "Curated")
            {
                StartPageBox.IsEnabled = isCurated;
                EndPageBox.IsEnabled = isCurated;
                SearchPresetButton.IsEnabled = isCurated;

                if (!string.IsNullOrWhiteSpace(typedSubject))
                {
                    SubjectBadgeBorder.Visibility = Visibility.Visible;
                    if (isCurated)
                    {
                        SubjectStatusBadge.Text = "Curated Preset";
                        if (Application.Current.Resources.TryGetValue("AccentTextFillColorPrimaryBrush", out object? accent) && accent is Brush b)
                        {
                            SubjectStatusBadge.Foreground = b;
                        }
                    }
                    else
                    {
                        SubjectStatusBadge.Text = "Not in Presets";
                        if (Application.Current.Resources.TryGetValue("SystemFillColorCautionBrush", out object? caution) && caution is Brush cb)
                        {
                            SubjectStatusBadge.Foreground = cb;
                        }
                    }
                }
                else
                {
                    SubjectBadgeBorder.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // In Custom Mode: page range is disabled
                StartPageBox.IsEnabled = false;
                EndPageBox.IsEnabled = false;
                SearchPresetButton.IsEnabled = false;

                if (!string.IsNullOrWhiteSpace(typedSubject))
                {
                    SubjectBadgeBorder.Visibility = Visibility.Visible;
                    SubjectStatusBadge.Text = "Custom Subject";
                    if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? sec) && sec is Brush sb)
                    {
                        SubjectStatusBadge.Foreground = sb;
                    }
                }
                else
                {
                    SubjectBadgeBorder.Visibility = Visibility.Collapsed;
                }
            }

            // Subject Recommendation (Closest match from curated presets)
            UpdateSubjectRecommendation(typedSubject);
        }

        private void UpdateSubjectRecommendation(string typedSubject)
        {
            if (string.IsNullOrWhiteSpace(typedSubject))
            {
                SubjectRecommendationPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var tree = PresetTagDatabase.Instance.GetTree();
            if (tree == null || tree.Count == 0)
            {
                SubjectRecommendationPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Gather all candidate names
            var candidates = new List<string>();
            foreach (var kvp in tree)
            {
                if (!candidates.Contains(kvp.Key)) candidates.Add(kvp.Key);
                if (!string.IsNullOrWhiteSpace(kvp.Value.short_name) && !candidates.Contains(kvp.Value.short_name))
                    candidates.Add(kvp.Value.short_name);
            }

            // Exact match already?
            if (candidates.Any(c => string.Equals(c, typedSubject, StringComparison.OrdinalIgnoreCase)))
            {
                SubjectRecommendationPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Find closest match (Prefix match, contains match, or best distance)
            string? closest = candidates.FirstOrDefault(c => c.StartsWith(typedSubject, StringComparison.OrdinalIgnoreCase))
                           ?? candidates.FirstOrDefault(c => c.IndexOf(typedSubject, StringComparison.OrdinalIgnoreCase) >= 0);

            if (closest == null)
            {
                // Distance based match
                int bestDist = int.MaxValue;
                foreach (var c in candidates)
                {
                    int d = ComputeLevenshteinDistance(typedSubject.ToLowerInvariant(), c.ToLowerInvariant());
                    if (d < bestDist && d <= Math.Max(3, typedSubject.Length))
                    {
                        bestDist = d;
                        closest = c;
                    }
                }
            }

            if (!string.IsNullOrEmpty(closest))
            {
                SubjectRecommendationText.Text = closest;
                SubjectRecommendationPanel.Visibility = Visibility.Visible;
            }
            else
            {
                SubjectRecommendationPanel.Visibility = Visibility.Collapsed;
            }
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
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
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private void OnSearchPresetClicked(object sender, RoutedEventArgs e)
        {
            string subject = SubjectBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(subject))
            {
                NotificationCard.Show("Please enter a Subject Name first.", InfoBarSeverity.Warning, "Subject Required");
                SubjectBox.Focus(FocusState.Programmatic);
                return;
            }

            if (!CustomTagService.Instance.IsCuratedSubject(subject))
            {
                NotificationCard.Show("Page Range search is only available for Curated Subjects. Please enter a valid curated subject name.", InfoBarSeverity.Error, "Curated Subject Required");
                return;
            }

            int? startPage = double.IsNaN(StartPageBox.Value) ? null : (int)StartPageBox.Value;
            int? endPage = double.IsNaN(EndPageBox.Value) ? null : (int)EndPageBox.Value;

            if (startPage == null && endPage == null)
            {
                NotificationCard.Show("Please enter at least a Start Page or End Page to search presets.", InfoBarSeverity.Warning, "Page Number Required");
                StartPageBox.Focus(FocusState.Programmatic);
                return;
            }

            int sStart = startPage ?? endPage ?? 0;
            int sEnd = endPage ?? startPage ?? 0;
            var allResults = PresetTagDatabase.Instance.Search(sStart, sEnd);
            _matchingResults = allResults.Where(r =>
                string.Equals(r.SubjectKey, subject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.SubjectShortName, subject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.BookFullName, subject, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (_matchingResults.Count == 0)
            {
                ChapterLabelText.Text = "None matched";
                TopicLabelText.Text = "None matched";
                MatchedSummaryBadgeText.Visibility = Visibility.Collapsed;
                NotificationCard.Show($"No curated preset topics found for {subject} within pages {startPage}-{endPage}.", InfoBarSeverity.Warning, "No Matches Found");
                return;
            }

            // Display all matched chapters and topics
            var distinctChapters = _matchingResults.Select(r => r.ChapterNumber > 0 ? $"Ch.{r.ChapterNumber}: {r.ChapterName}" : r.ChapterName)
                                                   .Distinct()
                                                   .ToList();
            var distinctTopics = _matchingResults.Select(r => r.TopicName)
                                                 .Distinct()
                                                 .ToList();

            ChapterLabelText.Text = string.Join(", ", distinctChapters);
            TopicLabelText.Text = string.Join(", ", distinctTopics);

            MatchedSummaryBadgeText.Text = $"Matched {_matchingResults.Count} preset topic(s) across {distinctChapters.Count} chapter(s)";
            MatchedSummaryBadgeText.Visibility = Visibility.Visible;

            NotificationCard.Show($"Found {_matchingResults.Count} preset configuration(s). All matched chapters and topics are assigned.", InfoBarSeverity.Success, "Preset Matched");
        }

        private void OnStartClicked(object sender, RoutedEventArgs e)
        {
            string sessionName = SessionNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(sessionName))
            {
                NotificationCard.Show("Please enter a Session Name.", InfoBarSeverity.Error, "Session Name Required");
                SessionNameBox.Focus(FocusState.Programmatic);
                return;
            }

            string subject = SubjectBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(subject))
            {
                NotificationCard.Show("Please enter a Subject Name.", InfoBarSeverity.Error, "Subject Required");
                SubjectBox.Focus(FocusState.Programmatic);
                return;
            }

            // Mode A: Curated Tags Mode
            if (_sessionMode == "Curated")
            {
                // Curated tags mode will not allow the user to proceed if the Subject field entered does not match any of the curated subjects
                if (!CustomTagService.Instance.IsCuratedSubject(subject))
                {
                    NotificationCard.Show($"In Curated Mode, Subject '{subject}' must match a curated preset subject. Please choose a curated subject or switch to Custom mode.", InfoBarSeverity.Error, "Invalid Curated Subject");
                    SubjectBox.Focus(FocusState.Programmatic);
                    return;
                }

                var tags = new List<Tag>();
                if (_matchingResults != null && _matchingResults.Count > 0)
                {
                    int idCounter = 1;
                    foreach (var res in _matchingResults)
                    {
                        tags.Add(new Tag
                        {
                            id = idCounter++,
                            subject_name = string.IsNullOrWhiteSpace(res.SubjectShortName) ? subject : res.SubjectShortName,
                            chapter_number = res.ChapterNumber,
                            chapter_name = res.ChapterName,
                            topic_name = res.TopicName,
                            book_name = res.BookFullName,
                            page_range = res.PageRangeString,
                            session_type = _selectedSessionType
                        });
                    }
                }
                else
                {
                    // Fallback to primary subject tag if page range was not specifically queried
                    tags.Add(new Tag
                    {
                        id = 1,
                        subject_name = subject,
                        chapter_number = 1,
                        chapter_name = "General",
                        topic_name = "General",
                        session_type = _selectedSessionType
                    });
                }

                var session = new BasicSessionBundle(null, isCurated: true, nextReviewDate: DateTime.UtcNow.AddDays(3), sessionName: sessionName, tag: tags[0], tags: tags);
                SessionService.Instance.SaveSession(session);
                NotificationCard.Dismiss();
                Frame.Navigate(typeof(EditCanvasPage), session);
                return;
            }

            // Mode B: Custom Tag Mode
            // Custom tag mode has no subject restriction, but does not allow filling chapter and topic fields
            var customTag = new Tag
            {
                id = 1,
                subject_name = subject,
                chapter_number = 0,
                chapter_name = "Custom",
                topic_name = "Custom",
                session_type = _selectedSessionType
            };

            var customSession = new BasicSessionBundle(null, isCurated: false, nextReviewDate: DateTime.UtcNow.AddDays(3), sessionName: sessionName, tag: customTag, tags: new[] { customTag });
            SessionService.Instance.SaveSession(customSession);
            NotificationCard.Dismiss();
            Frame.Navigate(typeof(EditCanvasPage), customSession);
        }

        private void NavigateBack()
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(DashboardPage));
        }
    }
}

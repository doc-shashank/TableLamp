using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        // Custom tag fields when subject is not a curated preset
        private int _customChapterNumber = 1;
        private string? _customChapterName;
        private string? _customTopicName;
        private string? _customPageRange;

        public SessionDetailsPage()
        {
            this.InitializeComponent();

            SelfSessionButton.Click += (s, e) => SetSessionType(0);
            ClassSessionButton.Click += (s, e) => SetSessionType(1);

            BackButton.Click += (s, e) => NavigateBack();
            CancelButton.Click += (s, e) => NavigateBack();
            NextButton.Click += OnNextClicked;
            SearchPresetButton.Click += OnSearchPresetClicked;
            OpenCustomTagPopupButton.Click += async (s, e) => await OpenCustomTagPopupAsync();

            SubjectBox.TextChanged += (s, e) => OnSubjectTextChanged();

            // Initial subject validation state
            OnSubjectTextChanged();
        }

        private void SetSessionType(int sessionType)
        {
            _selectedSessionType = sessionType == 1 ? 1 : 0;

            if (_selectedSessionType == 0)
            {
                SelfSessionButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                ClassSessionButton.Style = (Style)Application.Current.Resources["SubtleButtonStyle"];
                NewSessionHeaderSubtext.Text = "Self-directed study session. Enter session metadata or use preset page search to auto-fill details.";
            }
            else
            {
                SelfSessionButton.Style = (Style)Application.Current.Resources["SubtleButtonStyle"];
                ClassSessionButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                NewSessionHeaderSubtext.Text = "Lecture or classroom study session. Enter session metadata or use preset page search to auto-fill details.";
            }
        }

        private void OnSubjectTextChanged()
        {
            string typedSubject = SubjectBox.Text.Trim();
            bool isCurated = CustomTagService.Instance.IsCuratedSubject(typedSubject);

            // Make editing the page range text only possible if the subject is a valid curated Subject
            StartPageBox.IsEnabled = isCurated;
            EndPageBox.IsEnabled = isCurated;
            SearchPresetButton.IsEnabled = isCurated;

            if (!string.IsNullOrWhiteSpace(typedSubject) && !isCurated)
            {
                CustomTagInfoCard.Visibility = Visibility.Visible;
                SubjectBadgeBorder.Visibility = Visibility.Visible;
                SubjectStatusBadge.Text = "Custom Subject";
                if (Application.Current.Resources.TryGetValue("SystemFillColorCautionBrush", out object? caution) && caution is Brush b)
                {
                    SubjectStatusBadge.Foreground = b;
                }
            }
            else if (isCurated)
            {
                CustomTagInfoCard.Visibility = Visibility.Collapsed;
                SubjectBadgeBorder.Visibility = Visibility.Visible;
                SubjectStatusBadge.Text = "Curated Preset";
                if (Application.Current.Resources.TryGetValue("AccentTextFillColorPrimaryBrush", out object? accent) && accent is Brush b)
                {
                    SubjectStatusBadge.Foreground = b;
                }
            }
            else
            {
                CustomTagInfoCard.Visibility = Visibility.Collapsed;
                SubjectBadgeBorder.Visibility = Visibility.Collapsed;
            }
        }

        private async Task OpenCustomTagPopupAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "Fill Custom Session Details",
                PrimaryButtonText = "Apply",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var stack = new StackPanel { Spacing = 14, Width = 380 };

            var chNumBox = new NumberBox
            {
                Header = "Chapter Number",
                Value = _customChapterNumber > 0 ? _customChapterNumber : 1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Minimum = 1,
                SmallChange = 1
            };

            var chNameBox = new TextBox
            {
                Header = "Chapter Name *",
                PlaceholderText = "e.g. Mechanics, Thermodynamics",
                Text = _customChapterName ?? ""
            };

            var topicBox = new TextBox
            {
                Header = "Topic Name (Optional)",
                PlaceholderText = "e.g. Kinetic Energy, Work Theorem",
                Text = _customTopicName ?? ""
            };

            var pageRangeBox = new TextBox
            {
                Header = "Page Range (Optional)",
                PlaceholderText = "e.g. 15-30",
                Text = _customPageRange ?? ""
            };

            stack.Children.Add(chNumBox);
            stack.Children.Add(chNameBox);
            stack.Children.Add(topicBox);
            stack.Children.Add(pageRangeBox);

            dialog.Content = stack;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string chName = chNameBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(chName))
                {
                    NotificationCard.Show("Chapter Name cannot be empty.", InfoBarSeverity.Warning, "Chapter Required");
                    return;
                }

                _customChapterNumber = double.IsNaN(chNumBox.Value) ? 1 : (int)chNumBox.Value;
                _customChapterName = chName;
                _customTopicName = string.IsNullOrWhiteSpace(topicBox.Text) ? null : topicBox.Text.Trim();
                _customPageRange = string.IsNullOrWhiteSpace(pageRangeBox.Text) ? null : pageRangeBox.Text.Trim();

                // Clear any preset results since user specified custom details
                _matchingResults.Clear();

                ChapterLabelText.Text = $"Ch.{_customChapterNumber}: {_customChapterName}";
                TopicLabelText.Text = string.IsNullOrWhiteSpace(_customTopicName) ? "(None)" : _customTopicName;
                MatchedSummaryBadgeText.Text = "(Custom Tag Defined)";
                MatchedSummaryBadgeText.Visibility = Visibility.Visible;

                NotificationCard.Show("Custom session details configured successfully.", InfoBarSeverity.Success, "Details Configured");
            }
        }

        private void OnSearchPresetClicked(object sender, RoutedEventArgs e)
        {
            string typedSubject = SubjectBox.Text.Trim();
            if (!CustomTagService.Instance.IsCuratedSubject(typedSubject))
            {
                NotificationCard.Show("Preset page lookup requires a valid curated subject.", InfoBarSeverity.Warning, "Curated Subject Required");
                return;
            }

            int startPage = double.IsNaN(StartPageBox.Value) ? 0 : (int)StartPageBox.Value;
            int endPage = double.IsNaN(EndPageBox.Value) ? startPage : (int)EndPageBox.Value;

            if (startPage <= 0)
            {
                NotificationCard.Show("Please enter a valid start page number (1 or greater).", InfoBarSeverity.Warning, "Invalid Page Range");
                return;
            }

            if (endPage < startPage)
            {
                endPage = startPage;
                EndPageBox.Value = endPage;
            }

            var results = PresetTagDatabase.Instance.Search(startPage, endPage);
            if (results != null && results.Count > 0)
            {
                // Prioritize results belonging to typed curated subject
                var subjResults = results.Where(r =>
                    string.Equals(r.SubjectShortName, typedSubject, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.SubjectKey, typedSubject, StringComparison.OrdinalIgnoreCase)).ToList();

                _matchingResults = subjResults.Count > 0 ? subjResults : results;

                // Clear any custom tags
                _customChapterName = null;
                _customTopicName = null;

                // Display all matched chapters and topics as labels
                var chapterSummaries = _matchingResults
                    .Select(r => r.ChapterNumber > 0 ? $"Ch.{r.ChapterNumber}: {r.ChapterName}" : r.ChapterName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();

                ChapterLabelText.Text = chapterSummaries.Count > 0 ? string.Join(", ", chapterSummaries) : "General";

                var topicSummaries = _matchingResults
                    .Select(r => r.TopicName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();

                TopicLabelText.Text = topicSummaries.Count > 0 ? string.Join(", ", topicSummaries) : "(No specific topics)";

                MatchedSummaryBadgeText.Text = $"{_matchingResults.Count} topic/chapter match(es) will be assigned to this session bundle";
                MatchedSummaryBadgeText.Visibility = Visibility.Visible;

                NotificationCard.Show($"Matched {_matchingResults.Count} topic/chapter preset(s) for pp. {startPage}-{endPage}. All will be added to the session bundle.", InfoBarSeverity.Success, "Presets Matched");
            }
            else
            {
                _matchingResults.Clear();
                ChapterLabelText.Text = "None selected";
                TopicLabelText.Text = "None selected";
                MatchedSummaryBadgeText.Visibility = Visibility.Collapsed;

                NotificationCard.Show($"Could not find any preset matching page range {startPage} - {endPage}.", InfoBarSeverity.Warning, "Not Found");
            }
        }

        private void OnNextClicked(object sender, RoutedEventArgs e)
        {
            string sessionName = SessionNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(sessionName))
            {
                NotificationCard.Show("Session Name cannot be empty. Please enter a Session Name.", InfoBarSeverity.Warning, "Session Name Required");
                SessionNameBox.Focus(FocusState.Programmatic);
                return;
            }

            string subject = SubjectBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(subject))
            {
                NotificationCard.Show("Please provide a Subject name.", InfoBarSeverity.Warning, "Subject Required");
                SubjectBox.Focus(FocusState.Programmatic);
                return;
            }

            // Case A: Multiple or single curated preset match(es) from search
            if (_matchingResults != null && _matchingResults.Count > 0)
            {
                var tags = new List<Tag>();
                foreach (var res in _matchingResults)
                {
                    tags.Add(new Tag
                    {
                        subject_name = string.IsNullOrWhiteSpace(res.SubjectShortName) ? subject : res.SubjectShortName,
                        chapter_number = res.ChapterNumber,
                        chapter_name = res.ChapterName,
                        topic_name = res.TopicName,
                        book_name = res.BookFullName,
                        page_range = res.PageRangeString,
                        session_type = _selectedSessionType
                    });
                }

                var session = new BasicSessionBundle(null, isCurated: true, nextReviewDate: DateTime.UtcNow.AddDays(3), sessionName: sessionName, tag: tags[0], tags: tags);
                SessionService.Instance.SaveSession(session);
                NotificationCard.Dismiss();
                Frame.Navigate(typeof(EditCanvasPage), session);
                return;
            }

            // Case B: Custom tag details filled via popup
            if (!string.IsNullOrWhiteSpace(_customChapterName))
            {
                var customTag = new Tag
                {
                    subject_name = subject,
                    chapter_number = _customChapterNumber,
                    chapter_name = _customChapterName,
                    topic_name = _customTopicName,
                    book_name = null,
                    page_range = _customPageRange,
                    session_type = _selectedSessionType
                };

                // Validate custom tag
                if (!CustomTagService.Instance.ValidateAndSaveCustomTag(customTag, out string? customError))
                {
                    NotificationCard.Show(customError ?? "A custom tag cannot use a Subject that matches any curated preset subject.", InfoBarSeverity.Error, "Curated Subject Conflict");
                    SubjectBox.Focus(FocusState.Programmatic);
                    return;
                }

                var session = new BasicSessionBundle(null, isCurated: false, nextReviewDate: DateTime.UtcNow.AddDays(3), sessionName: sessionName, tag: customTag, tags: new[] { customTag });
                SessionService.Instance.SaveSession(session);
                NotificationCard.Dismiss();
                Frame.Navigate(typeof(EditCanvasPage), session);
                return;
            }

            // Case C: Neither preset searched nor custom tag filled
            NotificationCard.Show("Please search a preset page range or click 'Fill Session Details' to configure custom chapter/topic metadata.", InfoBarSeverity.Warning, "Details Needed");
        }

        private void NavigateBack()
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(DashboardPage));
            }
        }
    }
}

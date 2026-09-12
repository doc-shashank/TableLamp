using System;
using System.IO;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Models;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class ViewCanvasPage : Page
    {
        private BasicSessionBundle? _session;
        private int _currentIndex;
        private bool IsAdvancedWorkflow =>
            string.Equals(MainScreen.Current?.InvocationMode, LauncherMode.Advanced, StringComparison.OrdinalIgnoreCase);

        public ViewCanvasPage()
        {
            this.InitializeComponent();

            BackButton.Click += (s, e) =>
            {
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
                else
                {
                    Frame.Navigate(typeof(DashboardPage));
                }
            };
            SessionInfoButton.Click += OnSessionInfoClicked;
            PreviousQuestionButton.Click += OnPreviousQuestionClicked;
            NextQuestionButton.Click += OnNextQuestionClicked;
            RateHardButton.Click += (s, e) => OnRecallRated(RecallRating.Hard);
            RateMediumButton.Click += (s, e) => OnRecallRated(RecallRating.Medium);
            RateEasyButton.Click += (s, e) => OnRecallRated(RecallRating.Easy);
            TextRadioButton.Checked += (s, e) => OnViewModeChanged("Text");
            ImageRadioButton.Checked += (s, e) => OnViewModeChanged("Image");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is BasicSessionBundle session)
            {
                _session = session;
            }
            else if (e.Parameter is string sessionId)
            {
                _session = SessionService.Instance.GetSessionById(sessionId);
            }

            _currentIndex = 0;
            PopulateSessionDetails();
            DisplayCurrentQuestion();
        }

        private void PopulateSessionDetails()
        {
            if (_session == null) return;

            PageHeaderTitle.Text = _session.DisplayTitle;
            HeaderSessionTypeGlyph.Glyph = _session.SessionTypeGlyph;
            HeaderSessionTypeBadge.Background = new SolidColorBrush(
                _session.IsClassSession
                    ? ColorHelper.FromArgb(255, 138, 79, 255)
                    : ColorHelper.FromArgb(255, 0, 103, 192));
            ToolTipService.SetToolTip(HeaderSessionTypeBadge, _session.SessionTypeName);
        }

        private async void OnSessionInfoClicked(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;

            var panel = new StackPanel { Spacing = 10, MinWidth = 360 };

            void AddRow(string label, string value)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    Foreground = Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var s) && s is Brush sBrush ? sBrush : null
                };
                var val = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(val, 1);
                row.Children.Add(lbl);
                row.Children.Add(val);
                panel.Children.Add(row);
            }

            AddRow("Session Name", _session.DisplayTitle);
            AddRow("Session Type", _session.SessionTypeName);
            AddRow("Subject", _session.Tag?.subject_name ?? "General");
            AddRow("Chapters", _session.ChaptersSummary);
            AddRow("Topics", _session.TopicsSummary);

            if (!string.IsNullOrWhiteSpace(_session.Tag?.book_name))
            {
                AddRow("Book", _session.Tag.book_name);
            }

            if (!string.IsNullOrWhiteSpace(_session.Tag?.page_range))
            {
                AddRow("Pages", _session.Tag.page_range);
            }

            AddRow("Questions", $"{_session.QuestionCount} question(s)");
            AddRow("Created", $"{_session.FormattedDate} at {_session.FormattedTime}");
            AddRow("Preset Mode", _session.isCurated ? "Curated Preset" : "Custom Preset");

            var dialog = new ContentDialog
            {
                Title = "Session Details",
                Content = panel,
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private void DisplayCurrentQuestion()
        {
            int total = _session?.questions.Count ?? 0;
            if (total == 0)
            {
                ProgressIndicatorText.Text = "0 of 0";
                QuestionDisplayTextBlock.Text = "This session has no questions yet.";
                TextContainer.Visibility = Visibility.Visible;
                ImageContainer.Visibility = Visibility.Collapsed;
                ViewTogglePanel.Visibility = Visibility.Collapsed;
                PreviousQuestionButton.IsEnabled = false;
                NextQuestionButton.IsEnabled = false;
                return;
            }

            if (_currentIndex < 0) _currentIndex = 0;
            if (_currentIndex >= total) _currentIndex = total - 1;

            ProgressIndicatorText.Text = $"Question {_currentIndex + 1} of {total}";
            PreviousQuestionButton.IsEnabled = _currentIndex > 0;
            NextQuestionButton.IsEnabled = _currentIndex < total - 1;

            var question = _session!.questions[_currentIndex] as SimpleQuestion;
            if (question == null)
            {
                var baseQ = _session.questions[_currentIndex];
                string baseText = baseQ.question_element_array?.FirstOrDefault()?.simple_text ?? "Question";
                QuestionDisplayTextBlock.Text = baseText;
                TextContainer.Visibility = Visibility.Visible;
                ImageContainer.Visibility = Visibility.Collapsed;
                ViewTogglePanel.Visibility = Visibility.Collapsed;
                return;
            }

            bool hasText = question.HasText;
            bool hasImage = question.HasImage;

            if (hasText && hasImage)
            {
                ViewTogglePanel.Visibility = Visibility.Visible;
                string currentMode = ImageRadioButton.IsChecked == true ? "Image" : "Text";
                UpdateViewMode(currentMode, question);
            }
            else if (hasImage)
            {
                ViewTogglePanel.Visibility = Visibility.Collapsed;
                ImageRadioButton.IsChecked = true;
                UpdateViewMode("Image", question);
            }
            else
            {
                ViewTogglePanel.Visibility = Visibility.Collapsed;
                TextRadioButton.IsChecked = true;
                UpdateViewMode("Text", question);
            }

            if (IsAdvancedWorkflow)
            {
                CardSpacedRepetitionBorder.Visibility = Visibility.Visible;
                RatingBarBorder.Visibility = Visibility.Visible;
                RateHardButton.IsEnabled = true;
                RateMediumButton.IsEnabled = true;
                RateEasyButton.IsEnabled = true;
                _ = UpdateCardSpacedRepetitionStatsAsync();
            }
            else
            {
                CardSpacedRepetitionBorder.Visibility = Visibility.Collapsed;
                RatingBarBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateViewMode(string mode, SimpleQuestion question)
        {
            if (mode == "Image" && question.HasImage)
            {
                string? imagePath = question.ImageElement?.simple_image?.ToString();
                if (!string.IsNullOrEmpty(imagePath))
                {
                    try
                    {
                        BitmapImage bitmap;
                        if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri))
                        {
                            bitmap = new BitmapImage(uri);
                        }
                        else if (File.Exists(imagePath))
                        {
                            bitmap = new BitmapImage(new Uri(imagePath));
                        }
                        else
                        {
                            bitmap = new BitmapImage(new Uri($"file:///{imagePath.Replace('\\', '/')}"));
                        }

                        QuestionDisplayImage.Source = bitmap;
                        ImageContainer.Visibility = Visibility.Visible;
                        TextContainer.Visibility = Visibility.Collapsed;
                        return;
                    }
                    catch (Exception)
                    {
                        // Fallback to text if image fails to load
                    }
                }
            }

            // Fallback to Text presentation
            string text = question.TextElement?.simple_text ?? question.TextElement?.formatted_text ?? "[Image Question]";
            QuestionDisplayTextBlock.Text = text;
            TextContainer.Visibility = Visibility.Visible;
            ImageContainer.Visibility = Visibility.Collapsed;
        }

        private void OnViewModeChanged(string mode)
        {
            if (_session != null && _currentIndex < _session.questions.Count)
            {
                var question = _session.questions[_currentIndex] as SimpleQuestion;
                if (question != null)
                {
                    UpdateViewMode(mode, question);
                }
            }
        }

        private void OnPreviousQuestionClicked(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                DisplayCurrentQuestion();
            }
        }

        private void OnNextQuestionClicked(object sender, RoutedEventArgs e)
        {
            if (_session != null && _currentIndex < _session.questions.Count - 1)
            {
                _currentIndex++;
                DisplayCurrentQuestion();
            }
        }

        private void OnQuestionCardTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (_session == null || _currentIndex < 0 || _currentIndex >= _session.questions.Count) return;
            var question = _session.questions[_currentIndex] as SimpleQuestion;
            if (question == null) return;

            if (question.HasText && question.HasImage)
            {
                // Toggle between Text and Image
                if (ImageContainer.Visibility == Visibility.Visible)
                {
                    TextRadioButton.IsChecked = true;
                    UpdateViewMode("Text", question);
                }
                else
                {
                    ImageRadioButton.IsChecked = true;
                    UpdateViewMode("Image", question);
                }
            }
            else if (question.HasImage)
            {
                ImageRadioButton.IsChecked = true;
                UpdateViewMode("Image", question);
            }
            else
            {
                TextRadioButton.IsChecked = true;
                UpdateViewMode("Text", question);
            }
        }

        private async Task UpdateCardSpacedRepetitionStatsAsync()
        {
            if (_session == null || _currentIndex < 0 || _currentIndex >= _session.questions.Count) return;

            try
            {
                var q = _session.questions[_currentIndex];
                string qidStr = (q as SimpleQuestion)?.Id ?? q.ToString() ?? Guid.NewGuid().ToString();
                var state = await SpacedRepetitionManager.Instance.GetOrCreateCardStateAsync(qidStr, _session.Id);

                DispatcherQueue.TryEnqueue(() =>
                {
                    CardIntervalTextBlock.Text = $"Interval: {state.IntervalDays:F1}d";
                    CardEaseTextBlock.Text = $"Ease: {state.EaseFactor:F2}";
                    CardRepsTextBlock.Text = $"Reps: {state.TotalRepetitions}";

                    if (state.IsGraduated)
                    {
                        CardDueStatusBadge.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 65));
                        CardDueStatusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                        CardDueStatusTextBlock.Text = "Graduated";
                    }
                    else if (state.IsDue())
                    {
                        CardDueStatusBadge.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 244, 206));
                        CardDueStatusTextBlock.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 141, 91, 0));
                        CardDueStatusTextBlock.Text = "Due Now";
                    }
                    else
                    {
                        double daysRemaining = Math.Max(0.1, (state.NextDueDate - DateTimeOffset.UtcNow).TotalDays);
                        CardDueStatusBadge.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 230, 240, 255));
                        CardDueStatusTextBlock.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 103, 192));
                        CardDueStatusTextBlock.Text = $"Due in {daysRemaining:F1}d";
                    }
                });
            }
            catch
            {
                // Fallback gracefully if database read encounters an issue
            }
        }

        private async void OnRecallRated(RecallRating rating)
        {
            if (_session == null || _currentIndex < 0 || _currentIndex >= _session.questions.Count) return;

            var q = _session.questions[_currentIndex];
            string qidStr = (q as SimpleQuestion)?.Id ?? q.ToString() ?? Guid.NewGuid().ToString();

            // Process review synchronously in memory, asynchronously persist to SQLite
            await SpacedRepetitionManager.Instance.ProcessRecallRatingAsync(qidStr, _session.Id, rating);

            int total = _session.questions.Count;
            if (_currentIndex < total - 1)
            {
                _currentIndex++;
                DisplayCurrentQuestion();
            }
            else
            {
                // Last question in session: calculate aggregate and display summary
                var aggregate = await SpacedRepetitionManager.Instance.EvaluateSessionAsync(_session);
                await ShowSessionCompletionDialogAsync(aggregate);
            }
        }

        private async Task ShowSessionCompletionDialogAsync(SessionReviewAggregate aggregate)
        {
            var panel = new StackPanel { Spacing = 12, MinWidth = 340 };

            var iconBorder = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 65)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                Child = new FontIcon
                {
                    Glyph = "\uE73E",
                    FontSize = 20,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            panel.Children.Add(iconBorder);

            var titleText = new TextBlock
            {
                Text = "Session Review Complete!",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(titleText);

            var subtext = new TextBlock
            {
                Text = $"Great job! Active recall intervals and memory curves have been updated for all questions in '{_session?.DisplayTitle}'.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            panel.Children.Add(subtext);

            var statsBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var statsStack = new StackPanel { Spacing = 6 };
            void AddStatRow(string label, string val)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var l = new TextBlock { Text = label, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
                var v = new TextBlock { Text = val, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                Grid.SetColumn(l, 0);
                Grid.SetColumn(v, 1);
                g.Children.Add(l);
                g.Children.Add(v);
                statsStack.Children.Add(g);
            }

            AddStatRow("Total Questions", aggregate.TotalQuestions.ToString());
            AddStatRow("Graduated / Mastered", $"{aggregate.GraduatedQuestionsCount} / {aggregate.TotalQuestions}");
            string nextDueStr = aggregate.NextSessionDue.HasValue
                ? aggregate.NextSessionDue.Value.LocalDateTime.ToString("MMM d, yyyy h:mm tt")
                : (aggregate.IsSessionCompleted ? "All Cards Graduated!" : "None");
            AddStatRow("Next Scheduled Review", nextDueStr);

            statsBorder.Child = statsStack;
            panel.Children.Add(statsBorder);

            var dialog = new ContentDialog
            {
                Title = "Active Recall Summary",
                Content = panel,
                PrimaryButtonText = "Back to Dashboard",
                SecondaryButtonText = "Review Again",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (Frame.CanGoBack) Frame.GoBack();
                else Frame.Navigate(typeof(DashboardPage));
            }
            else
            {
                _currentIndex = 0;
                DisplayCurrentQuestion();
            }
        }
    }
}

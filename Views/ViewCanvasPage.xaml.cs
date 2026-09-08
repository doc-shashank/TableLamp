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
    }
}

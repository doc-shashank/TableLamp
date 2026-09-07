using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Models;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class EditCanvasPage : Page
    {
        private Tag? _tag;
        private BasicSessionBundle? _session;
        private readonly ObservableCollection<SimpleQuestion> _questions = new();
        private SimpleQuestion? _selectedQuestion;

        public EditCanvasPage()
        {
            this.InitializeComponent();
            QuestionsItemsControl.ItemsSource = _questions;

            BackButton.Click += (s, e) => NavigateBack();
            AddQuestionButton.Click += OnAddQuestionClicked;
            SaveSessionButton.Click += OnSaveSessionClicked;

            EditTextPencilButton.Click += OnEditTextPencilClicked;
            EditImagePencilButton.Click += OnEditImagePencilClicked;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is BasicSessionBundle session)
            {
                _session = session;
                _tag = session.Tag;
                HeaderTitleText.Text = session.DisplayTitle;

                _questions.Clear();
                foreach (var q in session.questions.OfType<SimpleQuestion>())
                {
                    _questions.Add(q);
                }
            }
            else if (e.Parameter is Tag tag)
            {
                _tag = tag;
                _session = new BasicSessionBundle(null, false, DateTime.UtcNow.AddDays(3), null, _tag);
                HeaderTitleText.Text = $"{tag.subject_name} - Ch.{tag.chapter_number} {tag.chapter_name}";
            }

            UpdateUIState();

            // Auto-select first question if present
            if (_questions.Count > 0)
            {
                SelectQuestion(_questions[0]);
            }
            else
            {
                ClearSelection();
            }
        }

        private async void OnAddQuestionClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new QuestionPopUp(_tag)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.ResultQuestion != null)
            {
                if (_session == null)
                {
                    _session = new BasicSessionBundle(null, false, DateTime.UtcNow.AddDays(3), null, _tag);
                }

                _session.AddQuestion(dialog.ResultQuestion);
                _questions.Add(dialog.ResultQuestion);
                UpdateUIState();

                SelectQuestion(dialog.ResultQuestion);
            }
        }

        private void OnQuestionCardSelected(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is SimpleQuestion q)
            {
                SelectQuestion(q);
            }
        }

        private void SelectQuestion(SimpleQuestion question)
        {
            _selectedQuestion = question;
            NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
            PreviewCardsContainer.Visibility = Visibility.Visible;

            RefreshPreviewCards();
        }

        private void ClearSelection()
        {
            _selectedQuestion = null;
            PreviewCardsContainer.Visibility = Visibility.Collapsed;
            NoSelectionPlaceholder.Visibility = Visibility.Visible;
        }

        private void RefreshPreviewCards()
        {
            if (_selectedQuestion == null) return;

            // 1. Text Preview Card
            string text = _selectedQuestion.TextElement?.simple_text ?? _selectedQuestion.TextElement?.formatted_text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                TextPreviewContentBlock.Text = "No text content. Click 'Edit Text' pencil button above to add text.";
                TextPreviewContentBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            }
            else
            {
                TextPreviewContentBlock.Text = text;
                TextPreviewContentBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            }

            // 2. Image Preview Card
            string? imagePath = _selectedQuestion.ImageElement?.simple_image?.ToString();
            if (!string.IsNullOrWhiteSpace(imagePath))
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

                    ImagePreviewContentImage.Source = bitmap;
                    ImagePreviewContentImage.Visibility = Visibility.Visible;
                    NoImagePlaceholderText.Visibility = Visibility.Collapsed;
                }
                catch (Exception)
                {
                    ImagePreviewContentImage.Visibility = Visibility.Collapsed;
                    NoImagePlaceholderText.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ImagePreviewContentImage.Source = null;
                ImagePreviewContentImage.Visibility = Visibility.Collapsed;
                NoImagePlaceholderText.Visibility = Visibility.Visible;
            }
        }

        private async void OnEditTextPencilClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedQuestion == null) return;

            string? currentText = _selectedQuestion.TextElement?.simple_text ?? _selectedQuestion.TextElement?.formatted_text;
            var dialog = new SingleElementEditDialog("Text", currentText)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _selectedQuestion.UpdateText(dialog.ResultText);

                // Refresh ObservableCollection item so left-side card updates
                int idx = _questions.IndexOf(_selectedQuestion);
                if (idx >= 0)
                {
                    _questions[idx] = _selectedQuestion;
                }

                RefreshPreviewCards();
            }
        }

        private async void OnEditImagePencilClicked(object sender, RoutedEventArgs e)
        {
            if (_selectedQuestion == null) return;

            string? currentImagePath = _selectedQuestion.ImageElement?.simple_image?.ToString();
            var dialog = new SingleElementEditDialog("Image", currentImagePath)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _selectedQuestion.UpdateImage(dialog.ResultImagePath);

                // Refresh ObservableCollection item so left-side card updates
                int idx = _questions.IndexOf(_selectedQuestion);
                if (idx >= 0)
                {
                    _questions[idx] = _selectedQuestion;
                }

                RefreshPreviewCards();
            }
        }

        private void OnDeleteQuestionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SimpleQuestion question)
            {
                _session?.RemoveQuestion(question);
                _questions.Remove(question);

                if (_selectedQuestion == question)
                {
                    if (_questions.Count > 0)
                    {
                        SelectQuestion(_questions[0]);
                    }
                    else
                    {
                        ClearSelection();
                    }
                }

                UpdateUIState();
            }
        }

        private void OnSaveSessionClicked(object sender, RoutedEventArgs e)
        {
            if (_session == null)
            {
                _session = new BasicSessionBundle(_questions, false, DateTime.UtcNow.AddDays(3), null, _tag);
            }
            else
            {
                _session.ClearQuestions();
                _session.AddQuestions(_questions);
            }

            SessionService.Instance.SaveSession(_session);
            Frame.Navigate(typeof(SessionListsPage));
        }

        private void UpdateUIState()
        {
            bool hasQuestions = _questions.Count > 0;
            LeftEmptyStateBorder.Visibility = hasQuestions ? Visibility.Collapsed : Visibility.Visible;
            QuestionsItemsControl.Visibility = hasQuestions ? Visibility.Visible : Visibility.Collapsed;
            QuestionCountBadge.Text = $"{_questions.Count} Question{(_questions.Count == 1 ? "" : "s")}";
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

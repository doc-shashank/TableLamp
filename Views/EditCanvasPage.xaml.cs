using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        public EditCanvasPage()
        {
            this.InitializeComponent();
            QuestionsItemsControl.ItemsSource = _questions;

            BackButton.Click += (s, e) => NavigateBack();
            AddQuestionButton.Click += OnAddQuestionClicked;
            EmptyAddButton.Click += OnAddQuestionClicked;
            SaveSessionButton.Click += OnSaveSessionClicked;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Tag tag)
            {
                _tag = tag;
                _session = new BasicSessionBundle(null, false, DateTime.UtcNow.AddDays(3), null, _tag);
                HeaderTitleText.Text = $"{tag.subject_name} - Ch.{tag.chapter_number} {tag.chapter_name}";
            }
            else if (e.Parameter is BasicSessionBundle existingSession)
            {
                _session = existingSession;
                _tag = existingSession.Tag;
                HeaderTitleText.Text = existingSession.DisplayTitle;

                _questions.Clear();
                foreach (var q in existingSession.questions.OfType<SimpleQuestion>())
                {
                    _questions.Add(q);
                }
            }

            UpdateUIState();
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
                // Ensure BasicSessionBundle instance is created (only first time for a given session)
                if (_session == null)
                {
                    _session = new BasicSessionBundle(null, false, DateTime.UtcNow.AddDays(3), null, _tag);
                }

                _session.AddQuestion(dialog.ResultQuestion);
                _questions.Add(dialog.ResultQuestion);
                UpdateUIState();
            }
        }

        private async void OnEditQuestionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SimpleQuestion question)
            {
                var dialog = new QuestionPopUp(_tag, question)
                {
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // Refresh collection item
                    int idx = _questions.IndexOf(question);
                    if (idx >= 0)
                    {
                        _questions[idx] = question;
                    }
                    UpdateUIState();
                }
            }
        }

        private void OnDeleteQuestionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SimpleQuestion question)
            {
                _session?.RemoveQuestion(question);
                _questions.Remove(question);
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
            EmptyStateBorder.Visibility = hasQuestions ? Visibility.Collapsed : Visibility.Visible;
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

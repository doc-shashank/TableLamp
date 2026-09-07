using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Models;

namespace TableLamp.Views
{
    public sealed partial class SessionDetailsPage : Page
    {
        public SessionDetailsPage()
        {
            this.InitializeComponent();

            TopicToggle.Toggled += (s, e) =>
            {
                TopicNameBox.Visibility = TopicToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            };

            BackButton.Click += (s, e) => NavigateBack();
            CancelButton.Click += (s, e) => NavigateBack();
            NextButton.Click += OnNextClicked;
        }

        private void OnNextClicked(object sender, RoutedEventArgs e)
        {
            string subject = SubjectBox.Text.Trim();
            string chapterName = ChapterNameBox.Text.Trim();
            int chapterNumber = double.IsNaN(ChapterNumberBox.Value) ? 1 : (int)ChapterNumberBox.Value;
            string? topicName = TopicToggle.IsOn ? TopicNameBox.Text.Trim() : null;

            if (string.IsNullOrWhiteSpace(subject))
            {
                ValidationInfoBar.Message = "Please provide a Subject name.";
                ValidationInfoBar.IsOpen = true;
                SubjectBox.Focus(FocusState.Programmatic);
                return;
            }

            if (string.IsNullOrWhiteSpace(chapterName))
            {
                ValidationInfoBar.Message = "Please provide a Chapter Name.";
                ValidationInfoBar.IsOpen = true;
                ChapterNameBox.Focus(FocusState.Programmatic);
                return;
            }

            ValidationInfoBar.IsOpen = false;

            var tag = new Tag
            {
                subject_name = subject,
                chapter_number = chapterNumber,
                chapter_name = chapterName,
                topic_name = topicName
            };

            // Navigate to EditCanvasPage with the created Tag
            Frame.Navigate(typeof(EditCanvasPage), tag);
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

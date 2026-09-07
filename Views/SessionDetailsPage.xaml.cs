using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Models;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class SessionDetailsPage : Page
    {
        private List<PresetSearchResult> _matchingResults = new();
        private int _currentMatchIndex;
        private string? _currentBookName;
        private string? _currentPageRange;

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
            SearchPresetButton.Click += OnSearchPresetClicked;

            // Wire key navigation and mouse scrollwheel for multi-match shifting
            this.KeyDown += OnSessionDetailsKeyDown;
            this.PointerWheelChanged += OnSessionDetailsPointerWheelChanged;
        }

        private void OnSearchPresetClicked(object sender, RoutedEventArgs e)
        {
            int startPage = double.IsNaN(StartPageBox.Value) ? 0 : (int)StartPageBox.Value;
            int endPage = double.IsNaN(EndPageBox.Value) ? startPage : (int)EndPageBox.Value;

            if (startPage <= 0)
            {
                SearchFeedbackInfoBar.Severity = InfoBarSeverity.Warning;
                SearchFeedbackInfoBar.Title = "Invalid Page Range";
                SearchFeedbackInfoBar.Message = "Please enter a valid start page number (1 or greater).";
                SearchFeedbackInfoBar.IsOpen = true;
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
                _matchingResults = results;
                _currentMatchIndex = 0;
                ApplyPresetResult(_matchingResults[0]);

                if (_matchingResults.Count > 1)
                {
                    SearchFeedbackInfoBar.Severity = InfoBarSeverity.Informational;
                    SearchFeedbackInfoBar.Title = "Multiple Presets Found";
                    SearchFeedbackInfoBar.Message = $"Found {_matchingResults.Count} matching topics for pp. {startPage}-{endPage}. Use Up/Down arrow keys or mouse scrollwheel to shift between configurations (1/{_matchingResults.Count}).";
                }
                else
                {
                    SearchFeedbackInfoBar.Severity = InfoBarSeverity.Success;
                    SearchFeedbackInfoBar.Title = "Preset Matched";
                    SearchFeedbackInfoBar.Message = $"Auto-filled details for: {_matchingResults[0].DisplayText}";
                }
                SearchFeedbackInfoBar.IsOpen = true;
            }
            else
            {
                _matchingResults.Clear();
                _currentMatchIndex = 0;
                BookMetadataBadge.Visibility = Visibility.Collapsed;

                SearchFeedbackInfoBar.Severity = InfoBarSeverity.Warning;
                SearchFeedbackInfoBar.Title = "Not Found";
                SearchFeedbackInfoBar.Message = $"Could not find any preset matching page range {startPage} - {endPage}.";
                SearchFeedbackInfoBar.IsOpen = true;
            }
        }

        private void ApplyPresetResult(PresetSearchResult result)
        {
            SubjectBox.Text = result.SubjectShortName;
            ChapterNumberBox.Value = result.ChapterNumber;
            ChapterNameBox.Text = result.ChapterName;

            if (!string.IsNullOrWhiteSpace(result.TopicName))
            {
                TopicToggle.IsOn = true;
                TopicNameBox.Text = result.TopicName;
                TopicNameBox.Visibility = Visibility.Visible;
            }

            _currentBookName = result.BookFullName;
            _currentPageRange = result.PageRangeString;

            BookNameText.Text = $"Book: {_currentBookName}";
            PageRangeBadgeText.Text = $"Pages: {_currentPageRange}";
            BookMetadataBadge.Visibility = Visibility.Visible;
        }

        private void ShiftMatch(int delta)
        {
            if (_matchingResults == null || _matchingResults.Count <= 1) return;

            int count = _matchingResults.Count;
            _currentMatchIndex = (_currentMatchIndex + delta + count) % count;

            ApplyPresetResult(_matchingResults[_currentMatchIndex]);

            SearchFeedbackInfoBar.Message = $"Found {_matchingResults.Count} matching topics. Use Up/Down arrow keys or mouse scrollwheel to shift between configurations ({_currentMatchIndex + 1}/{count}).";
            SearchFeedbackInfoBar.IsOpen = true;
        }

        private void OnSessionDetailsKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_matchingResults != null && _matchingResults.Count > 1)
            {
                if (e.Key == Windows.System.VirtualKey.Up)
                {
                    ShiftMatch(-1);
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.Down)
                {
                    ShiftMatch(1);
                    e.Handled = true;
                }
            }
        }

        private void OnSessionDetailsPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (_matchingResults != null && _matchingResults.Count > 1)
            {
                var point = e.GetCurrentPoint(this);
                int delta = point.Properties.MouseWheelDelta;
                if (delta > 0)
                {
                    ShiftMatch(-1);
                    e.Handled = true;
                }
                else if (delta < 0)
                {
                    ShiftMatch(1);
                    e.Handled = true;
                }
            }
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
                topic_name = topicName,
                book_name = _currentBookName,
                page_range = _currentPageRange ?? (StartPageBox.Value > 0 ? $"{StartPageBox.Value}-{EndPageBox.Value}" : null)
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

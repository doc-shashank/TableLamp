using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Models;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class SessionListsPage : Page
    {
        private List<BasicSessionBundle> _allSessions = new();
        private string _selectedTypeFilter = "All"; // "All", "Self", "Class"
        private string _selectedSortOrder = "Newest"; // "Newest", "Oldest"

        public SessionListsPage()
        {
            this.InitializeComponent();

            BackButton.Click += (s, e) => Frame.Navigate(typeof(DashboardPage));
            CreateNewSessionButton.Click += (s, e) => Frame.Navigate(typeof(SessionDetailsPage));
            SearchBox.TextChanged += (s, e) => FilterSessions(SearchBox.Text.Trim());

            // Wire filter buttons
            FilterAllButton.Click += (s, e) => SetTypeFilter("All");
            FilterSelfButton.Click += (s, e) => SetTypeFilter("Self");
            FilterClassButton.Click += (s, e) => SetTypeFilter("Class");

            // Wire sort buttons
            SortNewestButton.Click += (s, e) => SetSortOrder("Newest");
            SortOldestButton.Click += (s, e) => SetSortOrder("Oldest");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string param && param == "Newest")
            {
                SetSortOrder("Newest");
            }

            LoadSessions();
        }

        private void SetTypeFilter(string filter)
        {
            _selectedTypeFilter = filter;

            var accentStyle = (Style)Application.Current.Resources["AccentButtonStyle"];
            var subtleStyle = (Style)Application.Current.Resources["SubtleButtonStyle"];

            FilterAllButton.Style = filter == "All" ? accentStyle : subtleStyle;
            FilterSelfButton.Style = filter == "Self" ? accentStyle : subtleStyle;
            FilterClassButton.Style = filter == "Class" ? accentStyle : subtleStyle;

            FilterSessions(SearchBox.Text.Trim());
        }

        private void SetSortOrder(string sortOrder)
        {
            _selectedSortOrder = sortOrder;

            var accentStyle = (Style)Application.Current.Resources["AccentButtonStyle"];
            var subtleStyle = (Style)Application.Current.Resources["SubtleButtonStyle"];

            SortNewestButton.Style = sortOrder == "Newest" ? accentStyle : subtleStyle;
            SortOldestButton.Style = sortOrder == "Oldest" ? accentStyle : subtleStyle;

            FilterSessions(SearchBox.Text.Trim());
        }

        private void LoadSessions()
        {
            _allSessions = SessionService.Instance.GetAllSessions().ToList();
            FilterSessions(SearchBox.Text.Trim());
        }

        private void FilterSessions(string query)
        {
            IEnumerable<BasicSessionBundle> queryable = _allSessions;

            // 1. Text Search Filter
            if (!string.IsNullOrWhiteSpace(query))
            {
                queryable = queryable.Where(s =>
                    s.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (s.Tag?.subject_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Tag?.chapter_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Tag?.topic_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    s.Tags.Any(t =>
                        (t.subject_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (t.chapter_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (t.topic_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)));
            }

            // 2. Type Filter
            if (_selectedTypeFilter == "Self")
            {
                queryable = queryable.Where(s => s.IsSelfSession);
            }
            else if (_selectedTypeFilter == "Class")
            {
                queryable = queryable.Where(s => s.IsClassSession);
            }

            // 3. Date Sort
            if (_selectedSortOrder == "Oldest")
            {
                queryable = queryable.OrderBy(s => s.creation_date);
            }
            else
            {
                // Default: Newest first
                queryable = queryable.OrderByDescending(s => s.creation_date);
            }

            var filtered = queryable.ToList();
            SessionsItemsControl.ItemsSource = filtered;
            EmptyStateBorder.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnSessionCardPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                // Keep BorderThickness constant to prevent layout shifts during hover
                if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accent) && accent is Brush b)
                {
                    border.BorderBrush = b;
                }
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out object? bg) && bg is Brush bgBrush)
                {
                    border.Background = bgBrush;
                }
            }
        }

        private void OnSessionCardPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? stroke) && stroke is Brush b)
                {
                    border.BorderBrush = b;
                }
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? bg) && bg is Brush bgBrush)
                {
                    border.Background = bgBrush;
                }
            }
        }

        private void OnSessionCardTapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is BasicSessionBundle session)
            {
                Frame.Navigate(typeof(ViewCanvasPage), session);
            }
        }

        private void OnReviewSessionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BasicSessionBundle session)
            {
                Frame.Navigate(typeof(ViewCanvasPage), session);
            }
        }

        private async void OnDeleteSessionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BasicSessionBundle session)
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete Session",
                    Content = $"Are you sure you want to delete session \"{session.DisplayTitle}\"? This action cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    SessionService.Instance.DeleteSession(session.Id);
                    LoadSessions();
                }
            }
        }
    }
}

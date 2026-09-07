using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Models;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class SessionListsPage : Page
    {
        private List<BasicSessionBundle> _allSessions = new();

        public SessionListsPage()
        {
            this.InitializeComponent();

            BackButton.Click += (s, e) => Frame.Navigate(typeof(DashboardPage));
            CreateNewSessionButton.Click += (s, e) => Frame.Navigate(typeof(SessionDetailsPage));
            SearchBox.TextChanged += (s, e) => FilterSessions(SearchBox.Text.Trim());
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadSessions();
        }

        private void LoadSessions()
        {
            _allSessions = SessionService.Instance.GetAllSessions().ToList();
            FilterSessions(SearchBox.Text.Trim());
        }

        private void FilterSessions(string query)
        {
            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allSessions
                : _allSessions.Where(s =>
                    s.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (s.Tag?.subject_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Tag?.chapter_name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

            SessionsItemsControl.ItemsSource = filtered;
            EmptyStateBorder.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

        private void OnDeleteSessionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BasicSessionBundle session)
            {
                SessionService.Instance.DeleteSession(session.Id);
                LoadSessions();
            }
        }
    }
}

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
    public class DateSessionGroup
    {
        public DateTime Date { get; set; }

        /// <summary>
        /// Strictly formatted as "dd/mm/yyyy" (e.g. 12/09/2026) as required by Agent Instructions.
        /// </summary>
        public string DateHeader => Date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

        public IReadOnlyList<BasicSessionBundle> Sessions { get; set; } = Array.Empty<BasicSessionBundle>();

        public string SessionCountText => $"{Sessions.Count} {(Sessions.Count == 1 ? "session" : "sessions")}";
    }

    public sealed partial class RecentSessionPage : Page
    {
        private DateTime? _selectedFilterDate;

        public RecentSessionPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadGroupedSessions();
        }

        public void LoadGroupedSessions(DateTime? filterDate = null)
        {
            _selectedFilterDate = filterDate;
            var allSessions = SessionService.Instance.GetAllSessions();

            IEnumerable<BasicSessionBundle> filtered = allSessions;
            if (_selectedFilterDate.HasValue)
            {
                filtered = filtered.Where(s => s.creation_date.Date == _selectedFilterDate.Value.Date);
            }

            var groups = filtered
                .GroupBy(s => s.creation_date.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new DateSessionGroup
                {
                    Date = g.Key,
                    Sessions = g.OrderByDescending(s => s.creation_date).ToList().AsReadOnly()
                })
                .ToList();

            if (groups.Count == 0)
            {
                EmptyStateBorder.Visibility = Visibility.Visible;
                DateGroupsItemsControl.Visibility = Visibility.Collapsed;

                if (_selectedFilterDate.HasValue)
                {
                    EmptyStateText.Text = $"No sessions found for {_selectedFilterDate.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}.";
                }
                else
                {
                    EmptyStateText.Text = "No recently recorded sessions.";
                }
            }
            else
            {
                EmptyStateBorder.Visibility = Visibility.Collapsed;
                DateGroupsItemsControl.Visibility = Visibility.Visible;
                DateGroupsItemsControl.ItemsSource = groups;
            }
        }

        private void OnFilterDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (args.NewDate.HasValue)
            {
                LoadGroupedSessions(args.NewDate.Value.DateTime.Date);
            }
            else
            {
                LoadGroupedSessions(null);
            }
        }

        private void OnClearFilterClicked(object sender, RoutedEventArgs e)
        {
            FilterDatePicker.Date = null;
            LoadGroupedSessions(null);
        }

        private void OnBackButtonClicked(object sender, RoutedEventArgs e)
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

        private void OnSessionCardTapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is BasicSessionBundle session)
            {
                Frame.Navigate(typeof(ViewCanvasPage), session);
            }
        }

        private void OnSessionCardPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
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
    }
}

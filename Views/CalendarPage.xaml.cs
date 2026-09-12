using System;
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
    public sealed partial class CalendarPage : Page
    {
        public CalendarPage()
        {
            this.InitializeComponent();

            SessionsCalendarView.SelectedDatesChanged += OnCalendarDatesChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Select today's date by default if nothing selected
            if (SessionsCalendarView.SelectedDates.Count == 0)
            {
                SessionsCalendarView.SelectedDates.Add(DateTimeOffset.Now);
            }
            else
            {
                UpdateSelectedDateSessions(SessionsCalendarView.SelectedDates.First().DateTime);
            }
        }

        private void OnCalendarDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (args.AddedDates.Count > 0)
            {
                UpdateSelectedDateSessions(args.AddedDates[0].DateTime);
            }
        }

        private bool IsAdvancedWorkflow =>
            string.Equals(MainScreen.Current?.InvocationMode, LauncherMode.Advanced, StringComparison.OrdinalIgnoreCase);

        private async void UpdateSelectedDateSessions(DateTime date)
        {
            SelectedDateHeaderText.Text = date.Date == DateTime.Today
                ? "Sessions for Today"
                : $"Sessions for {date:MMM d, yyyy}";

            var allSessions = SessionService.Instance.GetSessionsByDate(date);
            var selfSessions = allSessions.Where(s => s.IsSelfSession).ToList();
            var classSessions = allSessions.Where(s => s.IsClassSession).ToList();

            SelfSessionsItemsControl.ItemsSource = selfSessions;
            ClassSessionsItemsControl.ItemsSource = classSessions;

            SelfSessionsCountText.Text = selfSessions.Count.ToString();
            ClassSessionsCountText.Text = classSessions.Count.ToString();

            // Load pending reviews due on this date (only in Advanced workflow)
            var scheduledReviews = IsAdvancedWorkflow
                ? await SpacedRepetitionManager.Instance.GetReviewsForDateAsync(date)
                : (System.Collections.Generic.IReadOnlyList<SessionReviewAggregate>)Array.Empty<SessionReviewAggregate>();

            ScheduledReviewsItemsControl.ItemsSource = scheduledReviews;
            ScheduledReviewsCountText.Text = scheduledReviews.Count.ToString();

            bool hasSessions = allSessions.Count > 0;
            bool hasReviews = scheduledReviews.Count > 0;
            bool hasAny = hasSessions || hasReviews;

            ScheduledReviewsGroup.Visibility = hasReviews ? Visibility.Visible : Visibility.Collapsed;
            EmptyDayBorder.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;
            SelfSessionsGroup.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
            ClassSessionsGroup.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;

            NoSelfSessionsText.Visibility = selfSessions.Count == 0 && hasSessions ? Visibility.Visible : Visibility.Collapsed;
            NoClassSessionsText.Visibility = classSessions.Count == 0 && hasSessions ? Visibility.Visible : Visibility.Collapsed;

            if (hasSessions && hasReviews)
            {
                SelectedDateSubtext.Text = $"{allSessions.Count} session(s) recorded • {scheduledReviews.Count} active recall review(s) pending";
            }
            else if (hasReviews)
            {
                SelectedDateSubtext.Text = $"{scheduledReviews.Count} active recall review(s) pending";
            }
            else if (hasSessions)
            {
                SelectedDateSubtext.Text = $"{allSessions.Count} session{(allSessions.Count == 1 ? "" : "s")} recorded ({selfSessions.Count} Self, {classSessions.Count} Class)";
            }
            else
            {
                SelectedDateSubtext.Text = IsAdvancedWorkflow
                    ? "No study activity or scheduled reviews on this day"
                    : "No study sessions recorded on this day";
            }
        }

        private void OnScheduledReviewTapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is SessionReviewAggregate agg)
            {
                NavigateToSessionByAggregate(agg);
            }
        }

        private void OnStartReviewClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SessionReviewAggregate agg)
            {
                NavigateToSessionByAggregate(agg);
            }
        }

        private void NavigateToSessionByAggregate(SessionReviewAggregate agg)
        {
            var all = SessionService.Instance.GetAllSessions();
            var session = all.FirstOrDefault(s => SpacedRepetitionDatabase.DeterministicGuid(s.Id) == agg.SessionId);
            if (session != null)
            {
                Frame.Navigate(typeof(ViewCanvasPage), session);
            }
        }

        private void OnSessionCardTapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is BasicSessionBundle session)
            {
                Frame.Navigate(typeof(ViewCanvasPage), session);
            }
        }

        private void OnOpenSessionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BasicSessionBundle session)
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
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? bg) && bg is Brush bgBrush)
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
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out object? bg) && bg is Brush bgBrush)
                {
                    border.Background = bgBrush;
                }
            }
        }
    }
}

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
                LoadSessionsForDate(SessionsCalendarView.SelectedDates.First().DateTime);
            }
        }

        private void OnCalendarDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (args.AddedDates.Count > 0)
            {
                LoadSessionsForDate(args.AddedDates[0].DateTime);
            }
        }

        private void LoadSessionsForDate(DateTime date)
        {
            SelectedDateHeaderText.Text = $"Sessions for {date:MMMM dd, yyyy}";

            var sessions = SessionService.Instance.GetSessionsByDate(date);
            DaySessionsItemsControl.ItemsSource = sessions;

            bool hasSessions = sessions.Count > 0;
            EmptyDayBorder.Visibility = hasSessions ? Visibility.Collapsed : Visibility.Visible;
            DaySessionsItemsControl.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
            SelectedDateSubtext.Text = hasSessions
                ? $"{sessions.Count} session{(sessions.Count == 1 ? "" : "s")} recorded on this day"
                : "No study activity recorded on this day";
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
    }
}

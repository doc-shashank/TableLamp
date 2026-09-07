using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Controllers;
using TableLamp.Models;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class DashboardPage : Page
    {
        private BasicUI? _basicUIController;

        public DashboardPage()
        {
            this.InitializeComponent();

            _basicUIController = new BasicUI(this);

            WireCardButtons();
            ViewAllSessionsButton.Click += (s, e) => Frame.Navigate(typeof(SessionListsPage));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _basicUIController?.LoadRecentSessions();
        }

        private void WireCardButtons()
        {
            // 1. Create a New Session Button
            CreateSessionCardButton.PointerEntered += (s, e) => HighlightCard(CreateSessionCardButton, true);
            CreateSessionCardButton.PointerExited += (s, e) => HighlightCard(CreateSessionCardButton, false);
            CreateSessionCardButton.Tapped += (s, e) => Frame.Navigate(typeof(SessionDetailsPage));

            // 2. Review Session Button
            ReviewSessionCardButton.PointerEntered += (s, e) => HighlightCard(ReviewSessionCardButton, true);
            ReviewSessionCardButton.PointerExited += (s, e) => HighlightCard(ReviewSessionCardButton, false);
            ReviewSessionCardButton.Tapped += (s, e) => Frame.Navigate(typeof(SessionListsPage));
        }

        private void HighlightCard(Border card, bool highlight)
        {
            if (highlight)
            {
                if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accent) && accent is Brush b)
                {
                    card.BorderBrush = b;
                    card.BorderThickness = new Thickness(2);
                }
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? stroke) && stroke is Brush b)
                {
                    card.BorderBrush = b;
                    card.BorderThickness = new Thickness(1.5);
                }
            }
        }

        public void PopulateRecentSessions(System.Collections.Generic.IEnumerable<BasicSessionBundle> sessions)
        {
            RecentSessionsItemsControl.ItemsSource = sessions;
        }

        private void OnRecentSessionTapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is BasicSessionBundle session)
            {
                Frame.Navigate(typeof(ViewCanvasPage), session);
            }
        }
    }
}

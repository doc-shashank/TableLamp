using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Controllers;
using TableLamp.Models;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class DashboardPage : Page
    {
        private BasicUI? _basicUIController;
        private bool _isCreateSessionExpanded;

        public DashboardPage()
        {
            this.InitializeComponent();

            _basicUIController = new BasicUI(this);

            WireCardButtons();
            ViewAllSessionsButton.Click += (s, e) => Frame.Navigate(typeof(SessionListsPage), "Newest");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _basicUIController?.LoadRecentSessions();
        }

        private void WireCardButtons()
        {
            // 1. Create a New Session Button (Extends vertically with animation to reveal Curated and Custom)
            CreateSessionCardButton.PointerEntered += (s, e) => HighlightCard(CreateSessionCardButton, true);
            CreateSessionCardButton.PointerExited += (s, e) => HighlightCard(CreateSessionCardButton, false);
            CreateSessionCardButton.Tapped += OnCreateSessionCardTapped;

            // Curated & Custom option buttons
            CuratedOptionButton.Click += (s, e) =>
            {
                Frame.Navigate(typeof(SessionDetailsPage), "Curated");
            };

            CustomOptionButton.Click += (s, e) =>
            {
                Frame.Navigate(typeof(SessionDetailsPage), "Custom");
            };

            // 2. Review Session Button
            ReviewSessionCardButton.PointerEntered += (s, e) => HighlightCard(ReviewSessionCardButton, true);
            ReviewSessionCardButton.PointerExited += (s, e) => HighlightCard(ReviewSessionCardButton, false);
            ReviewSessionCardButton.Tapped += (s, e) => Frame.Navigate(typeof(SessionListsPage));
        }

        private void OnCreateSessionCardTapped(object sender, TappedRoutedEventArgs e)
        {
            // If the tap was inside the options buttons, ignore here as button click handler navigates
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != CreateSessionCardButton)
                {
                    if (current == CuratedOptionButton || current == CustomOptionButton)
                    {
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            _isCreateSessionExpanded = !_isCreateSessionExpanded;
            AnimateCreateSessionCard(_isCreateSessionExpanded);
        }

        private void AnimateCreateSessionCard(bool expand)
        {
            double fromHeight = CreateSessionCardButton.ActualHeight > 0 ? CreateSessionCardButton.ActualHeight : 250;
            double toHeight = expand ? 335 : 250;

            var sb = new Storyboard();
            var anim = new DoubleAnimation
            {
                From = fromHeight,
                To = toHeight,
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(anim, CreateSessionCardButton);
            Storyboard.SetTargetProperty(anim, "Height");
            sb.Children.Add(anim);

            if (expand)
            {
                CreateSessionOptionsPanel.Visibility = Visibility.Visible;
                GetStartedIndicator.Visibility = Visibility.Collapsed;
                var fadeAnim = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200))
                };
                Storyboard.SetTarget(fadeAnim, CreateSessionOptionsPanel);
                Storyboard.SetTargetProperty(fadeAnim, "Opacity");
                sb.Children.Add(fadeAnim);
            }
            else
            {
                var fadeAnim = new DoubleAnimation
                {
                    From = CreateSessionOptionsPanel.Opacity,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                fadeAnim.Completed += (s, e) =>
                {
                    CreateSessionOptionsPanel.Visibility = Visibility.Collapsed;
                    GetStartedIndicator.Visibility = Visibility.Visible;
                };
                Storyboard.SetTarget(fadeAnim, CreateSessionOptionsPanel);
                Storyboard.SetTargetProperty(fadeAnim, "Opacity");
                sb.Children.Add(fadeAnim);
            }

            sb.Begin();
        }

        private void HighlightCard(Border card, bool highlight)
        {
            // Keep BorderThickness constant to prevent layout shifts during hover
            card.BorderThickness = new Thickness(1.5);

            if (highlight)
            {
                if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accent) && accent is Brush b)
                {
                    card.BorderBrush = b;
                }
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? stroke) && stroke is Brush b)
                {
                    card.BorderBrush = b;
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

        private void OnRecentSessionPointerEntered(object sender, PointerRoutedEventArgs e)
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

        private void OnRecentSessionPointerExited(object sender, PointerRoutedEventArgs e)
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

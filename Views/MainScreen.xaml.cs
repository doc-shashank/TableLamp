using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Controllers;
using TableLamp.Models;
using TableLamp.Services;
using WinRT.Interop;

namespace TableLamp.Views
{
    public sealed partial class MainScreen : Window
    {
        public static new MainScreen? Current { get; private set; }
        public static MainScreen? Instance => Current;

        public string InvocationMode { get; private set; } = "Basic";
        public AppWindow? AppWindowInstance { get; private set; }

        public MainScreen() : this("Basic")
        {
        }

        public MainScreen(string modeArgument)
        {
            Current = this;
            this.InitializeComponent();

            CheckAndSetArgument(modeArgument);
            MainScreenVersionTextBlock.Text = $"v{AppVersionService.CurrentVersionString}";
            ConfigureWindowSizing();
            WireNavigationEvents();
            WireCaptionButtons();

            // Navigate to DashboardPage
            NavigateToDashboard();
        }

        public void CheckAndSetArgument(string? argument)
        {
            InvocationMode = LauncherMode.Normalize(argument);
            UpdateNavigationForMode();
        }

        private void UpdateNavigationForMode()
        {
            if (ModeBadgeTextBlock != null)
            {
                ModeBadgeTextBlock.Text = InvocationMode;
            }

            if (string.Equals(InvocationMode, LauncherMode.Generator, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(InvocationMode, LauncherMode.Library, StringComparison.OrdinalIgnoreCase))
            {
                // Header of MainScreen will just be a dashboard icon
                if (CalendarNavButton != null) CalendarNavButton.Visibility = Visibility.Collapsed;
                if (SettingsNavButton != null) SettingsNavButton.Visibility = Visibility.Collapsed;
                if (DashboardNavButton != null) DashboardNavButton.Visibility = Visibility.Visible;
            }
            else
            {
                if (CalendarNavButton != null) CalendarNavButton.Visibility = Visibility.Visible;
                if (SettingsNavButton != null) SettingsNavButton.Visibility = Visibility.Visible;
                if (DashboardNavButton != null) DashboardNavButton.Visibility = Visibility.Visible;
            }
        }

        private bool _isReturningToLauncher = false;

        private void ConfigureWindowSizing()
        {
            try
            {
                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                if (hwnd != IntPtr.Zero)
                {
                    WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    AppWindowInstance = AppWindow.GetFromWindowId(windowId);

                    if (AppWindowInstance != null)
                    {
                        AppWindowInstance.Title = $"Table Lamp - {InvocationMode}";

                        // Border-only window without native title bar or native caption buttons
                        if (AppWindowInstance.Presenter is OverlappedPresenter presenter)
                        {
                            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                            presenter.IsResizable = false;
                            presenter.IsMaximizable = false;
                            presenter.Maximize();
                        }

                        // Closing the main window should open the launcher and not exit the app
                        AppWindowInstance.Closing += (s, args) =>
                        {
                            if (!_isReturningToLauncher)
                            {
                                _isReturningToLauncher = true;
                                var launcher = new TableLampLauncher();
                                launcher.Activate();
                            }
                        };
                    }
                }
            }
            catch (Exception)
            {
                // Fallback for test / headless environments
            }
        }

        private void WireCaptionButtons()
        {
            WindowMinimizeButton.Click += (s, e) =>
            {
                if (AppWindowInstance?.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                }
            };

            WindowExitButton.Click += (s, e) =>
            {
                // Closing MainScreen triggers reopening TableLampLauncher per requirement
                this.Close();
            };

            WindowExitButton.PointerEntered += (s, e) =>
            {
                WindowExitButton.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 232, 17, 35));
                if (WindowExitButton.Content is FontIcon icon)
                {
                    icon.Foreground = new SolidColorBrush(Colors.White);
                }
            };

            WindowExitButton.PointerExited += (s, e) =>
            {
                WindowExitButton.Background = new SolidColorBrush(Colors.Transparent);
                if (WindowExitButton.Content is FontIcon icon)
                {
                    icon.ClearValue(FontIcon.ForegroundProperty);
                }
            };
        }

        private void WireNavigationEvents()
        {
            this.Closed += (s, e) =>
            {
                if (Current == this) Current = null;

                if (!_isReturningToLauncher)
                {
                    _isReturningToLauncher = true;
                    var launcher = new TableLampLauncher();
                    launcher.Activate();
                }
            };

            DashboardNavButton.Click += (s, e) => NavigateToDashboard();
            CalendarNavButton.Click += (s, e) => NavigateToCalendar();
            SettingsNavButton.Click += (s, e) => NavigateToSettings();

            ThemeToggleButton.Click += (s, e) =>
            {
                if (Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = rootElement.ActualTheme == ElementTheme.Dark
                        ? ElementTheme.Light
                        : ElementTheme.Dark;
                }
            };

            RootFrame.Navigated += OnFrameNavigated;
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs e)
        {
            bool isDashboard = e.SourcePageType == typeof(DashboardPage);
            bool isCalendar = e.SourcePageType == typeof(CalendarPage);
            bool isSettings = e.SourcePageType == typeof(SettingsPage);

            DashboardNavButton.Style = (Style)Application.Current.Resources[isDashboard ? "DefaultButtonStyle" : "SubtleButtonStyle"];
            CalendarNavButton.Style = (Style)Application.Current.Resources[isCalendar ? "DefaultButtonStyle" : "SubtleButtonStyle"];
            SettingsNavButton.Style = (Style)Application.Current.Resources[isSettings ? "DefaultButtonStyle" : "SubtleButtonStyle"];
        }

        public void NavigateToDashboard()
        {
            RootFrame.Navigate(typeof(DashboardPage));
        }

        public void NavigateToCalendar()
        {
            RootFrame.Navigate(typeof(CalendarPage));
        }

        public void NavigateToSettings()
        {
            RootFrame.Navigate(typeof(SettingsPage));
        }

        public void ShowNotification(string message, InfoBarSeverity severity = InfoBarSeverity.Informational, string? title = null)
        {
            NotificationCard.Show(message, severity, title);
        }
    }
}

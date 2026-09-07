using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TableLamp.Controllers;
using TableLamp.Models;
using WinRT.Interop;

namespace TableLamp.Views
{
    public sealed partial class MainScreen : Window
    {
        public string InvocationMode { get; private set; } = "Basic";
        public AppWindow? AppWindowInstance { get; private set; }

        public MainScreen() : this("Basic")
        {
        }

        public MainScreen(string modeArgument)
        {
            this.InitializeComponent();

            CheckAndSetArgument(modeArgument);
            ConfigureWindowSizing();
            WireNavigationEvents();

            // Navigate to DashboardPage
            NavigateToDashboard();
        }

        public void CheckAndSetArgument(string? argument)
        {
            InvocationMode = LauncherMode.Normalize(argument);
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

                        if (AppWindowInstance.Presenter is OverlappedPresenter presenter)
                        {
                            presenter.IsResizable = false;
                            presenter.IsMaximizable = false;
                            presenter.Maximize();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback for test / headless environments
            }
        }

        private void WireNavigationEvents()
        {
            this.Closed += (s, e) =>
            {
                if (!_isReturningToLauncher)
                {
                    _isReturningToLauncher = true;
                    var launcher = new TableLampLauncher();
                    launcher.Activate();
                }
            };

            ReturnToLauncherButton.Click += (s, e) =>
            {
                _isReturningToLauncher = true;
                var launcher = new TableLampLauncher();
                launcher.Activate();
                this.Close();
            };

            DashboardNavButton.Click += (s, e) => NavigateToDashboard();
            CalendarNavButton.Click += (s, e) => NavigateToCalendar();

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

            DashboardNavButton.Style = (Style)Application.Current.Resources[isDashboard ? "DefaultButtonStyle" : "SubtleButtonStyle"];
            CalendarNavButton.Style = (Style)Application.Current.Resources[isCalendar ? "DefaultButtonStyle" : "SubtleButtonStyle"];
        }

        public void NavigateToDashboard()
        {
            RootFrame.Navigate(typeof(DashboardPage));
        }

        public void NavigateToCalendar()
        {
            RootFrame.Navigate(typeof(CalendarPage));
        }
    }
}

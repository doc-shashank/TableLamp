using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace TableLamp.Views
{
    /// <summary>
    /// TableLampLauncher is the initial compact window displaying three horizontal card containers:
    /// 'Basic', 'Advanced', and 'Generator'.
    /// Containers listen to mouse events and act as buttons.
    /// Advanced and Generator cards are non-interactable (for now).
    /// The Basic card button opens a maximized, non-resizable MainScreen.
    /// </summary>
    public sealed partial class TableLampLauncher : Window
    {
        private AppWindow? _appWindow;

        public TableLampLauncher()
        {
            this.InitializeComponent();

            ConfigureLauncherWindow();
            WireCardMouseEvents();
            WireCaptionButtons();
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private void ConfigureLauncherWindow()
        {
            try
            {
                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                if (hwnd != IntPtr.Zero)
                {
                    WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    _appWindow = AppWindow.GetFromWindowId(windowId);

                    if (_appWindow != null)
                    {
                        _appWindow.Title = "Table Lamp Launcher";

                        // Border-only window without native titlebar or native caption buttons
                        if (_appWindow.Presenter is OverlappedPresenter presenter)
                        {
                            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                            presenter.IsResizable = false;
                            presenter.IsMaximizable = false;
                        }

                        // Determine display scaling for DPI-aware sizing
                        uint dpi = GetDpiForWindow(hwnd);
                        double scale = (dpi > 0 ? dpi : 96) / 96.0;

                        const int targetWidthDip = 820;
                        const int targetHeightDip = 500;

                        int physWidth = (int)Math.Round(targetWidthDip * scale);
                        int physHeight = (int)Math.Round(targetHeightDip * scale);
                        _appWindow.Resize(new Windows.Graphics.SizeInt32(physWidth, physHeight));

                        // Center on display work area
                        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                        if (displayArea != null)
                        {
                            var position = _appWindow.Position;
                            position.X = Math.Max(0, (displayArea.WorkArea.Width - physWidth) / 2);
                            position.Y = Math.Max(0, (displayArea.WorkArea.Height - physHeight) / 2);
                            _appWindow.Move(position);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback for headless or test environments
            }
        }

        private void WireCaptionButtons()
        {
            WindowMinimizeButton.Click += (s, e) =>
            {
                if (_appWindow?.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                }
            };

            WindowExitButton.Click += (s, e) =>
            {
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

        private void WireCardMouseEvents()
        {
            // 1. Basic Card - Fully Interactive Button
            BasicCard.PointerEntered += OnBasicPointerEntered;
            BasicCard.PointerExited += OnBasicPointerExited;
            BasicCard.PointerPressed += OnBasicPointerPressed;
            BasicCard.PointerReleased += OnBasicPointerReleased;
            BasicCard.Tapped += OnBasicCardTapped;

            // 2. Advanced Card - Non-interactable (for now)
            AdvancedCard.PointerEntered += OnDisabledCardPointerEntered;
            AdvancedCard.PointerExited += OnDisabledCardPointerExited;

            // 3. Generator Card - Non-interactable (for now)
            GeneratorCard.PointerEntered += OnDisabledCardPointerEntered;
            GeneratorCard.PointerExited += OnDisabledCardPointerExited;

            // 4. Dev Tools Button
            DevToolsButton.Click += OnDevToolsButtonClicked;
        }

        private void OnDevToolsButtonClicked(object sender, RoutedEventArgs e)
        {
            // Open singleton maximized DevToolsWindow
            var devTools = DevToolsWindow.GetOrCreateInstance();
            devTools.Activate();
        }

        #region Basic Card Interactions

        private void OnBasicPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accentBrush) && accentBrush is Brush brush)
            {
                BasicCard.BorderBrush = brush;
                BasicCard.BorderThickness = new Thickness(1.5);
            }
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out object? bgBrush) && bgBrush is Brush hoverBg)
            {
                BasicCard.Background = hoverBg;
            }
        }

        private void OnBasicPointerExited(object sender, PointerRoutedEventArgs e)
        {
            ResetBasicCardStyle();
        }

        private void OnBasicPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            BasicCard.BorderThickness = new Thickness(1.5);
        }

        private void OnBasicPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ResetBasicCardStyle();
        }

        private void ResetBasicCardStyle()
        {
            if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? strokeBrush) && strokeBrush is Brush stroke)
            {
                BasicCard.BorderBrush = stroke;
                BasicCard.BorderThickness = new Thickness(1.5);
            }
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? bgBrush) && bgBrush is Brush bg)
            {
                BasicCard.Background = bg;
            }
        }

        private void OnBasicCardTapped(object sender, TappedRoutedEventArgs e)
        {
            // Open maximized non-resizable MainScreen with "Basic" argument
            LaunchMainScreen("Basic");
        }

        #endregion

        #region Non-interactable Cards

        private void OnDisabledCardPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Visual indication of non-interactability
        }

        private void OnDisabledCardPointerExited(object sender, PointerRoutedEventArgs e)
        {
            // No-op
        }

        #endregion

        /// <summary>
        /// Opens MainScreen with the specified argument, activates it, and closes the launcher.
        /// </summary>
        public void LaunchMainScreen(string modeArgument)
        {
            var mainScreen = new MainScreen(modeArgument);
            mainScreen.Activate();
            this.Close();
        }

        public void ShowNotification(string message, InfoBarSeverity severity = InfoBarSeverity.Informational, string? title = null)
        {
            NotificationCard.Show(message, severity, title);
        }
    }
}

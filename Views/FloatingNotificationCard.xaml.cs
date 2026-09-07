using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace TableLamp.Views
{
    public sealed partial class FloatingNotificationCard : UserControl
    {
        private DispatcherTimer? _autoDismissTimer;

        public FloatingNotificationCard()
        {
            this.InitializeComponent();
            DismissToastButton.Click += (s, e) => Dismiss();
        }

        public void Show(string message, InfoBarSeverity severity = InfoBarSeverity.Informational, string? title = null, int autoDismissMs = 4500)
        {
            ToastMessageText.Text = message;

            if (!string.IsNullOrWhiteSpace(title))
            {
                ToastTitleText.Text = title;
                ToastTitleText.Visibility = Visibility.Visible;
            }
            else
            {
                ToastTitleText.Visibility = Visibility.Collapsed;
            }

            // Configure icon and colors based on severity
            switch (severity)
            {
                case InfoBarSeverity.Success:
                    SeverityFontIcon.Glyph = "\uE73E"; // CheckMark
                    SeverityFontIcon.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 65));
                    SeverityIconBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(35, 16, 124, 65));
                    break;
                case InfoBarSeverity.Error:
                    SeverityFontIcon.Glyph = "\uEA39"; // ErrorBadge
                    SeverityFontIcon.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
                    SeverityIconBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(35, 196, 43, 28));
                    break;
                case InfoBarSeverity.Warning:
                    SeverityFontIcon.Glyph = "\uE7BA"; // Warning
                    SeverityFontIcon.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 216, 59, 1));
                    SeverityIconBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(35, 216, 59, 1));
                    break;
                case InfoBarSeverity.Informational:
                default:
                    SeverityFontIcon.Glyph = "\uE946"; // Info
                    if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accentBrush) && accentBrush is Brush b)
                    {
                        SeverityFontIcon.Foreground = b;
                    }
                    else
                    {
                        SeverityFontIcon.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 215));
                    }
                    SeverityIconBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(35, 0, 120, 215));
                    break;
            }

            ToastRoot.Visibility = Visibility.Visible;

            // Setup auto dismiss timer
            if (_autoDismissTimer == null)
            {
                _autoDismissTimer = new DispatcherTimer();
                _autoDismissTimer.Tick += (s, e) => Dismiss();
            }

            _autoDismissTimer.Stop();
            if (autoDismissMs > 0)
            {
                _autoDismissTimer.Interval = TimeSpan.FromMilliseconds(autoDismissMs);
                _autoDismissTimer.Start();
            }
        }

        public void Dismiss()
        {
            _autoDismissTimer?.Stop();
            ToastRoot.Visibility = Visibility.Collapsed;
        }
    }
}

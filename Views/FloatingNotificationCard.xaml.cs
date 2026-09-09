using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace TableLamp.Views
{
    public sealed partial class FloatingNotificationCard : UserControl
    {
        private DispatcherTimer? _progressTimer;
        private DateTime _startTime;
        private double _totalDurationMs;

        public FloatingNotificationCard()
        {
            this.InitializeComponent();
            DismissToastButton.Click += (s, e) => Dismiss();
        }

        public void Show(string message, InfoBarSeverity severity = InfoBarSeverity.Informational, string? title = null, int autoDismissMs = -1)
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

            // Configure icon, colors, and progress bar based on severity
            Brush severityBrush = GetSeverityBrush(severity);
            DismissProgressBar.Foreground = severityBrush;

            switch (severity)
            {
                case InfoBarSeverity.Success:
                    SeverityFontIcon.Glyph = "\uE73E"; // CheckMark
                    SeverityFontIcon.Foreground = severityBrush;
                    SeverityIconBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(35, 16, 124, 65));
                    break;
                case InfoBarSeverity.Error:
                    SeverityFontIcon.Glyph = "\uEA39"; // ErrorBadge
                    SeverityFontIcon.Foreground = severityBrush;
                    SeverityIconBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(35, 196, 43, 28));
                    break;
                case InfoBarSeverity.Warning:
                    SeverityFontIcon.Glyph = "\uE7BA"; // Warning
                    SeverityFontIcon.Foreground = severityBrush;
                    SeverityIconBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(35, 216, 59, 1));
                    break;
                case InfoBarSeverity.Informational:
                default:
                    SeverityFontIcon.Glyph = "\uE946"; // Info
                    SeverityFontIcon.Foreground = severityBrush;
                    SeverityIconBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(35, 0, 120, 215));
                    break;
            }

            // Duration is configurable in settings
            if (autoDismissMs > 0)
            {
                _totalDurationMs = Math.Max(500.0, autoDismissMs);
            }
            else
            {
                int durationSeconds = TableLamp.Services.AppSettingsService.Instance.NotificationDurationSeconds;
                _totalDurationMs = Math.Max(1000.0, durationSeconds * 1000.0);
            }

            DismissProgressBar.Value = 100.0;
            ToastRoot.Visibility = Visibility.Visible;

            _startTime = DateTime.UtcNow;

            if (_progressTimer == null)
            {
                _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
                _progressTimer.Tick += OnProgressTimerTick;
            }

            _progressTimer.Stop();
            _progressTimer.Start();
        }

        private void OnProgressTimerTick(object? sender, object e)
        {
            double elapsed = (DateTime.UtcNow - _startTime).TotalMilliseconds;
            double remainingRatio = 1.0 - (elapsed / _totalDurationMs);

            if (double.IsNaN(remainingRatio) || remainingRatio <= 0.0)
            {
                DismissProgressBar.Value = 0.0;
                Dismiss();
            }
            else
            {
                DismissProgressBar.Value = Math.Clamp(remainingRatio * 100.0, 0.0, 100.0);
            }
        }

        private static Brush GetSeverityBrush(InfoBarSeverity severity)
        {
            return severity switch
            {
                InfoBarSeverity.Success => new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 65)),
                InfoBarSeverity.Error => new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28)),
                InfoBarSeverity.Warning => new SolidColorBrush(ColorHelper.FromArgb(255, 216, 59, 1)),
                _ => Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? b) && b is Brush brush
                    ? brush
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 215))
            };
        }

        public void Dismiss()
        {
            _progressTimer?.Stop();
            ToastRoot.Visibility = Visibility.Collapsed;
        }
    }
}

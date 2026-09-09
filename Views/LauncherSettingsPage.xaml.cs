using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class LauncherSettingsPage : Page
    {
        public event Action? BackRequested;
        private string? _latestReleaseUrl;

        public LauncherSettingsPage()
        {
            this.InitializeComponent();

            InitializeUi();
            WireEvents();
        }

        private void InitializeUi()
        {
            AppVersionBadgeText.Text = $"Current Version: v{AppUpdateService.CurrentVersionString}";
            AppRepoInfoText.Text = $"Repository: {AppUpdateService.RepoOwner}/{AppUpdateService.RepoName}";

            RefreshCuratedInfo();
        }

        private void RefreshCuratedInfo()
        {
            string curatedVer = AppSettingsService.Instance.CuratedContentVersion;
            var lastUpdated = AppSettingsService.Instance.CuratedContentLastUpdated;
            string dateStr = lastUpdated.HasValue ? $" (Updated {lastUpdated.Value.ToLocalTime():yyyy-MM-dd})" : "";
            CuratedVersionBadgeText.Text = $"Curated Version: {curatedVer}{dateStr}";
            CuratedRepoInfoText.Text = $"Repository: {CuratedContentUpdateService.RepoOwner}/{CuratedContentUpdateService.RepoName}";

            var db = PresetTagDatabase.Instance;
            CuratedMetricsText.Text = $"Curated Database: {db.SubjectCount} subjects, {db.ChapterCount} chapters, {db.TopicCount} topics";
        }

        private void WireEvents()
        {
            BackToLauncherButton.Click += (s, e) => BackRequested?.Invoke();

            CheckAppUpdateButton.Click += OnCheckAppUpdateClicked;
            SyncCuratedPresetsButton.Click += OnSyncCuratedPresetsClicked;
            ViewReleaseUrlButton.Click += OnViewReleaseUrlClicked;
        }

        private async void OnCheckAppUpdateClicked(object sender, RoutedEventArgs e)
        {
            CheckAppUpdateButton.IsEnabled = false;
            AppUpdateProgressRing.IsActive = true;
            AppUpdateProgressRing.Visibility = Visibility.Visible;
            AppUpdateStatusText.Text = "Checking GitHub for latest application release...";
            ViewReleaseUrlButton.Visibility = Visibility.Collapsed;

            try
            {
                var result = await AppUpdateService.CheckForUpdatesAsync();
                if (result.Success)
                {
                    if (result.IsUpdateAvailable)
                    {
                        AppUpdateStatusText.Text = $"Update available: {result.LatestVersion}! {result.ReleaseTitle}";
                        _latestReleaseUrl = result.ReleaseUrl;
                        if (!string.IsNullOrWhiteSpace(_latestReleaseUrl))
                        {
                            ViewReleaseUrlButton.Visibility = Visibility.Visible;
                        }
                        ShowBanner($"A new version ({result.LatestVersion}) is available on GitHub!", InfoBarSeverity.Informational);
                    }
                    else
                    {
                        AppUpdateStatusText.Text = $"You are running the latest version of Table Lamp (v{AppUpdateService.CurrentVersionString}).";
                        ShowBanner($"Table Lamp is up to date (v{AppUpdateService.CurrentVersionString}).", InfoBarSeverity.Success);
                    }
                }
                else
                {
                    AppUpdateStatusText.Text = result.ErrorMessage ?? "Could not retrieve update information.";
                    ShowBanner(result.ErrorMessage ?? "Failed to check for updates.", InfoBarSeverity.Warning);
                }
            }
            catch (Exception ex)
            {
                AppUpdateStatusText.Text = $"Error: {ex.Message}";
                ShowBanner($"Update check encountered an error: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                AppUpdateProgressRing.IsActive = false;
                AppUpdateProgressRing.Visibility = Visibility.Collapsed;
                CheckAppUpdateButton.IsEnabled = true;
            }
        }

        private async void OnSyncCuratedPresetsClicked(object sender, RoutedEventArgs e)
        {
            SyncCuratedPresetsButton.IsEnabled = false;
            CuratedProgressRing.IsActive = true;
            CuratedProgressRing.Visibility = Visibility.Visible;
            CuratedStatusText.Text = "Checking repository, downloading package, and rebuilding Curated Database...";

            try
            {
                var result = await CuratedContentUpdateService.DownloadAndApplyUpdateAsync();
                if (result.Success)
                {
                    RefreshCuratedInfo();
                    CuratedStatusText.Text = $"Successfully synced Curated Tags ({result.VersionApplied}). Found {result.TotalFound} files, merged {result.FilesImported} preset file(s).";
                    ShowBanner($"Curated tags updated to {result.VersionApplied} ({result.FilesImported} files merged). Custom tags remain untouched.", InfoBarSeverity.Success);
                }
                else
                {
                    CuratedStatusText.Text = result.ErrorMessage ?? "Failed to sync curated tags.";
                    ShowBanner(result.ErrorMessage ?? "Curated tag synchronization failed.", InfoBarSeverity.Warning);
                }
            }
            catch (Exception ex)
            {
                CuratedStatusText.Text = $"Error: {ex.Message}";
                ShowBanner($"Curated tag sync failed: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                CuratedProgressRing.IsActive = false;
                CuratedProgressRing.Visibility = Visibility.Collapsed;
                SyncCuratedPresetsButton.IsEnabled = true;
            }
        }

        private async void OnViewReleaseUrlClicked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_latestReleaseUrl) && Uri.TryCreate(_latestReleaseUrl, UriKind.Absolute, out var uri))
            {
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }

        private void ShowBanner(string message, InfoBarSeverity severity)
        {
            LauncherSettingsInfoBar.Message = message;
            LauncherSettingsInfoBar.Severity = severity;
            LauncherSettingsInfoBar.IsOpen = true;
        }
    }
}

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TableLamp.Services;

namespace TableLamp.Views
{
    public sealed partial class LauncherSettingsPage : Page
    {
        public event Action? BackRequested;

        public LauncherSettingsPage()
        {
            this.InitializeComponent();

            InitializeUi();
            WireEvents();
        }

        private void InitializeUi()
        {
            AppVersionBadgeText.Text = $"Current Version: v{AppVersionService.CurrentVersionString}";
            AppRepoInfoText.Text = "Repository: doc-shashank/table-lamp";

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
            if (db.IsEmpty)
            {
                CuratedMetricsText.Text = "Curated Database: Empty (0 subjects). Download latest tags from GitHub.";
                ShowBanner("Curated tags database is currently empty. Click 'Sync Curated Presets' below to download the latest tags from the GitHub repository.", InfoBarSeverity.Informational);
            }
            else
            {
                CuratedMetricsText.Text = $"Curated Database: {db.SubjectCount} subjects, {db.ChapterCount} chapters, {db.TopicCount} topics";
            }
        }

        private void WireEvents()
        {
            BackToLauncherButton.Click += (s, e) => BackRequested?.Invoke();
            SyncCuratedPresetsButton.Click += OnSyncCuratedPresetsClicked;
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
                    if (result.AlreadyUpToDate)
                    {
                        CuratedStatusText.Text = $"Curated tags are already up to date ({result.VersionApplied}).";
                        ShowBanner($"Curated tags are already up to date ({result.VersionApplied}).", InfoBarSeverity.Informational);
                    }
                    else
                    {
                        CuratedStatusText.Text = $"Successfully synced Curated Tags ({result.VersionApplied}). Found {result.TotalFound} files, merged {result.FilesImported} preset file(s).";
                        ShowBanner($"Curated tags updated to {result.VersionApplied} ({result.FilesImported} files merged). Custom tags remain untouched.", InfoBarSeverity.Success);
                    }
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



        private void ShowBanner(string message, InfoBarSeverity severity)
        {
            LauncherSettingsInfoBar.Message = message;
            LauncherSettingsInfoBar.Severity = severity;
            LauncherSettingsInfoBar.IsOpen = true;
        }
    }
}

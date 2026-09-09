using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TableLamp.Services;
using WinRT.Interop;

namespace TableLamp.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();

            PresetTagDatabase.Instance.PresetsChanged += RefreshStats;
            CustomPresetTagDatabase.Instance.PresetsChanged += RefreshStats;
            RefreshStats();

            ImportJsonFilesButton.Click += OnImportJsonFilesClicked;
            ImportFolderButton.Click += OnImportFolderClicked;
            ImportZipArchiveButton.Click += OnImportZipArchiveClicked;
            FormatCuratedDatabaseButton.Click += OnFormatCuratedDatabaseClicked;
            FormatCustomDatabaseButton.Click += OnFormatCustomDatabaseClicked;
            FormatAllDatabasesButton.Click += OnFormatAllDatabasesClicked;
            CheckForUpdatesButton.Click += OnCheckForUpdatesClicked;
            UpdateCuratedPresetsButton.Click += OnUpdateCuratedPresetsClicked;

            LoadNotificationDurationSetting();
            NotificationDurationComboBox.SelectionChanged += OnNotificationDurationChanged;
        }

        private async void OnCheckForUpdatesClicked(object sender, RoutedEventArgs e)
        {
            CheckForUpdatesButton.IsEnabled = false;
            NotificationCard.Show("Checking for Table Lamp updates...", InfoBarSeverity.Informational);

            try
            {
                var result = await AppUpdateService.CheckForUpdatesAsync();
                if (result.Success)
                {
                    if (result.IsUpdateAvailable)
                    {
                        NotificationCard.Show($"New version available: {result.LatestVersion}! {result.ReleaseTitle}", InfoBarSeverity.Informational);
                    }
                    else
                    {
                        NotificationCard.Show($"You are running the latest version of Table Lamp (v{AppUpdateService.CurrentVersionString}).", InfoBarSeverity.Success);
                    }
                }
                else
                {
                    NotificationCard.Show(result.ErrorMessage ?? "Could not check for updates.", InfoBarSeverity.Warning);
                }
            }
            catch (Exception ex)
            {
                NotificationCard.Show($"Update check failed: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                CheckForUpdatesButton.IsEnabled = true;
            }
        }

        private async void OnUpdateCuratedPresetsClicked(object sender, RoutedEventArgs e)
        {
            UpdateCuratedPresetsButton.IsEnabled = false;
            NotificationCard.Show("Checking and syncing curated presets from GitHub...", InfoBarSeverity.Informational);

            try
            {
                var result = await CuratedContentUpdateService.DownloadAndApplyUpdateAsync();
                if (result.Success)
                {
                    if (result.AlreadyUpToDate)
                    {
                        NotificationCard.Show($"Curated tags are already on the latest version ({result.VersionApplied}).", InfoBarSeverity.Informational);
                    }
                    else
                    {
                        NotificationCard.Show($"Curated tags updated to {result.VersionApplied}! {result.FilesImported} file(s) merged into Curated Database.", InfoBarSeverity.Success);
                        RefreshStats();
                    }
                }
                else
                {
                    NotificationCard.Show(result.ErrorMessage ?? "Failed to sync curated tags.", InfoBarSeverity.Warning);
                }
            }
            catch (Exception ex)
            {
                NotificationCard.Show($"Curated tag sync failed: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                UpdateCuratedPresetsButton.IsEnabled = true;
            }
        }

        private bool _isInitializingSettings = true;

        private void LoadNotificationDurationSetting()
        {
            _isInitializingSettings = true;
            int current = AppSettingsService.Instance.NotificationDurationSeconds;
            foreach (var item in NotificationDurationComboBox.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is string tagStr && int.TryParse(tagStr, out int val) && val == current)
                {
                    NotificationDurationComboBox.SelectedItem = cbi;
                    break;
                }
            }
            if (NotificationDurationComboBox.SelectedItem == null && NotificationDurationComboBox.Items.Count > 1)
            {
                NotificationDurationComboBox.SelectedIndex = 1; // 5s default
            }
            _isInitializingSettings = false;
        }

        private void OnNotificationDurationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingSettings) return;

            if (NotificationDurationComboBox.SelectedItem is ComboBoxItem cbi &&
                cbi.Tag is string tagStr &&
                int.TryParse(tagStr, out int seconds))
            {
                AppSettingsService.Instance.NotificationDurationSeconds = seconds;
                NotificationCard.Show($"Notification duration set to {seconds} seconds.", InfoBarSeverity.Success, "Setting Saved");
            }
        }

        private void RefreshStats()
        {
            // Preset database stats dynamically presented via View Statistics dialog
        }

        private async void OnViewStatisticsClicked(object sender, RoutedEventArgs e)
        {
            var curatedDb = PresetTagDatabase.Instance;
            var customDb = CustomPresetTagDatabase.Instance;

            var contentPanel = new StackPanel { Spacing = 16 };

            // Curated DB Section
            contentPanel.Children.Add(new TextBlock
            {
                Text = "Curated Presets Database (Canonical / Synced)",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13
            });

            var curatedGrid = new Grid { ColumnSpacing = 12 };
            curatedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            curatedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            curatedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            curatedGrid.Children.Add(CreateMetricCard("Subjects", curatedDb.SubjectCount.ToString(), 0));
            curatedGrid.Children.Add(CreateMetricCard("Chapters", curatedDb.ChapterCount.ToString(), 1));
            curatedGrid.Children.Add(CreateMetricCard("Topics", curatedDb.TopicCount.ToString(), 2));
            contentPanel.Children.Add(curatedGrid);

            if (curatedDb.IsEmpty)
            {
                contentPanel.Children.Add(new InfoBar
                {
                    IsOpen = true,
                    Severity = InfoBarSeverity.Informational,
                    IsClosable = false,
                    Message = "Curated database is currently empty. Download the latest curated tags from the GitHub repository via Launcher Settings."
                });
            }

            contentPanel.Children.Add(new TextBlock
            {
                Text = $"Path: {curatedDb.StoragePath}",
                FontSize = 11,
                Foreground = Application.Current.Resources.TryGetValue("TextFillColorTertiaryBrush", out var ter) && ter is Brush terBrush ? terBrush : null,
                TextWrapping = TextWrapping.Wrap
            });

            // Custom DB Section
            contentPanel.Children.Add(new TextBlock
            {
                Text = "Custom Presets Database (User Imported)",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 8, 0, 0)
            });

            var customGrid = new Grid { ColumnSpacing = 12 };
            customGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            customGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            customGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            customGrid.Children.Add(CreateMetricCard("Subjects", customDb.SubjectCount.ToString(), 0));
            customGrid.Children.Add(CreateMetricCard("Chapters", customDb.ChapterCount.ToString(), 1));
            customGrid.Children.Add(CreateMetricCard("Topics", customDb.TopicCount.ToString(), 2));
            contentPanel.Children.Add(customGrid);

            contentPanel.Children.Add(new TextBlock
            {
                Text = $"Path: {customDb.StoragePath}",
                FontSize = 11,
                Foreground = Application.Current.Resources.TryGetValue("TextFillColorTertiaryBrush", out var ter2) && ter2 is Brush ter2Brush ? ter2Brush : null,
                TextWrapping = TextWrapping.Wrap
            });

            var dialog = new ContentDialog
            {
                Title = "Preset Databases Overview",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                Content = contentPanel
            };

            await dialog.ShowAsync();
        }

        private static Border CreateMetricCard(string title, string value, int column)
        {
            var card = new Border
            {
                Background = Application.Current.Resources.TryGetValue("LayerFillColorDefaultBrush", out var bg) && bg is Brush bgBrush ? bgBrush : null,
                BorderBrush = Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out var stroke) && stroke is Brush strokeBrush ? strokeBrush : null,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10)
            };
            Grid.SetColumn(card, column);

            var sp = new StackPanel { Spacing = 4 };
            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                Foreground = Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var s) && s is Brush sBrush ? sBrush : null
            });
            sp.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = Application.Current.Resources.TryGetValue("AccentTextFillColorPrimaryBrush", out var a) && a is Brush aBrush ? aBrush : null
            });
            card.Child = sp;
            return card;
        }

        private async void OnImportJsonFilesClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".json");

                IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                InitializeWithWindow.Initialize(picker, hwnd);

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    var paths = files.Select(f => f.Path);
                    var (success, failed) = CustomPresetTagDatabase.Instance.ImportJsonFiles(paths);

                    ShowStatus($"Import complete: {success} file(s) merged into custom preset database. ({failed} failed)",
                        failed == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
                    RefreshStats();
                    return;
                }
            }
            catch (Exception)
            {
                // Fallback to manual path input dialog if COM picker fails
                await ShowManualImportDialog("Enter Path to JSON File(s)", false);
                return;
            }

            ShowStatus("No files were selected.", InfoBarSeverity.Informational);
        }

        private async void OnImportFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                InitializeWithWindow.Initialize(picker, hwnd);

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    var (total, success, failed) = CustomPresetTagDatabase.Instance.ImportDirectory(folder.Path);
                    ShowStatus($"Folder scan complete: Found {total} JSON files. Successfully merged {success} into custom database ({failed} failed).",
                        success > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
                    RefreshStats();
                    return;
                }
            }
            catch (Exception)
            {
                // Fallback to manual path input dialog
                await ShowManualImportDialog("Enter Path to Folder Containing JSON Files", true);
                return;
            }

            ShowStatus("No folder was selected.", InfoBarSeverity.Informational);
        }

        private async System.Threading.Tasks.Task ShowManualImportDialog(string title, bool isFolder)
        {
            var textBox = new TextBox
            {
                PlaceholderText = isFolder ? @"C:\Presets\MySubjectFolder" : @"C:\Presets\preset.json",
                Width = 400
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = isFolder ? "Enter the directory path to recursively scan for .json files:" : "Enter full path to .json file:" },
                        textBox
                    }
                },
                PrimaryButtonText = "Import",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string path = textBox.Text.Trim();
                if (isFolder)
                {
                    if (Directory.Exists(path))
                    {
                        var (total, success, failed) = CustomPresetTagDatabase.Instance.ImportDirectory(path);
                        ShowStatus($"Folder scan complete: Found {total} JSON files. Merged {success} into custom database ({failed} failed).", InfoBarSeverity.Success);
                        RefreshStats();
                    }
                    else
                    {
                        ShowStatus($"Directory not found: {path}", InfoBarSeverity.Error);
                    }
                }
                else
                {
                    if (File.Exists(path))
                    {
                        var (success, failed) = CustomPresetTagDatabase.Instance.ImportJsonFiles(new[] { path });
                        ShowStatus($"Import complete: {success} file merged into custom database.", InfoBarSeverity.Success);
                        RefreshStats();
                    }
                    else
                    {
                        ShowStatus($"File not found: {path}", InfoBarSeverity.Error);
                    }
                }
            }
        }

        private async void OnImportZipArchiveClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".zip");

                IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    var (total, success, failed) = CustomPresetTagDatabase.Instance.ImportZipArchive(file.Path);
                    ShowStatus($"Archive import complete: Found {total} JSON files. Successfully merged {success} into custom database ({failed} failed).",
                        success > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
                    RefreshStats();
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error importing zip archive: {ex.Message}", InfoBarSeverity.Error);
                return;
            }

            ShowStatus("No zip archive was selected.", InfoBarSeverity.Informational);
        }

        private async void OnFormatCuratedDatabaseClicked(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Format Curated Tags Database?",
                Content = "This will format and clear all curated presets to an empty state. Custom tags will remain untouched. Curated presets can be re-synced from GitHub at any time.",
                PrimaryButtonText = "Format Curated Tags",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                PresetTagDatabase.Instance.FormatDatabase();
                ShowStatus("Curated tags database formatted and reset to empty state.", InfoBarSeverity.Warning);
                RefreshStats();
            }
        }

        private async void OnFormatCustomDatabaseClicked(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Format Custom Tags Database?",
                Content = "This will format and delete all custom presets you have imported or created. Curated tags will remain untouched. This action cannot be undone.",
                PrimaryButtonText = "Format Custom Tags",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                CustomPresetTagDatabase.Instance.FormatDatabase();
                ShowStatus("Custom tags database formatted and reset to empty state.", InfoBarSeverity.Warning);
                RefreshStats();
            }
        }

        private async void OnFormatAllDatabasesClicked(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Format Both Preset Databases?",
                Content = "This will format and reset BOTH Curated and Custom preset databases to start completely afresh. This action cannot be undone.",
                PrimaryButtonText = "Format Both Databases",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                CustomPresetTagDatabase.Instance.FormatDatabase();
                PresetTagDatabase.Instance.FormatDatabase();
                ShowStatus("Both Curated and Custom databases formatted and reset to empty state.", InfoBarSeverity.Warning);
                RefreshStats();
            }
        }

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            NotificationCard.Show(message, severity);
        }
    }
}

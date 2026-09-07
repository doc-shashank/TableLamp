using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            RefreshStats();

            ImportJsonFilesButton.Click += OnImportJsonFilesClicked;
            ImportFolderButton.Click += OnImportFolderClicked;
            FormatDatabaseButton.Click += OnFormatDatabaseClicked;
            RestoreStartersButton.Click += OnRestoreStartersClicked;
        }

        private void RefreshStats()
        {
            var db = PresetTagDatabase.Instance;
            SubjectsCountText.Text = db.SubjectCount.ToString();
            ChaptersCountText.Text = db.ChapterCount.ToString();
            TopicsCountText.Text = db.TopicCount.ToString();
            StoragePathText.Text = $"Database Path: {db.StoragePath}";
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
                    var (success, failed) = PresetTagDatabase.Instance.ImportJsonFiles(paths);

                    ShowStatus($"Import complete: {success} file(s) merged into preset database. ({failed} failed)",
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
                    var (total, success, failed) = PresetTagDatabase.Instance.ImportDirectory(folder.Path);
                    ShowStatus($"Folder scan complete: Found {total} JSON files. Successfully merged {success} into database ({failed} failed).",
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
                        var (total, success, failed) = PresetTagDatabase.Instance.ImportDirectory(path);
                        ShowStatus($"Folder scan complete: Found {total} JSON files. Merged {success} ({failed} failed).", InfoBarSeverity.Success);
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
                        var (success, failed) = PresetTagDatabase.Instance.ImportJsonFiles(new[] { path });
                        ShowStatus($"Import complete: {success} file merged.", InfoBarSeverity.Success);
                        RefreshStats();
                    }
                    else
                    {
                        ShowStatus($"File not found: {path}", InfoBarSeverity.Error);
                    }
                }
            }
        }

        private async void OnFormatDatabaseClicked(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Format Preset Database?",
                Content = "This will remove all subjects, chapters, and topics from the database to start completely afresh. This action cannot be undone.",
                PrimaryButtonText = "Format Database",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                PresetTagDatabase.Instance.FormatDatabase();
                ShowStatus("Database formatted and reset to empty state.", InfoBarSeverity.Warning);
                RefreshStats();
            }
        }

        private async void OnRestoreStartersClicked(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Restore Starter Presets?",
                Content = "This will replace current presets with canonical starter presets (Robins Physiology & Guyton Medical Physiology).",
                PrimaryButtonText = "Restore",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                PresetTagDatabase.Instance.ResetToStarterPresets();
                ShowStatus("Canonical starter presets restored successfully.", InfoBarSeverity.Success);
                RefreshStats();
            }
        }

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            SettingsStatusInfoBar.Message = message;
            SettingsStatusInfoBar.Severity = severity;
            SettingsStatusInfoBar.IsOpen = true;
        }
    }
}

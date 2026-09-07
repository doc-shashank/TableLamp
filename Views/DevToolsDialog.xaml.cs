using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TableLamp.Models;
using TableLamp.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace TableLamp.Views
{
    public sealed partial class DevToolsDialog : ContentDialog
    {
        private readonly PresetTagGenerator _generator = new();
        private Dictionary<string, PresetSubject> _tree = new(StringComparer.OrdinalIgnoreCase);
        private readonly TextBox[] _fields;

        public DevToolsDialog()
        {
            this.InitializeComponent();

            _fields = new[]
            {
                FieldSubjectKey,
                FieldShortName,
                FieldFullName,
                FieldEdition,
                FieldChapterName,
                FieldTopicName,
                FieldStartPage,
                FieldEndPage
            };

            SetupLineSelectionAndNavigation();

            // Load existing presets from database
            string currentJson = PresetTagDatabase.Instance.ExportJson();
            JsonOutputBox.Text = currentJson;
            _tree = _generator.Parse(currentJson);

            AddEntryButton.Click += OnAddEntryClicked;
            LoadStarterButton.Click += (s, e) =>
            {
                JsonOutputBox.Text = _generator.CreateStarterPresetJson();
                _tree = _generator.Parse(JsonOutputBox.Text);
                ShowStatus("Canonical starter presets loaded.", InfoBarSeverity.Success);
            };

            CopyJsonButton.Click += OnCopyJsonClicked;
            ExportJsonButton.Click += OnExportJsonClicked;
            PrimaryButtonClick += OnApplyToDatabaseClicked;
        }

        private void SetupLineSelectionAndNavigation()
        {
            for (int i = 0; i < _fields.Length; i++)
            {
                int index = i;
                var field = _fields[i];

                field.GotFocus += (s, e) =>
                {
                    if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? brush) && brush is Brush b)
                    {
                        field.BorderBrush = b;
                    }
                };

                field.LostFocus += (s, e) =>
                {
                    if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? brush) && brush is Brush b)
                    {
                        field.BorderBrush = b;
                    }
                };

                field.KeyDown += (s, e) =>
                {
                    if (e.Key == VirtualKey.Up && index > 0)
                    {
                        _fields[index - 1].Focus(FocusState.Programmatic);
                        e.Handled = true;
                    }
                    else if (e.Key == VirtualKey.Down && index < _fields.Length - 1)
                    {
                        _fields[index + 1].Focus(FocusState.Programmatic);
                        e.Handled = true;
                    }
                };
            }
        }

        private void OnAddEntryClicked(object sender, RoutedEventArgs e)
        {
            string subjKey = string.IsNullOrWhiteSpace(FieldSubjectKey.Text) ? "Subject1" : FieldSubjectKey.Text.Trim();
            string shortName = FieldShortName.Text.Trim();
            string fullName = FieldFullName.Text.Trim();
            string edition = FieldEdition.Text.Trim();
            string chName = string.IsNullOrWhiteSpace(FieldChapterName.Text) ? "General" : FieldChapterName.Text.Trim();
            string topName = string.IsNullOrWhiteSpace(FieldTopicName.Text) ? "Overview" : FieldTopicName.Text.Trim();
            string startPage = FieldStartPage.Text.Trim();
            string endPage = FieldEndPage.Text.Trim();

            if (!_tree.TryGetValue(subjKey, out var subj))
            {
                subj = new PresetSubject
                {
                    Key = subjKey,
                    short_name = shortName,
                    full_name = fullName,
                    edition = edition,
                    Chapters = new Dictionary<string, PresetChapter>(StringComparer.OrdinalIgnoreCase)
                };
                _tree[subjKey] = subj;
            }
            else
            {
                if (!string.IsNullOrEmpty(shortName)) subj.short_name = shortName;
                if (!string.IsNullOrEmpty(fullName)) subj.full_name = fullName;
                if (!string.IsNullOrEmpty(edition)) subj.edition = edition;
            }

            string chKey = $"Chapter {subj.Chapters.Count + 1}";
            var existingCh = System.Linq.Enumerable.FirstOrDefault(subj.Chapters.Values, c => c.name.Equals(chName, StringComparison.OrdinalIgnoreCase));
            if (existingCh != null)
            {
                chKey = existingCh.Key;
            }
            else
            {
                existingCh = new PresetChapter
                {
                    Key = chKey,
                    name = chName,
                    Topics = new Dictionary<string, PresetTopic>(StringComparer.OrdinalIgnoreCase)
                };
                subj.Chapters[chKey] = existingCh;
            }

            string topKey = $"topic_{existingCh.Topics.Count + 1}";
            existingCh.Topics[topKey] = new PresetTopic
            {
                Key = topKey,
                name = topName,
                start_page = startPage,
                end_page = endPage
            };

            JsonOutputBox.Text = _generator.GenerateJson(_tree);
            ShowStatus($"Entry for '{subjKey} -> {chName} -> {topName}' added and JSON regenerated.", InfoBarSeverity.Success);
        }

        private void OnCopyJsonClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var package = new DataPackage();
                package.SetText(JsonOutputBox.Text);
                Clipboard.SetContent(package);
                ShowStatus("Presets JSON copied to clipboard!", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to copy: {ex.Message}", InfoBarSeverity.Warning);
            }
        }

        private void OnExportJsonClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string exportPath = Path.Combine(localFolder, "TableLamp", "presets_export.json");
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
                File.WriteAllText(exportPath, JsonOutputBox.Text);
                ShowStatus($"Exported presets file to: {exportPath}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Export error: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void OnApplyToDatabaseClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                PresetTagDatabase.Instance.ReloadFromJson(JsonOutputBox.Text);
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                ShowStatus($"Failed to apply: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}

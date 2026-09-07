using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace TableLamp.Views
{
    public sealed partial class SingleElementEditDialog : ContentDialog
    {
        public string ElementMode { get; } // "Text" or "Image"
        public string? ResultText { get; private set; }
        public string? ResultImagePath { get; private set; }

        public SingleElementEditDialog(string mode, string? initialValue = null)
        {
            this.InitializeComponent();
            ElementMode = mode;

            if (mode == "Text")
            {
                Title = "Edit Text Element";
                PrimaryButtonText = "Save Text";
                TextEditingPanel.Visibility = Visibility.Visible;
                ImageEditingPanel.Visibility = Visibility.Collapsed;

                if (!string.IsNullOrEmpty(initialValue))
                {
                    EditContentTextBox.Text = initialValue;
                }
            }
            else // "Image"
            {
                Title = "Edit Image Element";
                PrimaryButtonText = "Save Image";
                TextEditingPanel.Visibility = Visibility.Collapsed;
                ImageEditingPanel.Visibility = Visibility.Visible;

                if (!string.IsNullOrEmpty(initialValue))
                {
                    EditImagePathBox.Text = initialValue;
                    UpdateImagePreview(initialValue);
                }

                EditImagePathBox.TextChanged += (s, e) => UpdateImagePreview(EditImagePathBox.Text.Trim());
                BrowseButton.Click += OnBrowseClicked;
                ClearButton.Click += (s, e) =>
                {
                    EditImagePathBox.Text = "";
                    PreviewImage.Source = null;
                    ImagePreviewBorder.Visibility = Visibility.Collapsed;
                };
            }

            PrimaryButtonClick += OnPrimaryButtonClick;
        }

        private async void OnBrowseClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".gif");
                picker.FileTypeFilter.Add(".bmp");
                picker.FileTypeFilter.Add(".webp");

                IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    EditImagePathBox.Text = file.Path;
                    UpdateImagePreview(file.Path);
                }
            }
            catch (Exception)
            {
                // Fallback: user types or pastes the path
            }
        }

        private void UpdateImagePreview(string pathOrUri)
        {
            if (string.IsNullOrWhiteSpace(pathOrUri))
            {
                ImagePreviewBorder.Visibility = Visibility.Collapsed;
                PreviewImage.Source = null;
                return;
            }

            try
            {
                BitmapImage bitmap;
                if (Uri.TryCreate(pathOrUri, UriKind.Absolute, out var uri))
                {
                    bitmap = new BitmapImage(uri);
                }
                else if (File.Exists(pathOrUri))
                {
                    bitmap = new BitmapImage(new Uri(pathOrUri));
                }
                else
                {
                    bitmap = new BitmapImage(new Uri($"file:///{pathOrUri.Replace('\\', '/')}"));
                }

                PreviewImage.Source = bitmap;
                ImagePreviewBorder.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                ImagePreviewBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ElementMode == "Text")
            {
                ResultText = EditContentTextBox.Text.Trim();
            }
            else
            {
                ResultImagePath = EditImagePathBox.Text.Trim();
            }
        }
    }
}

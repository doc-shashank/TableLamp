using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using TableLamp.Models;

namespace TableLamp.Views
{
    public sealed partial class QuestionPopUp : ContentDialog
    {
        private readonly Tag? _tag;
        private readonly SimpleQuestion? _existingQuestion;

        public SimpleQuestion? ResultQuestion { get; private set; }

        public QuestionPopUp(Tag? tag, SimpleQuestion? existingQuestion = null)
        {
            this.InitializeComponent();

            _tag = tag;
            _existingQuestion = existingQuestion;

            if (existingQuestion != null)
            {
                Title = "Edit Question";
                PrimaryButtonText = "Update Question";

                string? text = existingQuestion.TextElement?.simple_text ?? existingQuestion.TextElement?.formatted_text;
                if (!string.IsNullOrEmpty(text))
                {
                    QuestionTextBox.Text = text;
                }

                string? image = existingQuestion.ImageElement?.simple_image?.ToString();
                if (!string.IsNullOrEmpty(image))
                {
                    ImagePathBox.Text = image;
                    UpdateImagePreview(image);
                }
            }

            ImagePathBox.TextChanged += (s, e) => UpdateImagePreview(ImagePathBox.Text.Trim());
            BrowseImageButton.Click += OnBrowseImageClicked;
            RemoveImageButton.Click += (s, e) =>
            {
                ImagePathBox.Text = "";
                ImagePreviewContainer.Visibility = Visibility.Collapsed;
                PreviewImage.Source = null;
            };

            PrimaryButtonClick += OnPrimaryButtonClick;
        }

        private async void OnBrowseImageClicked(object sender, RoutedEventArgs e)
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

                // WinUI 3 Window handle association
                IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    ImagePathBox.Text = file.Path;
                    UpdateImagePreview(file.Path);
                }
            }
            catch (Exception)
            {
                // Fallback: user can manually type path or URL
            }
        }

        private void UpdateImagePreview(string pathOrUri)
        {
            if (string.IsNullOrWhiteSpace(pathOrUri))
            {
                ImagePreviewContainer.Visibility = Visibility.Collapsed;
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
                ImagePreviewContainer.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                ImagePreviewContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string text = QuestionTextBox.Text.Trim();
            string imagePath = ImagePathBox.Text.Trim();

            bool hasText = !string.IsNullOrWhiteSpace(text);
            bool hasImage = !string.IsNullOrWhiteSpace(imagePath);

            // The elements in a SimpleQuestion are not mutually inclusive (independent: can have either or both)
            if (!hasText && !hasImage)
            {
                args.Cancel = true;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;

            var elements = new List<Element>();
            if (hasText)
            {
                elements.Add(Element.FromText(text));
            }
            if (hasImage)
            {
                elements.Add(new Element { simple_image = imagePath });
            }

            if (_existingQuestion != null)
            {
                _existingQuestion.question_element_array = elements.ToArray();
                ResultQuestion = _existingQuestion;
            }
            else
            {
                ResultQuestion = new SimpleQuestion(elements.ToArray(), _tag, isEditable: true);
            }
        }
    }
}

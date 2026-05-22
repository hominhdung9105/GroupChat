using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GroupChat_Client.Views
{
    public partial class ImageViewerWindow : Window
    {
        public ImageViewerWindow(string imagePath)
        {
            InitializeComponent();
            LoadImage(imagePath);
        }

        private void LoadImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            // Nếu kích thước ảnh lớn hơn kích thước khống chế của cửa sổ, ta bật chế độ Uniform để thu nhỏ an toàn
            if (bitmap.PixelWidth > 1140 || bitmap.PixelHeight > 840)
            {
                PreviewImage.Stretch = System.Windows.Media.Stretch.Uniform;
                PreviewImage.Width = Math.Min(bitmap.PixelWidth, 1140);
                PreviewImage.Height = Math.Min(bitmap.PixelHeight, 840);
            }
            else
            {
                PreviewImage.Stretch = System.Windows.Media.Stretch.None;
            }

            PreviewImage.Source = bitmap;
        }
    }
}

using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GroupChat_Client.Views
{
    public partial class ImageViewerWindow : Window
    {
        private readonly string _imagePath;

        public ImageViewerWindow(string imagePath)
        {
            InitializeComponent();
            _imagePath = imagePath;
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

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_imagePath) || !File.Exists(_imagePath))
            {
                MessageBox.Show("Không tìm thấy ảnh để tải.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var extension = Path.GetExtension(_imagePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*",
                DefaultExt = extension,
                FileName = Path.GetFileName(_imagePath)
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                File.Copy(_imagePath, dialog.FileName, true);
                MessageBox.Show("Đã tải ảnh thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

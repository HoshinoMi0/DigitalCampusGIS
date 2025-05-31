using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Navigation;

namespace DigitalCampusGIS
{
  public partial class PoiDetailWindow : Window
  {
    private string _appDirectory = null;
    private const string PhotoSubfolder = "PoiImages";
    private List<BitmapImage> _poiImages = new List<BitmapImage>(); // 存储加载的图片

    public PoiDetailWindow()
    {
      InitializeComponent();
      _appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    // 公共方法，用于从 MainWindow 传递数据并显示
    public void LoadPoiDetails(PoiData poi)
    {
      if (poi == null) return;

      // 填充文本信息
      poiDetailNameTextBlock.Text = poi.Name;
      poiDetailDescTextBlock.Text = string.IsNullOrEmpty(poi.Description) ? "(暂无简介)" : poi.Description;

      // --- 修改: 加载并显示图片 ---
      _poiImages.Clear(); // 清空旧图片
      thumbnailListBox.ItemsSource = null; // 清空绑定
      largeImageView.Source = null; // 清空大图

      if (poi.ImagePaths != null && poi.ImagePaths.Any() && !string.IsNullOrEmpty(_appDirectory))
      {
        string photoDirectory = Path.Combine(_appDirectory, PhotoSubfolder);
        foreach (string imageName in poi.ImagePaths)
        {
          string fullPath = Path.Combine(photoDirectory, imageName);
          if (File.Exists(fullPath))
          {
            try
            {
              BitmapImage bitmap = new BitmapImage();
              bitmap.BeginInit();
              bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
              bitmap.CacheOption = BitmapCacheOption.OnLoad; // 加载时缓存
              bitmap.EndInit();
              _poiImages.Add(bitmap); // 添加到列表
            }
            catch (Exception ex)
            {
              Console.WriteLine($"Error loading image {fullPath} for detail window: {ex.Message}");
              // 可以考虑添加一个占位符图片表示加载失败
            }
          }
        }
      }

      // 将 BitmapImage 列表绑定到缩略图 ListBox
      thumbnailListBox.ItemsSource = _poiImages;

      // 如果有图片，默认选中第一张并显示大图
      if (_poiImages.Any())
      {
        thumbnailListBox.SelectedIndex = 0;
        largeImageView.Source = _poiImages[0];
      }
      // ---------------------------

      // --- 加载并显示相关链接 ---
      if (poi.RelatedLinks != null && poi.RelatedLinks.Any())
      {
        relatedLinksItemsControl.ItemsSource = poi.RelatedLinks;
        relatedLinksItemsControl.Visibility = Visibility.Visible;
        noLinksTextBlock.Visibility = Visibility.Collapsed;
      }
      else
      {
        relatedLinksItemsControl.ItemsSource = null;
        relatedLinksItemsControl.Visibility = Visibility.Collapsed;
        noLinksTextBlock.Visibility = Visibility.Visible; // 显示 "暂无链接"
      }
      // ---------------------------
    }

    // --- 缩略图选择改变事件处理 ---
    private void ThumbnailListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      // 当用户在 ListBox 中选择不同的缩略图时，更新大图预览区域
      if (thumbnailListBox.SelectedItem is BitmapImage selectedImage)
      {
        largeImageView.Source = selectedImage;
      }
    }

    //超链接点击事件处理
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
      // 在默认浏览器中打开链接
      try
      {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
      }
      catch (Exception ex)
      {
        MessageBox.Show($"无法打开链接: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
      }
      e.Handled = true; // 标记事件已处理
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }
  }
}


using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DigitalCampusGIS
{
  public partial class CampusManagementWindow : Window, INotifyPropertyChanged // 实现 INotifyPropertyChanged 用于数据绑定
  {
    private ObservableCollection<CampusData> _campuses; // 使用 ObservableCollection 以便 ListView 自动更新
    private CampusData _selectedCampus;
    private string _appDirectory;
    private const string OverlaySubfolder = "Overlays"; // 与 MainWindow 一致

    // 公开属性，用于数据绑定
    public ObservableCollection<CampusData> Campuses
    {
      get => _campuses;
      set { _campuses = value; OnPropertyChanged(); } // OnPropertyChanged 通知 UI 更新
    }

    public CampusData SelectedCampus
    {
      get => _selectedCampus;
      set
      {
        _selectedCampus = value;
        OnPropertyChanged(); // 通知 UI 当前选中的项已更改
        OnPropertyChanged(nameof(IsCampusSelected)); // 通知 IsCampusSelected 属性已更改
        OnPropertyChanged(nameof(HasOverlayImage)); // 通知 HasOverlayImage 属性已更改
      }
    }

    // 用于控制 UI 元素启用状态的属性
    public bool IsCampusSelected => SelectedCampus != null;

    public bool HasOverlayImage => SelectedCampus != null && !string.IsNullOrEmpty(SelectedCampus.OverlayImageFileName);

    public List<CampusData> ResultCampuses { get; private set; }

    public CampusManagementWindow(List<CampusData> initialCampuses) // 接收来自 MainWindow 的列表
    {
      InitializeComponent();
      DataContext = this; // 设置 DataContext 为自身，以便 XAML 绑定
      _appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      Campuses = new ObservableCollection<CampusData>(
          initialCampuses.Select(c => CloneCampus(c)) // 克隆每个对象，避免直接修改原始列表
      );

      if (Campuses.Any())
      {
        campusListView.SelectedIndex = 0; // 默认选中第一个
      }
    }

    private CampusData CloneCampus(CampusData original)
    {
      if (original == null) return null;
      var serialized = JsonConvert.SerializeObject(original);
      return JsonConvert.DeserializeObject<CampusData>(serialized);
    }

    // --- 事件处理 ---
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      // 当 SelectedCampus 改变时，手动触发相关属性的通知
      if (propertyName == nameof(SelectedCampus))
      {
        OnPropertyChanged(nameof(IsCampusSelected));
        OnPropertyChanged(nameof(HasOverlayImage));
      }
      // 当 OverlayImageFileName 改变时 (通过绑定或代码)，更新 HasOverlayImage
      if (propertyName == nameof(SelectedCampus.OverlayImageFileName)) // 需要监听子属性变化，或者在设置文件名后手动调用
      {
        OnPropertyChanged(nameof(HasOverlayImage));
      }
    }

    private void CampusListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      OnPropertyChanged(nameof(SelectedCampus.OverlayImageFileName));
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
      // 创建一个新的默认校区对象
      var newCampus = new CampusData
      {
        Name = "新校区",
        CenterLon = 116.404, 
        CenterLat = 39.915,
        DefaultZoom = 15
        // 其他属性为默认值 (null 或 0)
      };
      Campuses.Add(newCampus); // 添加到集合
      campusListView.SelectedItem = newCampus; // 选中新添加的项
      nameTextBox.Focus(); // 将焦点设置到名称输入框
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
      if (SelectedCampus != null)
      {
        MessageBoxResult result = MessageBox.Show($"确定要删除校区 \"{SelectedCampus.Name}\" 吗？\n（注意：这不会删除关联的叠加图像文件）",
                                                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
          Campuses.Remove(SelectedCampus); // 从集合中移除
          SelectedCampus = null; // 清除选中状态
        }
      }
    }

    private void SelectOverlayImageButton_Click(object sender, RoutedEventArgs e)
    {
      if (SelectedCampus == null) return; // 必须先选中一个校区

      OpenFileDialog openFileDialog = new OpenFileDialog();
      openFileDialog.Filter = "图像文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*";
      openFileDialog.Title = "选择叠加图像文件";

      bool? result = openFileDialog.ShowDialog();

      if (result == true)
      {
        string selectedFilePath = openFileDialog.FileName;
        string fileName = Path.GetFileName(selectedFilePath);

        try
        {
          if (string.IsNullOrEmpty(_appDirectory)) throw new DirectoryNotFoundException("无法获取应用程序目录。");
          string overlayDirectory = Path.Combine(_appDirectory, OverlaySubfolder);
          Directory.CreateDirectory(overlayDirectory); // 确保目录存在
          string destinationPath = Path.Combine(overlayDirectory, fileName);

          File.Copy(selectedFilePath, destinationPath, true); // 复制并覆盖
          Console.WriteLine($"Overlay image copied successfully to: {destinationPath}");

          // 更新选中校区的叠加图像文件名
          SelectedCampus.OverlayImageFileName = fileName;
          // 手动触发绑定更新 (如果 XAML 中的 TextBlock 绑定没有自动更新)
          OnPropertyChanged(nameof(SelectedCampus)); // 通知整个 SelectedCampus 更新可能触发子属性更新
          OnPropertyChanged(nameof(HasOverlayImage)); // 确保相关 UI 状态更新
          overlayImagePathTextBlock.Foreground = Brushes.Black; // 更新文本颜色

        }
        catch (Exception ex)
        {
          MessageBox.Show($"处理叠加图像时出错: {ex.Message}", "图像错误", MessageBoxButton.OK, MessageBoxImage.Error);
          // 出错时不清空选择，让用户知道出了问题
          overlayImagePathTextBlock.Foreground = Brushes.Red;
        }
      }
    }

    private void ClearOverlayImageButton_Click(object sender, RoutedEventArgs e)
    {
      if (SelectedCampus != null)
      {
        SelectedCampus.OverlayImageFileName = null;
        OnPropertyChanged(nameof(SelectedCampus)); // 通知更新
        OnPropertyChanged(nameof(HasOverlayImage));
        overlayImagePathTextBlock.Foreground = Brushes.Gray;
      }
    }

    private bool ValidateInput()
    {
      if (SelectedCampus == null) return true; // 没有选中项，无需验证

      // 验证名称不能为空
      if (string.IsNullOrWhiteSpace(SelectedCampus.Name))
      {
        MessageBox.Show("校区名称不能为空。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        nameTextBox.Focus();
        return false;
      }

      // 验证经纬度和缩放级别是否为有效数字
      if (!double.TryParse(lonTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ||
          !double.TryParse(latTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ||
          !int.TryParse(zoomTextBox.Text, out _))
      {
        MessageBox.Show("中心点经纬度和缩放级别必须是有效的数字。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }
      // 添加更详细的范围验证
      if (SelectedCampus.CenterLon < -180 || SelectedCampus.CenterLon > 180 || SelectedCampus.CenterLat < -90 || SelectedCampus.CenterLat > 90)
      {
        MessageBox.Show("中心点经纬度超出有效范围 (-180~180, -90~90)。", "坐标错误", MessageBoxButton.OK, MessageBoxImage.Warning); return false;
      }
      if (SelectedCampus.DefaultZoom < 3 || SelectedCampus.DefaultZoom > 21) // 百度地图缩放范围通常是 3-19/21
      {
        MessageBox.Show("默认缩放级别超出有效范围 (通常为 3-21)。", "缩放错误", MessageBoxButton.OK, MessageBoxImage.Warning); return false;
      }


      // 如果选择了叠加图像，则验证边界坐标
      if (!string.IsNullOrEmpty(SelectedCampus.OverlayImageFileName))
      {
        if (!double.TryParse(swLonTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double swLon) ||
            !double.TryParse(swLatTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double swLat) ||
            !double.TryParse(neLonTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double neLon) ||
            !double.TryParse(neLatTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double neLat))
        {
          MessageBox.Show("选择了叠加图像时，必须输入有效的数字作为边界坐标。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
          return false;
        }
        // 添加边界坐标逻辑验证
        if (swLon >= neLon) { MessageBox.Show("叠加层西南角经度必须小于东北角经度。", "坐标错误", MessageBoxButton.OK, MessageBoxImage.Warning); swLonTextBox.Focus(); return false; }
        if (swLat >= neLat) { MessageBox.Show("叠加层西南角纬度必须小于东北角纬度。", "坐标错误", MessageBoxButton.OK, MessageBoxImage.Warning); swLatTextBox.Focus(); return false; }
        if (swLon < -180 || swLon > 180 || neLon < -180 || neLon > 180 || swLat < -90 || swLat > 90 || neLat < -90 || neLat > 90) { MessageBox.Show("叠加层经纬度超出有效范围 (-180~180, -90~90)。", "坐标错误", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }

        // 确保值被正确赋回给 SelectedCampus (如果绑定是 OneWay 或有问题)
        SelectedCampus.SwLon = swLon;
        SelectedCampus.SwLat = swLat;
        SelectedCampus.NeLon = neLon;
        SelectedCampus.NeLat = neLat;
      }


      return true; // 所有验证通过
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
      // 在保存前进行最后一次验证
      if (!ValidateInput())
      {
        return; // 验证失败则不保存
      }

      // 检查是否有重名校区
      var duplicate = Campuses.GroupBy(c => c.Name.Trim())
                              .Where(g => g.Count() > 1)
                              .Select(g => g.Key)
                              .FirstOrDefault();
      if (duplicate != null)
      {
        MessageBox.Show($"存在重复的校区名称: '{duplicate}'。请修改后再保存。", "名称重复", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }


      // 将当前编辑的 ObservableCollection 转换回 List<CampusData>
      ResultCampuses = Campuses.ToList();

      this.DialogResult = true; // 设置 DialogResult 为 true
      this.Close(); // 关闭窗口
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
      this.DialogResult = false; // 设置 DialogResult 为 false
      this.Close(); // 关闭窗口
    }
  }
}




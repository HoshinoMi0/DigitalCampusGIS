using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices; 
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Reflection;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Globalization;

namespace DigitalCampusGIS
{

  public class CampusData
  {
    public string Name { get; set; } // 校区名称
    public double CenterLon { get; set; } // 中心点经度
    public double CenterLat { get; set; } // 中心点纬度
    public int DefaultZoom { get; set; } // 默认缩放级别

    // 可选的叠加层信息
    public string OverlayImageFileName { get; set; } // 叠加图片文件名 (null or empty if no overlay)
    public double SwLon { get; set; } // 西南角经度 (only if OverlayImageFileName is set)
    public double SwLat { get; set; } // 西南角纬度 (only if OverlayImageFileName is set)
    public double NeLon { get; set; } // 东北角经度 (only if OverlayImageFileName is set)
    public double NeLat { get; set; } // 东北角纬度 (only if OverlayImageFileName is set)

    // 添加一个属性方便 ComboBox 显示
    public string DisplayName => Name;
  }

  public class WebMessage
  {
    public string Type { get; set; } // 消息类型，例如 "poiClick"
    public string Id { get; set; }   // POI 的 ID (Guid 字符串)
  }

  public class PoiData
  {
    public Guid Id { get; set; }
    public string Name { get; set; }
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public string Description { get; set; }
    public string IconPath { get; set; }
    public List<string> ImagePaths { get; set; } // 存储图片文件名
    public string Category { get; set; }

    // --- 添加: 用于存储相关链接的列表 ---
    public List<string> RelatedLinks { get; set; }
    // ------------------------------------

    public PoiData()
    {
      Id = Guid.NewGuid();
      ImagePaths = new List<string>(); // 初始化列表
      RelatedLinks = new List<string>(); // 初始化列表
    }
  }

  public class LayerItem : INotifyPropertyChanged
  {
    private bool _isVisible = true; // 默认可见
    public string CategoryName { get; set; }

    public bool IsVisible
    {
      get => _isVisible;
      set
      {
        if (_isVisible != value)
        {
          _isVisible = value;
          OnPropertyChanged();
          // 触发事件，通知 MainWindow 调用 JS
          VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler VisibilityChanged; // 添加事件

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }

  public partial class MainWindow : Window
  {
    private ObservableCollection<PoiData> currentDisplayedPois;// --- 当前在地图上显示的 POI 数据集合 ---
    private bool isEditing = false;// --- 标记当前是否处于编辑 POI 的状态 ---
    private Guid editingPoiId = Guid.Empty;// --- 存储当前正在编辑的 POI 的 ID ---
    private string _currentSelectedIconFileName = null;// --- 存储当前选中的自定义图标的文件名 ---
    private bool _iconManuallySelected = false; // 标志用户是否手动选择了图标
    private const string IconSubfolder = "Icons";// --- 定义存储自定义图标的子文件夹名称 ---
    private const string PhotoSubfolder = "PoiImages";// --- 定义存储 POI 照片的子文件夹名称 ---
    private const string OverlaySubfolder = "Overlays";// --- 定义存储校区叠加图的子文件夹名称 ---
    private const string VirtualHostName = "appassets.example";// --- 定义 WebView2 虚拟主机名 ---
    private List<string> _newPoiPhotoFileNames = new List<string>();// --- 存储为新 POI 添加的照片文件名列表 ---
    private string _appDirectory = null;// --- 存储应用程序的运行目录路径 ---
    private bool _isSatelliteView = false;// --- 标记当前地图视图是否为卫星视图 ---
    private readonly List<string> _predefinedCategories = new List<string> { "教学楼", "食堂餐馆", "宿舍", "运动场馆", "行政楼", "生活服务", "地标景点", "未分类", "其他" };// --- 预定义的 POI 类别列表 ---
    private const string AllCategoriesFilter = "显示所有类别";// --- 定义在类别筛选下拉框中表示“显示所有”的文本 ---
    private List<CampusData> _campuses;//--- 存储校区数据的列表 ---
    private const string CampusesFileName = "campuses.json"; //--- 校区数据文件名 ---
    private readonly Dictionary<string, string> _categoryIconMap = new Dictionary<string, string>//--- 类别到图标文件名的映射字典 ---
        {
            { "教学楼", "building.png" },
            { "食堂餐馆", "restaurant.png" },
            { "宿舍", "dorm.png" },
            { "运动场馆", "sports.png" },
            { "行政楼", "office.png" },
            { "生活服务", "service.png" },
            { "地标景点", "landmark.png" },
            { "未分类", null }, // 未分类或没有对应图标，则使用 null 或 string.Empty
            { "其他", null }    // 其他类别也没有默认图标
            // 添加更多映射...
        }; // 类别到图标文件名的映射字典
    public ObservableCollection<LayerItem> PoiLayers { get; set; }

    public MainWindow()
    {
      InitializeComponent();
      currentDisplayedPois = new ObservableCollection<PoiData>();
      poiListView.ItemsSource = currentDisplayedPois;
      _appDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

      //初始化校区数据和选择器 ---
      LoadCampusData(); //从文件加载校区数据
      InitializeCampusSelector(); // 填充 ComboBox
                                  // ------------------------------------

      InitializeCategoryComboBoxes();
      PoiLayers = new ObservableCollection<LayerItem>();
      InitializePoiLayers(); // 根据预定义类别初始化
    }

    #region 初始化模块

    #region 初始化POI列表
    private void InitializePoiLayers()
    {
      PoiLayers.Clear(); 
      foreach (var category in _predefinedCategories.OrderBy(c => c)) // 按名称排序
      {
        var layerItem = new LayerItem { CategoryName = category, IsVisible = true };
        layerItem.VisibilityChanged += LayerVisibilityChanged; // 订阅事件
        PoiLayers.Add(layerItem);
      }
    }
    #endregion

    #region 初始化默认校区数据
    private void InitializeDefaultCampusData()
    {
      _campuses = new List<CampusData>
            {
                new CampusData { Name = "奉贤校区", CenterLon = 121.523025, CenterLat = 30.842458, DefaultZoom = 17, OverlayImageFileName = "SHNU.png", SwLon = 121.509758, SwLat = 30.834327, NeLon = 121.53465, NeLat = 30.849761 },
                new CampusData { Name = "徐汇校区", CenterLon = 121.424846, CenterLat = 31.16724, DefaultZoom = 16, OverlayImageFileName = null }
            };
      Console.WriteLine("Initialized with default campus data.");
    }
    #endregion

    #region 初始化校区选择下拉框
    private void InitializeCampusSelector()
    {
      campusSelectorComboBox.ItemsSource = _campuses;
      campusSelectorComboBox.DisplayMemberPath = "DisplayName";
      if (_campuses != null && _campuses.Any())
      {
        campusSelectorComboBox.SelectedIndex = 0;
      }
      else
      {
        campusSelectorComboBox.ItemsSource = null;
      }
    }
    #endregion

    #region 初始化 POI 类别相关的下拉框
    private void InitializeCategoryComboBoxes()
    {
      // 填充筛选 ComboBox
      categoryFilterComboBox.Items.Add(AllCategoriesFilter); // "全部" 选项
      foreach (string category in _predefinedCategories)
      {
        categoryFilterComboBox.Items.Add(category);
      }
      categoryFilterComboBox.SelectedItem = AllCategoriesFilter; // 默认选中 "全部"

      // 填充编辑 ComboBox
      foreach (string category in _predefinedCategories)
      {
        poiCategoryComboBox.Items.Add(category);

        poiCategoryComboBox.SelectedIndex = 0;
      }
    }
    #endregion

    #endregion

    #region 方法模块

    #region 管理POI分类的方法
    private async Task SetPoiCategoryVisibility(string category, bool isVisible)
    {
      if (webView?.CoreWebView2 == null) return;

      // 对类别名称进行 JSON 序列化以正确处理特殊字符
      string categoryJson = JsonConvert.SerializeObject(category);
      string script = $"setPoiCategoryVisibility({categoryJson}, {isVisible.ToString().ToLowerInvariant()});";
      Console.WriteLine($"Executing script: {script}");
      try
      {
        await webView.CoreWebView2.ExecuteScriptAsync(script);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error executing SetPoiCategoryVisibility script: {ex.Message}");
      }
    }
    #endregion

    #region 异步初始化 WebView2 的方法
    async System.Threading.Tasks.Task InitializeWebViewAsync()
    {
      try
      {
        var environment = await CoreWebView2Environment.CreateAsync(null, null, null);
        await webView.EnsureCoreWebView2Async(environment);

        if (!string.IsNullOrEmpty(_appDirectory))
        {
          Directory.CreateDirectory(System.IO.Path.Combine(_appDirectory, IconSubfolder));
          Directory.CreateDirectory(System.IO.Path.Combine(_appDirectory, PhotoSubfolder));
          Directory.CreateDirectory(System.IO.Path.Combine(_appDirectory, OverlaySubfolder)); 

          // 映射根目录到虚拟主机名
          webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
          webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
              VirtualHostName, _appDirectory, CoreWebView2HostResourceAccessKind.Allow);
          Console.WriteLine($"Mapped 'https://{VirtualHostName}/' to '{_appDirectory}'");
          // 订阅消息
          webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }
        else { /* ... */ }

        // 导航
        string htmlFilePath = System.IO.Path.Combine(_appDirectory ?? "", "map.html");
        if (!System.IO.File.Exists(htmlFilePath)) { /* ... */ return; }
        webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
        webView.CoreWebView2.Navigate($"https://{VirtualHostName}/map.html");
        Console.WriteLine($"WebView2 navigating to virtual host URL: https://{VirtualHostName}/map.html");

      }
      catch (Exception ex)
      {
        MessageBox.Show($"初始化 WebView2 时发生严重错误: {ex.Message}", "初始化失败", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
    #endregion

    #region 调用 JavaScript 缩放到指定 POI 的辅助方法
    private async Task ZoomMapToPoi(double longitude, double latitude, int zoom)
    {
      if (webView == null || webView.CoreWebView2 == null) return;

      // 构建调用 JS 函数 zoomToPoi 的脚本
      string script = $"zoomToPoi({longitude}, {latitude}, {zoom});";
      Console.WriteLine($"Executing script: {script}");

      try
      {
        await webView.CoreWebView2.ExecuteScriptAsync(script);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"执行 zoomToPoi 时出错: {ex.Message}");
      }
    }
    #endregion

    #region 更新地图上的 POI
    private async Task UpdatePoisOnMap(IEnumerable<PoiData> poisToShow) // 修改参数类型
    {
      if (webView == null || webView.CoreWebView2 == null) { /* ... */ return; }
      if (poisToShow == null) { poisToShow = new List<PoiData>(); }

      // 确保序列化的是当前集合的最新状态
      string poisJson = JsonConvert.SerializeObject(poisToShow.ToList()); // 转换为 List 再序列化
      Console.WriteLine($"Updating map with POIs JSON: {poisJson}");
      string escapedJson = JsonConvert.SerializeObject(poisJson);
      string addPoisScript = $"addPoisFromJson({escapedJson});";
      Console.WriteLine($"Executing script: {addPoisScript}");
      try { await webView.CoreWebView2.ExecuteScriptAsync(addPoisScript); }
      catch (Exception ex) { /* ... error handling ... */ }
    }
    #endregion 

    #region 应用筛选和搜索的核心方法
    private async Task ApplyFiltersAndSearch()
    {
      if (currentDisplayedPois == null) return;

      // 获取当前筛选条件
      string selectedCategory = categoryFilterComboBox.SelectedItem as string;
      string searchTerm = searchTextBox.Text.Trim().ToLowerInvariant();

      IEnumerable<PoiData> poisToDisplay = currentDisplayedPois;

      if (!string.IsNullOrEmpty(selectedCategory) && selectedCategory != AllCategoriesFilter)
      {
        poisToDisplay = poisToDisplay.Where(p => p.Category == selectedCategory);
      }

      // 应用文本搜索筛选
      if (!string.IsNullOrEmpty(searchTerm))
      {
        poisToDisplay = poisToDisplay.Where(poi =>
                     (poi.Name != null && poi.Name.ToLowerInvariant().Contains(searchTerm)) ||
                     (poi.Description != null && poi.Description.ToLowerInvariant().Contains(searchTerm)));
      }

      // 获取最终筛选结果列表
      List<PoiData> filteredPois = poisToDisplay.ToList();
      Console.WriteLine($"Filtering/Search resulted in {filteredPois.Count} POIs.");

      // 更新地图显示
      await UpdatePoisOnMap(filteredPois);

      // 更新 ListView 状态 (选中第一个)
      if (filteredPois.Any())
      {
        // 查找第一个结果在原始列表中的对应项，以确保选中项是 ObservableCollection 中的对象
        PoiData firstResultInOriginal = currentDisplayedPois.FirstOrDefault(p => p.Id == filteredPois.First().Id);
        if (firstResultInOriginal != null)
        {
          poiListView.SelectedItem = firstResultInOriginal;
          poiListView.ScrollIntoView(poiListView.SelectedItem);
        }
        else
        {
          poiListView.SelectedItem = null;
        }
      }
      else
      {
        poiListView.SelectedItem = null;
        // 如果是搜索触发的，可以提示未找到
        // if (!string.IsNullOrEmpty(searchTerm)) {
        //     MessageBox.Show($"未找到符合条件的 POI。", "搜索结果", MessageBoxButton.OK, MessageBoxImage.Information);
        // }
      }
    }
    #endregion

    #region 切换到指定校区的核心方法
    private async Task SwitchToCampus(CampusData campus)
    {
      if (campus == null || webView?.CoreWebView2 == null)
      {
        Console.WriteLine("SwitchToCampus called with null campus or WebView2 not ready.");
        return;
      }

      Console.WriteLine($"Switching to campus: {campus.Name}");

      string campusJson = JsonConvert.SerializeObject(campus);

      Console.WriteLine($"Data being sent to JS (raw JSON): {campusJson}");

      string jsCompatibleJsonString = JsonConvert.SerializeObject(campusJson); 

      string script = $"switchCampus({jsCompatibleJsonString});"; 
                                                                  // ---------------------------------------------------------
      Console.WriteLine($"Executing script: {script}");

      try
      {
        Console.WriteLine("Attempting to execute switchCampus script...");
        await webView.CoreWebView2.ExecuteScriptAsync(script);
        Console.WriteLine("Switch campus command sent to JS successfully.");

        await ApplyFiltersAndSearch();
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error executing switchCampus script: {ex.Message}");
        MessageBox.Show($"切换校区时出错: {ex.Message}", "脚本错误", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
    #endregion



    #endregion

    #region 事件处理模块

    #region 图层可见性改变事件处理
    private async void LayerVisibilityChanged(object sender, EventArgs e)
    {
      if (sender is LayerItem changedLayer)
      {
        Console.WriteLine($"Layer visibility changed: {changedLayer.CategoryName}, IsVisible: {changedLayer.IsVisible}");

        // 调用 JavaScript 函数来更新地图上的显示
        await SetPoiCategoryVisibility(changedLayer.CategoryName, changedLayer.IsVisible);
      }
    }
    #endregion

    #region 窗口加载完成事件处理程序
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
      await InitializeWebViewAsync();
    }
    #endregion

    #region WebView2 接收到来自 JavaScript 的消息事件处理程序
    private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
      string jsonMessage = args.TryGetWebMessageAsString();
      if (string.IsNullOrEmpty(jsonMessage)) return;
      Console.WriteLine($"Message received from JS: {jsonMessage}");
      try
      {
        WebMessage message = JsonConvert.DeserializeObject<WebMessage>(jsonMessage);
        if (message?.Type == "poiClick" && Guid.TryParse(message.Id, out Guid clickedPoiId))
        {
          ShowPoiDetailsWindow(clickedPoiId); // 调用新方法
        }
      }
      catch (Exception ex) { Console.WriteLine($"Error processing message from JS: {ex.Message}"); }
    }
    #endregion

    #region "搜索" 按钮点击事件处理程序
    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
      // 只应用筛选和搜索条件
      await ApplyFiltersAndSearch();
    }
    #endregion

    #region "清除搜索" 按钮点击事件处理程序
    private async void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
      searchTextBox.Clear();
      categoryFilterComboBox.SelectedItem = AllCategoriesFilter;
      await ApplyFiltersAndSearch(); 
      poiListView.SelectedItem = null; // 取消列表选中
      Console.WriteLine("Search and filter cleared. Displaying all POIs.");
    }
    #endregion

    #region 导航完成事件处理程序
    private async void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
      if (e.IsSuccess)
      {
        Console.WriteLine("WebView2 navigation completed successfully.");
        if (campusSelectorComboBox.SelectedItem is CampusData selectedCampus)
        {
          await SwitchToCampus(selectedCampus); // 调用切换校区的逻辑
        }
        else if (_campuses != null && _campuses.Any())
        {
          campusSelectorComboBox.SelectedIndex = 0; 
        }
        else
        {
          Console.WriteLine("No campus selected or available on navigation completed.");
        }
        // 初始加载时不显示 POI，因为 POI 可能属于特定校区
        await UpdatePoisOnMap(new List<PoiData>()); 
      }
      else
      {
        Console.WriteLine($"WebView2 navigation failed with status: {e.WebErrorStatus}");
        MessageBox.Show($"无法加载地图页面: {e.WebErrorStatus}", "导航错误", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
    #endregion

    #region 导入按钮点击事件处理程序
    private async void ImportPoisButton_Click(object sender, RoutedEventArgs e)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      openFileDialog.Filter = "CSV 文件 (*.csv)|*.csv|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
      openFileDialog.Title = "导入 POI 数据文件 (CSV 或 JSON)";
      openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);


      bool? result = openFileDialog.ShowDialog();

      if (result == true)
      {
        string filePath = openFileDialog.FileName;
        List<PoiData> importedPois = new List<PoiData>(); // 用于存储导入的 POI
        bool importSuccess = false;

        try
        {
          string fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

          if (fileExtension == ".csv")
          {
            // --- 处理 CSV 文件 ---
            Console.WriteLine($"Attempting to import CSV file: {filePath}");
            string[] lines = File.ReadAllLines(filePath, Encoding.Default);
            // -----------------------------------------------

            if (lines.Length <= 1) 
            {
              MessageBox.Show("CSV 文件为空或只有表头，无法导入。", "导入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
              return;
            }

            for (int i = 1; i < lines.Length; i++)
            {
              string line = lines[i];
              if (string.IsNullOrWhiteSpace(line)) continue; // 跳过空行

              string[] fields = line.Split(','); // 按逗号分割
                                                 // 检查列数是否符合预期 (X, Y, Z, Name, description -> 5 列)
              if (fields.Length < 5)
              {
                Console.WriteLine($"Skipping line {i + 1}: Incorrect number of columns ({fields.Length}). Expected 5.");
                continue; // 跳过格式不正确的行
              }

              // 尝试解析数据
              if (double.TryParse(fields[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double longitude) &&
                  double.TryParse(fields[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double latitude))
              {
                string name = fields[3].Trim();
                string description = fields[4].Trim();
                // 处理可能存在的引号（简单移除）
                name = name.Trim('"');
                description = description.Trim('"');

                // 如果名称为空，可以跳过或给个默认名称
                if (string.IsNullOrEmpty(name))
                {
                  Console.WriteLine($"Skipping line {i + 1}: Name is empty.");
                  continue;
                }


                var newPoi = new PoiData
                {
                  Id = Guid.NewGuid(), // 生成新 ID
                  Name = name, // 现在应该能正确读取中文了
                  Longitude = longitude,
                  Latitude = latitude,
                  Description = description, // 现在应该能正确读取中文了
                  Category = "未分类", // CSV 中无类别，设为默认
                  IconPath = null, // 无图标信息
                  ImagePaths = new List<string>(), // 初始化空列表
                  RelatedLinks = new List<string>() // 初始化空列表
                };
                importedPois.Add(newPoi);
              }
              else
              {
                Console.WriteLine($"Skipping line {i + 1}: Failed to parse Longitude/Latitude.");
              }
            }
            Console.WriteLine($"Successfully parsed {importedPois.Count} POIs from CSV.");
            importSuccess = true;
            // ---------------------

          }
          else if (fileExtension == ".json")
          {
            // --- 处理 JSON 文件 (保持原有逻辑) ---
            Console.WriteLine($"Attempting to import JSON file: {filePath}");
            string jsonContent = File.ReadAllText(filePath, Encoding.UTF8); // JSON 通常是 UTF-8
            List<PoiData> importedPoisRaw = JsonConvert.DeserializeObject<List<PoiData>>(jsonContent);

            if (importedPoisRaw != null)
            {
              string iconDirectory = System.IO.Path.Combine(_appDirectory ?? "", IconSubfolder);
              string photoDirectory = System.IO.Path.Combine(_appDirectory ?? "", PhotoSubfolder);
              foreach (var poi in importedPoisRaw)
              {
                if (poi.Id == Guid.Empty) poi.Id = Guid.NewGuid();
                // ... (省略了 JSON 中 Icon/Image/Category 的验证和处理逻辑，与之前相同) ...
                if (!string.IsNullOrEmpty(poi.IconPath) && !System.IO.File.Exists(System.IO.Path.Combine(iconDirectory, poi.IconPath))) poi.IconPath = null;
                if (poi.ImagePaths == null) poi.ImagePaths = new List<string>();
                poi.ImagePaths = poi.ImagePaths.Where(img => !string.IsNullOrEmpty(img) && System.IO.File.Exists(System.IO.Path.Combine(photoDirectory, img))).ToList();
                if (string.IsNullOrWhiteSpace(poi.Category)) poi.Category = "未分类";
                if (poi.RelatedLinks == null) poi.RelatedLinks = new List<string>(); // 确保初始化

                importedPois.Add(poi); // 添加到最终列表
              }
              Console.WriteLine($"Successfully parsed {importedPois.Count} POIs from JSON.");
              importSuccess = true;
            }
            else
            {
              MessageBox.Show("无法从 JSON 文件中解析 POI 数据。", "导入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            // -----------------------------
          }
          else
          {
            MessageBox.Show($"不支持的文件类型: {fileExtension}\n请选择 .csv 或 .json 文件。", "文件类型错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
          }

          // --- 导入成功后的处理 ---
          if (importSuccess && importedPois.Any())
          {
            currentDisplayedPois.Clear(); // 清空当前列表
            foreach (var poi in importedPois)
            {
              currentDisplayedPois.Add(poi); // 添加新导入的 POI
            }
            MessageBox.Show($"成功导入 {importedPois.Count} 个 POI。", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);

            // 更新 UI
            await ApplyFiltersAndSearch(); // 应用筛选并更新地图
            ClearEditForm(); // 清空编辑表单
                             // 可选: 导入后更新图层类别
                             // InitializePoiLayers();
          }
          else if (importSuccess) // 导入成功但列表为空
          {
            MessageBox.Show("文件已处理，但未导入任何有效的 POI 数据。", "导入提示", MessageBoxButton.OK, MessageBoxImage.Information);
            currentDisplayedPois.Clear(); // 清空列表
            await ApplyFiltersAndSearch(); // 更新地图
            ClearEditForm();
          }
          // ----------------------

        }
        catch (IOException ioEx)
        {
          MessageBox.Show($"读取文件时出错: {ioEx.Message}\n请检查文件是否被占用或路径是否正确。", "文件读取错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (FormatException formatEx)
        {
          MessageBox.Show($"解析文件数据时格式错误: {formatEx.Message}\n请检查文件内容是否符合预期格式。", "格式错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (JsonException jsonEx) // 单独捕获 JSON 解析错误
        {
          MessageBox.Show($"解析 JSON 文件时出错: {jsonEx.Message}\n请检查文件内容是否为有效的 POI 数据 JSON 格式。", "JSON 解析错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
          MessageBox.Show($"导入 POI 数据时发生未知错误: {ex.Message}", "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
      else
      {
        Console.WriteLine("Import POI file dialog cancelled.");
      }
    }
    #endregion

    #region "选择图标" 按钮点击事件处理程序
    private void SelectIconButton_Click(object sender, RoutedEventArgs e)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog { /* ... */ };
      bool? result = openFileDialog.ShowDialog();
      if (result == true)
      {
        string selectedFilePath = openFileDialog.FileName;
        string fileName = System.IO.Path.GetFileName(selectedFilePath);
        try
        {
          // ... (文件复制逻辑) ...
          string appDirectory = _appDirectory; // 使用缓存的路径
          if (string.IsNullOrEmpty(appDirectory)) throw new DirectoryNotFoundException("无法获取应用程序目录。");
          string iconDirectory = System.IO.Path.Combine(appDirectory, IconSubfolder);
          Directory.CreateDirectory(iconDirectory);
          string destinationPath = System.IO.Path.Combine(iconDirectory, fileName);
          File.Copy(selectedFilePath, destinationPath, true);
          Console.WriteLine($"Icon file copied successfully to: {destinationPath}");

          // 更新暂存的文件名
          _currentSelectedIconFileName = fileName;
          // 更新 UI 显示
          iconPathTextBlock.Text = _currentSelectedIconFileName;
          iconPathTextBlock.Foreground = Brushes.Black;
          _iconManuallySelected = true;
          Console.WriteLine("Icon manually selected by user.");

        }
        catch (Exception ex) { /* ... */ ClearIconSelection(); } // 出错时清除选择
      }
    }
    #endregion

    #region"添加照片" 按钮点击事件处理程序
    private void AddPhotosButton_Click(object sender, RoutedEventArgs e)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      openFileDialog.Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*";
      openFileDialog.Title = "选择一张或多张照片";
      openFileDialog.Multiselect = true; // 允许多选

      bool? result = openFileDialog.ShowDialog();

      if (result == true && openFileDialog.FileNames.Length > 0)
      {
        string appDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(appDirectory)) { /* ... error ... */ return; }
        string photoDirectory = System.IO.Path.Combine(appDirectory, PhotoSubfolder);
        Directory.CreateDirectory(photoDirectory); // 确保目录存在

        List<string> addedFiles = new List<string>(); // 记录成功添加的文件名

        foreach (string selectedFilePath in openFileDialog.FileNames)
        {
          string fileName = System.IO.Path.GetFileName(selectedFilePath);
          string destinationPath = System.IO.Path.Combine(photoDirectory, fileName);
          try
          {
            File.Copy(selectedFilePath, destinationPath, true); // 覆盖同名文件
            Console.WriteLine($"Photo copied successfully to: {destinationPath}");
            addedFiles.Add(fileName); // 添加成功的文件名
          }
          catch (Exception ex)
          {
            MessageBox.Show($"复制照片 '{fileName}' 时出错: {ex.Message}", "文件复制错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            // 单个文件复制失败，继续处理其他文件
          }
        }

        // 将成功复制的文件名添加到 UI 和数据中
        if (addedFiles.Any())
        {
          if (isEditing && editingPoiId != Guid.Empty)
          {
            // 如果正在编辑，直接修改数据源
            PoiData poiToUpdate = currentDisplayedPois.FirstOrDefault(p => p.Id == editingPoiId);
            if (poiToUpdate != null)
            {
              if (poiToUpdate.ImagePaths == null) poiToUpdate.ImagePaths = new List<string>();
              foreach (var file in addedFiles)
              {
                if (!poiToUpdate.ImagePaths.Contains(file)) // 避免重复添加
                {
                  poiToUpdate.ImagePaths.Add(file);
                  photoListBox.Items.Add(file); // 更新 UI
                }
              }
            }
          }
          else
          {
            // 如果是新建状态，添加到暂存列表和 UI
            foreach (var file in addedFiles)
            {
              if (!_newPoiPhotoFileNames.Contains(file)) // 避免重复添加
              {
                _newPoiPhotoFileNames.Add(file);
                photoListBox.Items.Add(file); // 更新 UI
              }
            }
          }
        }
      }
    }
    #endregion

    #region "移除选中照片" 按钮点击事件处理程序
    private void RemovePhotoButton_Click(object sender, RoutedEventArgs e)
    {
      if (photoListBox.SelectedItem is string selectedFileName) // 获取选中的文件名
      {
        photoListBox.Items.Remove(selectedFileName); // 从 UI 移除

        if (isEditing && editingPoiId != Guid.Empty)
        {
          // 如果正在编辑，从数据源移除
          PoiData poiToUpdate = currentDisplayedPois.FirstOrDefault(p => p.Id == editingPoiId);
          if (poiToUpdate?.ImagePaths != null) // 安全访问
          {
            poiToUpdate.ImagePaths.Remove(selectedFileName);
            Console.WriteLine($"Removed photo reference: {selectedFileName} from POI: {poiToUpdate.Name}");
          }
        }
        else
        {
          _newPoiPhotoFileNames.Remove(selectedFileName);
          Console.WriteLine($"Removed photo reference: {selectedFileName} from new POI temp list.");
        }
      }
    }
    #endregion

    #region"保存 POIs" 按钮点击事件处理程序
    private void SavePoisButton_Click(object sender, RoutedEventArgs e)
    {
      if (currentDisplayedPois == null || !currentDisplayedPois.Any())
      {
        MessageBox.Show("当前没有 POI 数据可保存。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      SaveFileDialog saveFileDialog = new SaveFileDialog();
      saveFileDialog.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
      saveFileDialog.Title = "保存 POI 数据到文件";
      saveFileDialog.FileName = "campus_pois.json"; // 默认文件名
      saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

      bool? result = saveFileDialog.ShowDialog();

      if (result == true)
      {
        string filePath = saveFileDialog.FileName;
        try
        {
          // 将 ObservableCollection 序列化为 JSON 字符串 (美化格式)
          string jsonOutput = JsonConvert.SerializeObject(currentDisplayedPois, Formatting.Indented);

          // 写入文件 (使用 UTF-8 编码)
          File.WriteAllText(filePath, jsonOutput, Encoding.UTF8);

          MessageBox.Show($"POI 数据已成功保存到:\n{filePath}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
          Console.WriteLine($"POIs saved to: {filePath}");
        }
        catch (JsonException jsonEx)
        {
          MessageBox.Show($"序列化 POI 数据时出错: {jsonEx.Message}", "保存错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (IOException ioEx)
        {
          MessageBox.Show($"写入文件时出错: {ioEx.Message}\n请检查文件路径和权限。", "保存错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
          MessageBox.Show($"保存 POI 数据时发生未知错误: {ex.Message}", "保存错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
    }
    #endregion

    #region"加载 POIs" 按钮点击事件处理程序
    private async void LoadPoisButton_Click(object sender, RoutedEventArgs e)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      openFileDialog.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
      openFileDialog.Title = "从文件加载 POI 数据";
      openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

      bool? result = openFileDialog.ShowDialog();

      if (result == true)
      {
        string filePath = openFileDialog.FileName;
        Console.WriteLine($"Attempting to load POIs from: {filePath}");
        try
        {
          // 读取文件内容
          string jsonInput = File.ReadAllText(filePath, Encoding.UTF8);

          // 反序列化 JSON 为 List<PoiData>
          List<PoiData> loadedPois = JsonConvert.DeserializeObject<List<PoiData>>(jsonInput);

          if (loadedPois != null)
          {
            // 清空当前集合
            currentDisplayedPois.Clear();

            // 将加载的数据添加到 ObservableCollection
            string iconDirectory = System.IO.Path.Combine(_appDirectory ?? "", IconSubfolder);
            string photoDirectory = System.IO.Path.Combine(_appDirectory ?? "", PhotoSubfolder);
            foreach (var poi in loadedPois)
            {
              // ... (处理 ID, 图标, 照片) ...
              if (poi.Id == Guid.Empty) poi.Id = Guid.NewGuid();
              if (!string.IsNullOrEmpty(poi.IconPath) && !File.Exists(System.IO.Path.Combine(iconDirectory, poi.IconPath))) poi.IconPath = null;
              if (poi.ImagePaths == null) poi.ImagePaths = new List<string>();
              poi.ImagePaths = poi.ImagePaths.Where(img => !string.IsNullOrEmpty(img) && File.Exists(System.IO.Path.Combine(photoDirectory, img))).ToList();
              if (string.IsNullOrWhiteSpace(poi.Category)) poi.Category = "未分类";
              currentDisplayedPois.Add(poi);
            }

            MessageBox.Show($"成功从文件加载了 {currentDisplayedPois.Count} 个 POI。", "加载成功", MessageBoxButton.OK, MessageBoxImage.Information);
            Console.WriteLine($"Loaded {currentDisplayedPois.Count} POIs from: {filePath}");
            await ApplyFiltersAndSearch();

            // 清空编辑表单
            ClearEditForm();
            // 更新地图显示
            await UpdatePoisOnMap(currentDisplayedPois);
          }
          else
          {
            MessageBox.Show("无法从文件中解析 POI 数据，文件可能为空或格式不正确。", "加载错误", MessageBoxButton.OK, MessageBoxImage.Warning);
          }
        }
        catch (JsonException jsonEx)
        {
          MessageBox.Show($"解析 JSON 文件时出错: {jsonEx.Message}\n请检查文件内容是否为有效的 POI 数据 JSON 格式。", "加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (IOException ioEx)
        {
          MessageBox.Show($"读取文件时出错: {ioEx.Message}\n请检查文件路径和权限。", "加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
          MessageBox.Show($"加载 POI 数据时发生未知错误: {ex.Message}", "加载错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
    }
    #endregion

    #region"POI分类" 按钮点击事件处理程序
    private void PoiCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      // 如果不是用户手动选择的图标，则根据类别自动匹配
      if (!_iconManuallySelected && poiCategoryComboBox.SelectedItem is string selectedCategory)
      {
        if (_categoryIconMap.TryGetValue(selectedCategory, out string defaultIconFileName))
        {
          // 找到了对应的默认图标文件名
          _currentSelectedIconFileName = defaultIconFileName;
          iconPathTextBlock.Text = string.IsNullOrEmpty(_currentSelectedIconFileName) ? "(默认图标)" : _currentSelectedIconFileName;
          iconPathTextBlock.Foreground = string.IsNullOrEmpty(_currentSelectedIconFileName) ? Brushes.Gray : Brushes.Black;
          Console.WriteLine($"Category '{selectedCategory}' selected, automatically set icon to: {_currentSelectedIconFileName ?? "default"}");
        }
        else
        {
          // 如果映射中没有这个类别（或值为 null），则使用默认图标
          _currentSelectedIconFileName = null;
          iconPathTextBlock.Text = "(默认图标)";
          iconPathTextBlock.Foreground = Brushes.Gray;
          Console.WriteLine($"Category '{selectedCategory}' selected, no default icon found, using default.");
        }
      }
      // 如果 _iconManuallySelected 为 true，则不执行任何操作，保留用户手动选择的图标
      else if (_iconManuallySelected)
      {
        Console.WriteLine($"Category changed, but icon was manually selected, keeping '{_currentSelectedIconFileName}'.");
      }
    }
    #endregion

    #region "切换地图类型" 按钮点击事件处理程序
    private async void SwitchMapTypeButton_Click(object sender, RoutedEventArgs e)
    {
      if (webView == null || webView.CoreWebView2 == null) return; // 确保 WebView2 已准备好

      // 切换状态
      _isSatelliteView = !_isSatelliteView;

      // 更新按钮文本
      switchMapTypeButton.Content = _isSatelliteView ? "切换到普通地图" : "切换到卫星地图";

      // 构建调用 JavaScript 函数 setMapType 的脚本
      string script = $"setMapType({_isSatelliteView.ToString().ToLowerInvariant()});";
      Console.WriteLine($"Executing script: {script}");

      try
      {
        await webView.CoreWebView2.ExecuteScriptAsync(script);
      }
      catch (Exception ex)
      {
        MessageBox.Show($"切换地图类型时出错: {ex.Message}", "脚本错误", MessageBoxButton.OK, MessageBoxImage.Error);
        // 出错时可能需要将状态和按钮文本恢复
        _isSatelliteView = !_isSatelliteView;
        switchMapTypeButton.Content = _isSatelliteView ? "切换到普通地图" : "切换到卫星地图";
      }
    }
    #endregion

    #region 校区选择下拉框选项改变事件处理程序
    private async void CampusSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (campusSelectorComboBox.SelectedItem is CampusData selectedCampus)
      {
        await SwitchToCampus(selectedCampus);
      }
    }
    #endregion

    #region “校区管理”按钮点击事件处理程序
    private void ManageCampusesButton_Click(object sender, RoutedEventArgs e)
    {
      CampusManagementWindow managementWindow = new CampusManagementWindow(_campuses ?? new List<CampusData>());
      managementWindow.Owner = this;

      bool? result = managementWindow.ShowDialog();

      if (result == true)
      {
        Console.WriteLine("Campus management window saved changes.");
        // 获取管理窗口返回的已更新列表
        _campuses = managementWindow.ResultCampuses;

        // 保存更新后的列表到文件
        SaveCampusData();

        // 记录当前选中的校区名称，以便刷新后恢复选择
        string previouslySelectedName = (campusSelectorComboBox.SelectedItem as CampusData)?.Name;

        // 刷新 ComboBox
        InitializeCampusSelector();

        // 尝试恢复之前的选择
        if (!string.IsNullOrEmpty(previouslySelectedName))
        {
          var campusToReselect = _campuses.FirstOrDefault(c => c.Name == previouslySelectedName);
          if (campusToReselect != null)
          {
            campusSelectorComboBox.SelectedItem = campusToReselect;
            // 如果恢复的选择就是当前选择，SelectionChanged 可能不会触发，需要手动切换一下地图
          }
          else if (campusSelectorComboBox.Items.Count > 0)
          {
            campusSelectorComboBox.SelectedIndex = 0; // 如果之前的找不到了，选第一个
          }
        }
        else if (campusSelectorComboBox.Items.Count > 0)
        {
          campusSelectorComboBox.SelectedIndex = 0; // 如果之前没选，选第一个
        }


        // 如果当前没有选中任何校区（例如列表被清空），可能需要清除地图状态
        if (campusSelectorComboBox.SelectedItem == null)
        {
        }

      }
      else
      {
        Console.WriteLine("Campus management window cancelled.");
      }
    }
    #endregion

    #region“删除POI”按钮点击事件处理程序
    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
      if (poiListView.SelectedItem is PoiData selectedPoi)
      {
        // 弹出确认对话框
        MessageBoxResult confirmResult = MessageBox.Show($"确定要删除 POI \"{selectedPoi.Name}\" 吗？",
                                                        "确认删除",
                                                        MessageBoxButton.YesNo,
                                                        MessageBoxImage.Question);

        if (confirmResult == MessageBoxResult.Yes)
        {
          // 从集合中移除
          bool removed = currentDisplayedPois.Remove(selectedPoi);

          if (removed)
          {
            Console.WriteLine($"Deleted POI: {selectedPoi.Name}");
            // 更新地图显示
            await ApplyFiltersAndSearch();
            // 如果删除的是正在编辑的项，清空表单
            if (isEditing && editingPoiId == selectedPoi.Id)
            {
              ClearEditForm();
            }
            // 清除列表选中状态
            poiListView.SelectedItem = null;
          }
        }
      }
      else
      {
        MessageBox.Show("请先在列表中选择要删除的 POI。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
      }
    }
    #endregion

    #region “导出地图图片”按钮点击事件处理程序
    private async void ExportMapButton_Click(object sender, RoutedEventArgs e)
    {
      // 检查 WebView2 控件和 CoreWebView2 是否准备就绪
      if (webView == null || webView.CoreWebView2 == null)
      {
        MessageBox.Show("地图视图尚未完全初始化。", "导出错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      SaveFileDialog saveFileDialog = new SaveFileDialog();
      saveFileDialog.Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg)|*.jpg"; 
      saveFileDialog.Title = "导出地图视图为图片";
      saveFileDialog.FileName = $"地图截图_{DateTime.Now:yyyyMMddHHmmss}";

      if (saveFileDialog.ShowDialog() == true)
      {
        string filePath = saveFileDialog.FileName;
        string fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

        CoreWebView2CapturePreviewImageFormat imageFormat;
        switch (fileExtension)
        {
          case ".jpg":
          case ".jpeg":
            imageFormat = CoreWebView2CapturePreviewImageFormat.Jpeg;
            break;
          case ".png":
          default:
            imageFormat = CoreWebView2CapturePreviewImageFormat.Png;
            if (fileExtension != ".png")
            {
              filePath = System.IO.Path.ChangeExtension(filePath, ".png");
              Console.WriteLine($"File extension changed to .png: {filePath}");
            }
            break;
        }

        try
        {
          using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
          {
            Console.WriteLine($"Attempting to capture preview as {imageFormat} to {filePath}");
            await webView.CoreWebView2.CapturePreviewAsync(imageFormat, fileStream);
            Console.WriteLine("CapturePreviewAsync completed.");
          }

          MessageBox.Show($"地图已成功导出到:\n{filePath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
          Console.WriteLine($"Map exported to: {filePath}");
        }
        catch (Exception ex)
        {
          MessageBox.Show($"导出地图时发生错误: {ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
          Console.WriteLine($"Error exporting map using CapturePreviewAsync: {ex.Message}");
        }
      }
    }
    #endregion

    #region“帮助”按钮点击事件处理程序
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
      HelpWindow helpWindow = new HelpWindow();
      helpWindow.Owner = this;
      helpWindow.ShowDialog();
    }
    #endregion

    #endregion

    #region 函数模块

    #region 从文件加载校区数据
    private void LoadCampusData()
    {
      string filePath = System.IO.Path.Combine(_appDirectory ?? "", CampusesFileName);
      if (File.Exists(filePath))
      {
        try
        {
          string json = File.ReadAllText(filePath);
          _campuses = JsonConvert.DeserializeObject<List<CampusData>>(json);
          Console.WriteLine($"Loaded {_campuses?.Count ?? 0} campuses from {filePath}");
        }
        catch (Exception ex)
        {
          MessageBox.Show($"加载校区数据文件 '{CampusesFileName}' 时出错: {ex.Message}\n将使用默认示例数据。", "加载错误", MessageBoxButton.OK, MessageBoxImage.Warning);
          _campuses = null; // 加载失败，清空
        }
      }
      else
      {
        Console.WriteLine($"Campus data file not found: {filePath}. Using default example data.");
        _campuses = null; // 文件不存在
      }

      if (_campuses == null)
      {
        InitializeDefaultCampusData(); 
      }
    }
    #endregion

    #region 保存校区数据到文件
    private void SaveCampusData()
    {
      if (_campuses == null) return; // 没有数据可保存

      string filePath = System.IO.Path.Combine(_appDirectory ?? "", CampusesFileName);
      try
      {
        string json = JsonConvert.SerializeObject(_campuses, Formatting.Indented); // 美化格式
        File.WriteAllText(filePath, json, Encoding.UTF8);
        Console.WriteLine($"Saved {_campuses.Count} campuses to {filePath}");
      }
      catch (Exception ex)
      {
        MessageBox.Show($"保存校区数据到 '{CampusesFileName}' 时出错: {ex.Message}", "保存错误", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
    #endregion

    #region 显示 POI 详细信息窗口
    private void ShowPoiDetailsWindow(Guid poiId)
    {
      PoiData selectedPoi = currentDisplayedPois.FirstOrDefault(p => p.Id == poiId);
      if (selectedPoi != null)
      {
        Console.WriteLine($"Opening details window for POI: {selectedPoi.Name}");

        // 1. 创建新窗口实例
        PoiDetailWindow detailWindow = new PoiDetailWindow();

        // 2. 设置所有者，使其显示在主窗口之上 (可选)
        detailWindow.Owner = this;

        // 3. 调用新窗口的方法来加载数据
        detailWindow.LoadPoiDetails(selectedPoi);

        // 4. 显示窗口 (非模态)
        detailWindow.Show();
      }
      else
      {
        Console.WriteLine($"POI with ID {poiId} not found.");
      }
    }
    #endregion

    #region 清空编辑表单和状态
    private void ClearEditForm(bool clearId = true)
    {
      // ... (清空 Name, Lon, Lat, Desc) ...
      poiNameTextBox.Clear(); /*...*/ poiLonTextBox.Clear(); /*...*/ poiLatTextBox.Clear(); /*...*/ poiDescTextBox.Clear(); /*...*/

      // 清空类别
      poiCategoryComboBox.SelectedIndex = -1;
      poiCategoryComboBox.Text = string.Empty;

      // 清空图标
      iconPathTextBlock.Text = "(默认图标)"; iconPathTextBlock.Foreground = Brushes.Gray;
      _currentSelectedIconFileName = null;
      _iconManuallySelected = false; // 重置手动选择标志

      // ... (清空照片列表) ...
      photoListBox.Items.Clear(); _newPoiPhotoFileNames.Clear(); removePhotoButton.IsEnabled = false;

      // ... (恢复编辑状态和按钮文本) ...
      isEditing = false; if (clearId) editingPoiId = Guid.Empty; if (clearId) poiIdTextBox.Clear();
      savePoiButton.Content = "保存 POI"; poiListView.SelectedItem = null;
      Console.WriteLine("Edit form cleared.");
    }
    #endregion 

    #region 清除图标选择状态
    private void ClearIconSelection()
    {
      _currentSelectedIconFileName = null;
      iconPathTextBlock.Text = "(默认图标)";
      iconPathTextBlock.Foreground = Brushes.Gray;
      _iconManuallySelected = false;
    }
    #endregion

    #endregion

    #region POI面板模块

    #region ListView 选中项改变事件
    private void PoiListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      // 根据是否有选中项来启用/禁用编辑和删除按钮
      bool isItemSelected = poiListView.SelectedItem != null;
      editPoiButton.IsEnabled = isItemSelected;
      deletePoiButton.IsEnabled = isItemSelected;

      // 如果取消了编辑状态，并且没有选中项，则清空表单
      if (!isEditing && !isItemSelected)
      {
        ClearEditForm();
      }
    }
    #endregion

    #region "编辑选中项" 按钮点击事件
    private void EditPoiButton_Click(object sender, RoutedEventArgs e)
    {
      if (poiListView.SelectedItem is PoiData selectedPoi)
      {
        // ... (填充 Name, Lon, Lat, Desc) ...
        poiIdTextBox.Text = selectedPoi.Id.ToString();
        poiNameTextBox.Text = selectedPoi.Name;
        poiLonTextBox.Text = selectedPoi.Longitude.ToString(CultureInfo.InvariantCulture);
        poiLatTextBox.Text = selectedPoi.Latitude.ToString(CultureInfo.InvariantCulture);
        poiDescTextBox.Text = selectedPoi.Description;

        poiCategoryComboBox.Text = selectedPoi.Category ?? "未分类"; // 处理 null 情况

        _currentSelectedIconFileName = selectedPoi.IconPath;
        iconPathTextBlock.Text = string.IsNullOrEmpty(_currentSelectedIconFileName) ? "(默认图标)" : _currentSelectedIconFileName; // 只显示文件名
        iconPathTextBlock.Foreground = string.IsNullOrEmpty(_currentSelectedIconFileName) ? Brushes.Gray : Brushes.Black;

        _iconManuallySelected = false;

        photoListBox.Items.Clear();
        _newPoiPhotoFileNames.Clear(); // 清空新建时的暂存列表
        if (selectedPoi.ImagePaths != null)
        {
          foreach (string imgPath in selectedPoi.ImagePaths)
          {
            photoListBox.Items.Add(imgPath);
          }
        }
        removePhotoButton.IsEnabled = false; // 重置移除按钮状态

        isEditing = true;
        editingPoiId = selectedPoi.Id;
        savePoiButton.Content = "更新 POI";
        Console.WriteLine($"Editing POI: {selectedPoi.Name} (ID: {selectedPoi.Id})");
      }
    }
    #endregion
  
    #region"删除选中项" 按钮点击事件
    private async void DeletePoiButton_Click(object sender, RoutedEventArgs e)
    {
      if (poiListView.SelectedItem is PoiData selectedPoi)
      {
        // 弹出确认对话框
        MessageBoxResult confirmResult = MessageBox.Show($"确定要删除 POI \"{selectedPoi.Name}\" 吗？",
                                                        "确认删除",
                                                        MessageBoxButton.YesNo,
                                                        MessageBoxImage.Question);

        if (confirmResult == MessageBoxResult.Yes)
        {
          // 从集合中移除
          bool removed = currentDisplayedPois.Remove(selectedPoi);

          if (removed)
          {
            Console.WriteLine($"Deleted POI: {selectedPoi.Name}");
            // 更新地图显示
            await ApplyFiltersAndSearch();
            // 如果删除的是正在编辑的项，清空表单
            if (isEditing && editingPoiId == selectedPoi.Id)
            {
              ClearEditForm();
            }
          }
        }
      }
    }
    #endregion

    #region "保存 POI" 按钮点击事件
    private async void SavePoiButton_Click(object sender, RoutedEventArgs e)
    {
      // ... (读取和验证 Name, Lon, Lat, Desc) ...
      string name = poiNameTextBox.Text.Trim(); /*...*/
      string lonStr = poiLonTextBox.Text.Trim(); /*...*/
      string latStr = poiLatTextBox.Text.Trim(); /*...*/
      string desc = poiDescTextBox.Text.Trim(); /*...*/
      string category = poiCategoryComboBox.Text.Trim();
      if (string.IsNullOrWhiteSpace(category)) category = "未分类";

      if (string.IsNullOrEmpty(name)) { /*...*/ return; }
      if (!double.TryParse(lonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double longitude)) { /*...*/ return; }
      if (!double.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double latitude)) { /*...*/ return; }
      if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90) { /*...*/ return; }


      if (isEditing && editingPoiId != Guid.Empty)
      {
        PoiData poiToUpdate = currentDisplayedPois.FirstOrDefault(p => p.Id == editingPoiId);
        if (poiToUpdate != null)
        {
          poiToUpdate.Name = name; /*...*/ poiToUpdate.Longitude = longitude; /*...*/ poiToUpdate.Latitude = latitude; /*...*/
          poiToUpdate.Description = desc; /*...*/
          poiToUpdate.Category = category;
          poiToUpdate.IconPath = _currentSelectedIconFileName;

          Console.WriteLine($"Updated POI: {poiToUpdate.Name}");

          poiListView.Items.Refresh();
          await ApplyFiltersAndSearch(); // 刷新列表和地图
          ClearEditForm();
        }
        else { /* ... */ ClearEditForm(); }
      }
      else
      {
        PoiData newPoi = new PoiData
        {
          Name = name,
          Longitude = longitude,
          Latitude = latitude,
          Description = desc,
          Category = category, // 设置类别
          IconPath = _currentSelectedIconFileName, // 设置图标文件名
          ImagePaths = new List<string>(_newPoiPhotoFileNames)
        };
        currentDisplayedPois.Add(newPoi);
        Console.WriteLine($"Added new POI: {newPoi.Name}");
        await ApplyFiltersAndSearch(); // 刷新列表和地图
        ClearEditForm(clearId: false);
      }
    }
    #endregion

    #region "新建 POI" 按钮点击事件
    private void NewPoiButton_Click(object sender, RoutedEventArgs e)
    {
      ClearEditForm(); // ClearEditForm 会重置 _iconManuallySelected
      poiNameTextBox.Focus();
    }
    #endregion

    #region 照片列表框选中项改变事件
    private void PhotoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      // 根据是否有选中项来启用/禁用移除按钮
      removePhotoButton.IsEnabled = (photoListBox.SelectedItem != null);
    }
    #endregion

    #region 类别筛选 ComboBox 选择改变事件
    private async void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      await ApplyFiltersAndSearch();
    }
    #endregion

    #endregion

    

    



  }
}
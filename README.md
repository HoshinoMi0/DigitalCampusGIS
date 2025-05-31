# 数字校园 GIS 系统 (Digital Campus GIS System)

## 简介

本项目是一个基于 WPF (C# .NET Framework) 和 WebView2 (内嵌百度地图 JavaScript API) 开发的桌面应用程序，旨在为高校提供一个集校园信息展示、兴趣点 (POI) 管理、多校区视图切换、图层控制及数据导入导出等功能于一体的地理信息系统。用户可以通过本系统直观地浏览校园地图，管理校园内的各类设施信息，并进行可视化展示。

该项目是《卫星导航定位技术》课程的实践成果，重点在于结合 GNSS 数据采集流程，实现对采集数据的内业处理、可视化管理和应用。

## 主要功能

* **地图可视化**:
    * 支持百度地图作为底图（街道图与卫星影像图切换）。
    * 在地图上显示 POI 点标记及名称标签。
    * POI 标记和标签大小随地图缩放级别动态调整。
    * 支持加载和显示校区叠加图（例如校园平面图）。
* **校区管理**:
    * 支持多校区数据管理（通过 `campuses.json` 文件配置）。
    * 提供校区选择下拉框，方便切换不同校区视图。
    * 独立的校区管理窗口，用于添加、编辑、删除校区信息（包括名称、中心点、缩放级别、叠加图及其边界）。
* **POI 数据管理**:
    * 在右侧面板以列表形式展示 POI 数据。
    * 支持按预设类别筛选 POI。
    * 支持按名称或描述进行关键词搜索。
    * 提供完整的 POI 编辑表单，用于添加新 POI 或修改现有 POI 信息（名称、经纬度、类别、描述、自定义图标、关联照片、相关链接）。
    * 支持从 CSV 或 JSON 文件批量导入 POI 数据（CSV 文件需注意编码，支持中文）。
    * 支持将当前 POI 列表导出为 JSON 文件。
    * 支持从 JSON 文件加载 POI 列表。
* **图层控制**:
    * 根据 POI 类别动态生成图层控制复选框。
    * 允许用户按类别显式打开或关闭地图上对应 POI 的显示。
* **POI 详细信息展示**:
    * 点击地图上的 POI 标记或标签，可弹出详细信息窗口。
    * 详情窗口展示 POI 名称、描述、多张关联图片（缩略图与大图预览）、以及可点击的相关网页链接。
* **辅助功能**:
    * 将当前地图视图导出为图片文件 (PNG, JPG)。
    * 提供内置的帮助文档窗口。
    * 经过美化的用户界面，提升操作体验。

## 技术栈

* **后端与主界面**: C# .NET Framework, WPF
* **地图引擎与前端交互**: Microsoft Edge WebView2
* **地图服务**: 百度地图 JavaScript API (WebGL 版)
* **前端实现**: HTML, CSS, JavaScript (运行于 WebView2 内的 `map.html`)
* **数据格式**: JSON (用于校区配置、POI 导入导出), CSV (用于 POI 导入)
* **核心库**:
    * `Newtonsoft.Json`: 用于 JSON 序列化与反序列化。
    * `Microsoft.Web.WebView2.Wpf`: WebView2 WPF 控件库。

## 系统架构概述

系统采用混合架构：
1.  **WPF 用户界面层**: 负责主窗口、各管理窗口的UI展示和用户交互。
2.  **C# 后端逻辑层**: 处理业务逻辑、数据管理（内存及文件读写）、与 WebView2 的通信。
3.  **Web 前端层 (WebView2)**: 内嵌 `map.html`，通过 JavaScript 调用百度地图 API 实现地图渲染和交互，并与 C# 后端进行双向通信。
4.  **数据存储层**: 本地文件系统存储校区配置、POI 数据及相关资源文件（图标、照片、叠加图）。

*(您也可以在此处插入之前生成的文字版技术架构说明或功能架构说明的链接，或者精简版本)*

## 安装与运行

### 前提条件

1.  **.NET Framework**: 目标计算机需要安装 .NET Framework 4.8.1 或更高兼容版本。
2.  **WebView2 运行时**: 目标计算机必须安装 Microsoft Edge WebView2 运行时。如果未安装，程序首次启动时（如果使用了引导程序）会提示安装，或者用户需要自行从 [微软官方网站](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) 下载并安装 Evergreen Bootstrapper 或 Standalone Installer。

### 运行已发布的版本

1.  从发布包中解压所有文件到一个本地文件夹。
2.  直接运行文件夹中的 `DigitalCampusGIS.exe` 文件。
3.  确保与 `.exe` 同级的 `map.html` 文件以及 `Icons`, `PoiImages`, `Overlays` 子文件夹（如果包含默认内容）也一并复制。程序运行时会自动在程序目录下创建这些子文件夹（如果尚不存在）。

### 从源代码构建 (可选)

1.  克隆本仓库到本地：
    ```bash
    git clone [https://github.com/HoshinoMi0/DigitalCampusGIS.git](https://github.com/HoshinoMi0/DigitalCampusGIS.git) 
    ```
    (请将上面的 URL 替换为您实际的仓库 URL)
2.  使用 Visual Studio 2019 (或更高版本，需支持 .NET Framework 4.8.1) 打开 `DigitalCampusGIS.sln` 解决方案文件。
3.  确保已安装所有必要的 NuGet 包（Visual Studio 通常会自动还原）。主要依赖包括 `Newtonsoft.Json` 和 `Microsoft.Web.WebView2`。
4.  在 Visual Studio 中选择启动项目为 `DigitalCampusGIS`。
5.  点击“启动”按钮或按 F5 编译并运行。

## 使用说明

1.  **校区选择与管理**:
    * 启动后，通过顶部“选择校区”下拉框切换不同校区视图。
    * 点击“校区管理...”按钮，在弹出窗口中添加、编辑或删除校区信息，包括设置校区中心点、缩放级别及可选的叠加图。
2.  **POI 操作**:
    * 在右侧 POI 面板进行筛选、搜索。
    * 通过“图层控制”复选框显示/隐藏不同类别的 POI。
    * 点击“新建 POI”或在列表中选择 POI 后点击“编辑选中项”来管理 POI 的详细信息（名称、坐标、类别、描述、图标、照片、链接）。
    * 通过“导入 POI 文件”、“保存列表”、“加载列表”按钮管理 POI 数据。
3.  **地图交互**:
    * 直接在地图上点击 POI 图标或标签可查看其详细信息。
    * 使用鼠标滚轮或地图右下角控件进行缩放。
    * 按住鼠标左键拖动平移地图。
    * 点击顶部“切换地图类型”按钮在街道图和卫星影像间切换。
4.  **其他功能**:
    * 点击“导出地图图片...”可将当前地图视图保存为图片。
    * 点击“帮助”按钮查看帮助文档。

## 文件结构 (主要文件/文件夹)
DigitalCampusGIS/
├── DigitalCampusGIS.sln                # Visual Studio 解决方案文件
├── DigitalCampusGIS/                   # 主项目文件夹
│   ├── MainWindow.xaml               # 主窗口界面定义
│   ├── MainWindow.xaml.cs            # 主窗口后端逻辑
│   ├── CampusManagementWindow.xaml     # 校区管理窗口界面
│   ├── CampusManagementWindow.xaml.cs  # 校区管理窗口逻辑
│   ├── PoiDetailWindow.xaml          # POI 详情窗口界面
│   ├── PoiDetailWindow.xaml.cs       # POI 详情窗口逻辑
│   ├── HelpWindow.xaml               # 帮助窗口界面
│   ├── HelpWindow.xaml.cs            # 帮助窗口逻辑
│   ├── map.html                      # 嵌入 WebView2 的地图页面
│   ├── campuses.json                 # (示例) 校区配置文件
│   ├── Icons/                        # 存储 POI 图标的文件夹
│   ├── PoiImages/                    # 存储 POI 照片的文件夹
│   ├── Overlays/                     # 存储校区叠加图的文件夹
│   └── ... (其他项目文件如 App.xaml, .csproj, packages.config 等)
└── .gitignore                          # Git 忽略配置文件


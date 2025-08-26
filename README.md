# GameLauncher (游戏启动器)

一个现代化的 Windows 游戏启动器应用程序，基于 WinUI 3 和 .NET 8 构建，支持游戏管理、快速启动和 Steam 游戏集成。

## 🎮 主要功能

### 游戏管理
- **添加游戏**：手动添加自定义游戏到启动器
- **Steam 游戏导入**：自动扫描并导入已安装的 Steam 游戏
- **游戏启动**：一键启动游戏，支持 Steam 协议和直接可执行文件启动
- **游戏图标提取**：自动提取游戏可执行文件的图标
- **游戏目录访问**：右键菜单快速打开游戏安装目录

### 游戏组织
- **重复游戏清理**：智能检测并清理重复的游戏条目
- **批量删除**：支持多选删除游戏
- **游戏搜索与过滤**：快速找到想要的游戏

### Steam 集成
- **Steam 游戏自动发现**：扫描所有 Steam 游戏库路径
- **Steam 协议支持**：通过 Steam 客户端启动游戏
- **Steam 商店链接**：右键菜单直接跳转到 Steam 商店页面

### 界面与主题
- **现代化 UI**：基于 WinUI 3 的流畅设计语言
- **主题切换**：支持浅色、深色和跟随系统主题
- **响应式布局**：适配不同屏幕尺寸
- **自定义标题栏**：集成的自定义窗口标题栏

## 🏗️ 项目结构

```
GameLauncher/
├── GameLauncher.sln              # Visual Studio 解决方案文件
├── GameLauncher/                 # 主项目目录
│   ├── GameLauncher.csproj       # 项目配置文件
│   ├── Package.appxmanifest      # MSIX 打包清单
│   ├── app.manifest              # 应用程序清单
│   │
│   ├── App.xaml                  # 应用程序入口 XAML
│   ├── App.xaml.cs               # 应用程序入口代码
│   ├── MainWindow.xaml           # 主窗口 XAML
│   ├── MainWindow.xaml.cs        # 主窗口代码逻辑
│   │
│   ├── Pages/                    # 页面目录
│   │   ├── GamesPage.xaml        # 游戏管理页面 XAML
│   │   ├── GamesPage.xaml.cs     # 游戏管理页面逻辑
│   │   ├── SettingsPage.xaml     # 设置页面 XAML
│   │   └── SettingsPage.xaml.cs  # 设置页面逻辑
│   │
│   ├── Services/                 # 服务层
│   │   ├── SteamService.cs       # Steam 集成服务
│   │   └── ThemeService.cs       # 主题管理服务
│   │
│   ├── CustomDataObject.cs       # 游戏数据模型
│   ├── IconExtractor.cs          # 图标提取工具
│   │
│   ├── Assets/                   # 应用资源
│   │   ├── *.png                 # 应用图标和徽标
│   │   └── ...
│   │
│   └── Properties/               # 项目属性
│       ├── launchSettings.json   # 启动配置
│       └── PublishProfiles/      # 发布配置文件
│           ├── win-x64.pubxml    # x64 发布配置
│           ├── win-x86.pubxml    # x86 发布配置
│           └── win-arm64.pubxml  # ARM64 发布配置
```

## 🛠️ 技术栈

### 框架与平台
- **.NET 8.0**：最新的 .NET 运行时
- **WinUI 3**：Microsoft 的现代 Windows UI 框架
- **Windows App SDK**：Windows 平台特性支持
- **MSIX**：现代化的应用打包和分发

### 开发工具
- **C# 12**：现代 C# 语言特性
- **XAML**：声明式 UI 标记语言
- **Visual Studio 2022**：集成开发环境

### 系统集成
- **Windows Registry**：Steam 安装路径检测
- **Shell32 API**：图标提取功能
- **Steam Protocol**：Steam 游戏启动支持

## 🚀 构建与运行

1. **安装开发工具**：
   ```
   - Visual Studio 2022 (17.14 或更高版本)
   - Windows App SDK 工作负载
   - .NET 8.0 SDK
   ```

2. **克隆项目**：
   ```bash
   git clone <repository-url>
   cd GameLauncher
   ```

3. **打开解决方案**：
   ```bash
   GameLauncher.sln
   ```

## 📱 使用说明

### 首次启动
1. 启动应用程序
2. 应用会自动检测 Steam 安装（如果存在）
3. 可以选择导入 Steam 游戏或手动添加游戏

### 添加游戏
1. 点击"添加游戏"按钮
2. 填写游戏名称
3. 选择游戏的可执行文件(.exe)
4. 应用会自动提取游戏图标

### Steam 游戏导入
1. 点击"导入 Steam 游戏"按钮
2. 应用会扫描所有 Steam 游戏库
3. 选择要导入的游戏
4. 导入的游戏会显示 Steam 标识

### 游戏管理
- **启动游戏**：单击游戏卡片
- **右键菜单**：访问更多选项（删除、打开目录、Steam 商店等）
- **批量操作**：使用"删除游戏"模式进行多选操作
- **清理重复**：使用"清理重复"功能去除重复的游戏条目

### 设置选项
- **主题切换**：在设置页面选择浅色、深色或跟随系统主题
- **其他设置**：查看应用信息和版本


## 📄 许可证

本项目采用 [GPL 许可证](LICENSE.txt) - 查看 [LICENSE](LICENSE.txt) 文件了解详情。
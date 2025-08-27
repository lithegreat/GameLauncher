English | [简体中文](./docs/README.zh-CN.md) 

# GameLauncher

A modern Windows game launcher application built with WinUI 3 and .NET 8, featuring game management, quick launch, and Steam integration.

## 🎮 Key Features

### Game Management
- **Add Games**: Manually add custom games to the launcher
- **Steam Game Import**: Automatically scan and import installed Steam games
- **Game Launch**: One-click game launching with Steam protocol and direct executable support
- **Icon Extraction**: Automatically extract icons from game executables
- **Game Directory Access**: Right-click menu for quick access to game installation directories

### Steam Integration
- **Steam Game Discovery**: Scan all Steam library paths
- **Steam Protocol Support**: Launch games through Steam client
- **Steam Store Links**: Right-click menu to jump directly to Steam store pages

### Interface & Theming
- **Modern UI**: Based on WinUI 3 Fluent Design Language
- **Theme Switching**: Support for light, dark, and system theme following
- **Responsive Layout**: Adapts to different screen sizes

## 📥 Installation Instructions

Before installing GameLauncher, you need to install the application signing certificate:

1. **Download Certificate Files**:
   - Download the latest version from the [Releases](https://github.com/lithegreat/GameLauncher/releases) page
   - Download the release package containing both `.msix` installer and `.cer` certificate file

2. **Install Certificate**:
   - Right-click on the downloaded `.cer` certificate file
   - Select "Install Certificate"
   - In the Certificate Import Wizard, select "Local Machine"
   - Choose "Place all certificates in the following store"
   - Click "Browse" and select "Trusted Root Certification Authorities"
   - Complete the certificate installation

3. **Install Application**:
   - Double-click the `.msix` file to start installation
   - If the certificate is installed correctly, the application will install normally
   - If you encounter security warnings, the certificate was not installed correctly - repeat step 2

### Troubleshooting
- **If installation fails**: Ensure the certificate is installed in the "Trusted Root Certification Authorities" store
- **If problems persist**: Try running the certificate installation process as administrator
- **Windows 11/10 Requirements**: Ensure your system is updated to the latest version

## 🏗️ Project Structure

```
GameLauncher/
├── GameLauncher.sln              # Visual Studio Solution File
├── GameLauncher/                 # Main Project Directory
│   ├── GameLauncher.csproj       # Project Configuration File
│   ├── Package.appxmanifest      # MSIX Package Manifest
│   ├── app.manifest              # Application Manifest
│   │
│   ├── App.xaml                  # Application Entry XAML
│   ├── App.xaml.cs               # Application Entry Code
│   ├── MainWindow.xaml           # Main Window XAML
│   ├── MainWindow.xaml.cs        # Main Window Logic
│   │
│   ├── Pages/                    # Pages Directory
│   │   ├── GamesPage.xaml        # Game Management Page XAML
│   │   ├── GamesPage.xaml.cs     # Game Management Page Logic
│   │   ├── SettingsPage.xaml     # Settings Page XAML
│   │   └── SettingsPage.xaml.cs  # Settings Page Logic
│   │
│   ├── Services/                 # Service Layer
│   │   ├── SteamService.cs       # Steam Integration Service
│   │   └── ThemeService.cs       # Theme Management Service
│   │
│   ├── CustomDataObject.cs       # Game Data Model
│   ├── IconExtractor.cs          # Icon Extraction Tool
│   │
│   ├── Assets/                   # Application Resources
│   │   ├── *.png                 # Application Icons and Logos
│   │   └── ...
│   │
│   └── Properties/               # Project Properties
│       ├── launchSettings.json   # Launch Configuration
│       └── PublishProfiles/      # Publish Configuration Files
│           ├── win-x64.pubxml    # x64 Publish Configuration
│           ├── win-x86.pubxml    # x86 Publish Configuration
│           └── win-arm64.pubxml  # ARM64 Publish Configuration
```

## 🛠️ Technology Stack

### Frameworks & Platform
- **.NET 8.0**
- **WinUI 3**: Microsoft's modern Windows UI framework
- **Windows App SDK**: Windows platform feature support
- **MSIX**: Modern application packaging and distribution

### Development Tools
- **C# 12**: Modern C# language features
- **XAML**: Declarative UI markup language
- **Visual Studio 2022**: Integrated development environment

## 🚀 Build & Run

1. **Install Development Tools**:
   ```
   - Visual Studio 2022 (version 17.14 or higher)
   - Windows App SDK workload
   - .NET 8.0 SDK
   ```

2. **Clone Project**:
   ```bash
   git clone <repository-url>
   cd GameLauncher
   ```

3. **Open Solution**:
   ```bash
   GameLauncher.sln
   ```

## 📱 Usage Instructions

### First Launch
1. Start the application
2. The app will automatically detect Steam installation (if exists)
3. Choose to import Steam games or manually add games

### Adding Games
1. Click the "Add Game" button
2. Fill in the game name
3. Select the game's executable file (.exe)
4. The app will automatically extract the game icon

### Steam Game Import
1. Click the "Import Steam Games" button
2. The app will scan all Steam game libraries
3. Select the games to import

### Game Management
- **Launch Game**: Click on game card
- **Right-click Menu**: Access more options (delete, open directory, Steam store, etc.)
- **Batch Operations**: Use "Delete Games" mode for multi-selection operations
- **Clean Duplicates**: Use "Clean Duplicates" feature to remove duplicate game entries

### Settings Options
- **Theme Switching**: Select light, dark, or system theme in settings page
- **Other Settings**: View app information and version

## 📄 License

This project is licensed under the [GPL License](LICENSE.txt) - see the [LICENSE](LICENSE.txt) file for details.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using System.Xml.Linq;
using GameLauncher.Models;

namespace GameLauncher.Services
{
    public class XboxGame
    {
        public string PackageFamilyName { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public DateTime? LastActivity { get; set; }
        public ulong Playtime { get; set; } // 游戏时长（秒）
    }

    public static class XboxService
    {
        private const string XboxPassAppFamilyName = "Microsoft.GamingApp_8wekyb3d8bbwe";
        private static bool? _isXboxPassAppInstalled;

        /// <summary>
        /// 检查 Xbox PC Pass 应用是否已安装
        /// </summary>
        public static bool IsXboxPassAppInstalled
        {
            get
            {
                if (_isXboxPassAppInstalled.HasValue)
                    return _isXboxPassAppInstalled.Value;

                try
                {
                    var uwpApps = Programs.GetUWPApps();
                    var xboxApp = uwpApps.FirstOrDefault(app => app.AppId == XboxPassAppFamilyName);
                    _isXboxPassAppInstalled = xboxApp != null;
                    
                    System.Diagnostics.Debug.WriteLine($"Xbox Pass App 安装状态: {_isXboxPassAppInstalled}");
                    return _isXboxPassAppInstalled.Value;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"检查 Xbox Pass App 时出错: {ex.Message}");
                    _isXboxPassAppInstalled = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// 打开 Xbox PC Pass 应用
        /// </summary>
        public static bool OpenXboxPassApp()
        {
            try
            {
                var uwpApps = Programs.GetUWPApps();
                var xboxApp = uwpApps.FirstOrDefault(app => app.AppId == XboxPassAppFamilyName);
                
                if (xboxApp == null)
                {
                    System.Diagnostics.Debug.WriteLine("Xbox PC Pass 应用未安装");
                    return false;
                }

                if (!string.IsNullOrEmpty(xboxApp.Path))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = xboxApp.Path,
                        Arguments = xboxApp.Arguments,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                else
                {
                    // 使用协议启动
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"shell:appsFolder\\{XboxPassAppFamilyName}!App",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开 Xbox Pass App 时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 扫描 Xbox 游戏 - 整合 UWP 和目录扫描
        /// </summary>
        public static async Task<List<XboxGame>> ScanXboxGamesAsync()
        {
            var games = new List<XboxGame>();
            var foundIdentifiers = new HashSet<string>();

            System.Diagnostics.Debug.WriteLine("=== 开始全面扫描 Xbox 游戏 ===");

            // 方法1: 扫描已安装的 UWP Xbox 游戏
            await ScanInstalledXboxUwpGamesAsync(games, foundIdentifiers);

            // 方法2: 扫描所有驱动器的 XboxGames 目录
            await ScanXboxGamesDirectoriesAsync(games, foundIdentifiers);

            // 方法3: 通过注册表查找Xbox游戏安装路径
            await ScanXboxRegistryLocationsAsync(games, foundIdentifiers);

            System.Diagnostics.Debug.WriteLine($"=== Xbox 游戏扫描完成，总共找到 {games.Count} 个游戏 ===");
            
            // 按名称排序并返回
            return games.OrderBy(g => g.Name).ToList();
        }

        /// <summary>
        /// 扫描已安装的 UWP Xbox 游戏
        /// </summary>
        private static async Task ScanInstalledXboxUwpGamesAsync(List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("--- 开始扫描 UWP Xbox 游戏 ---");

                var uwpApps = Programs.GetUWPApps();
                var xboxGames = uwpApps.Where(app => app.IsGame && IsXboxRelatedApp(app)).ToList();

                foreach (var uwpGame in xboxGames)
                {
                    try
                    {
                        var game = CreateGameFromUwpApp(uwpGame);
                        if (game != null && !foundIdentifiers.Contains(game.AppId))
                        {
                            games.Add(game);
                            foundIdentifiers.Add(game.AppId);
                            System.Diagnostics.Debug.WriteLine($"通过 UWP 找到Xbox游戏: {game.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"处理 UWP Xbox 游戏时出错: {uwpGame.Name} - {ex.Message}");
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描 UWP Xbox 游戏时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断 UWP 应用是否为 Xbox 相关游戏
        /// </summary>
        private static bool IsXboxRelatedApp(UwpApp app)
        {
            if (!app.IsGame)
                return false;

            // 检查是否安装在 Xbox 相关目录
            if (!string.IsNullOrEmpty(app.WorkDir))
            {
                var lowerPath = app.WorkDir.ToLowerInvariant();
                if (lowerPath.Contains("xboxgames") || 
                    lowerPath.Contains("modifiablewindowsapps") ||
                    lowerPath.Contains("xbox"))
                {
                    return true;
                }
            }

            // 检查应用ID是否包含已知的Xbox游戏发布商
            var xboxPublishers = new[]
            {
                "microsoft", "xbox", "8wekyb3d8bbwe" // Microsoft的发布商ID
            };

            return xboxPublishers.Any(pub => app.AppId.ToLowerInvariant().Contains(pub));
        }

        /// <summary>
        /// 从 UWP 应用创建 Xbox 游戏对象
        /// </summary>
        private static XboxGame CreateGameFromUwpApp(UwpApp uwpApp)
        {
            try
            {
                // 尝试从安装目录找到最佳的可执行文件
                var executablePath = FindXboxGameExecutable(uwpApp.WorkDir, uwpApp.Name);
                if (string.IsNullOrEmpty(executablePath) && !string.IsNullOrEmpty(uwpApp.Path))
                {
                    executablePath = uwpApp.Path;
                }

                return new XboxGame
                {
                    PackageFamilyName = uwpApp.AppId,
                    AppId = uwpApp.AppId,
                    Name = CleanGameName(uwpApp.Name),
                    InstallLocation = uwpApp.WorkDir,
                    ExecutablePath = executablePath,
                    IconPath = uwpApp.Icon,
                    IsInstalled = true,
                    Version = "Unknown",
                    Publisher = "Microsoft"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从 UWP 应用创建游戏对象时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清理游戏名称，移除常见后缀
        /// </summary>
        private static string CleanGameName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return name
                .Replace("(PC)", "")
                .Replace("(Windows)", "")
                .Replace("for Windows 10", "")
                .Replace("- Windows 10", "")
                .Replace("?", "")
                .Replace("?", "")
                .Trim();
        }

        /// <summary>
        /// 扫描所有驱动器的 XboxGames 目录
        /// </summary>
        private static async Task ScanXboxGamesDirectoriesAsync(List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("--- 开始扫描 XboxGames 目录 ---");

                var xboxGamesPaths = new List<string>();

                // 获取所有固定驱动器
                var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady);

                foreach (var drive in drives)
                {
                    var possibleXboxPaths = new[]
                    {
                        // 主要的XboxGames目录位置
                        Path.Combine(drive.RootDirectory.FullName, "XboxGames"),
                        
                        // 可能的其他Xbox游戏目录
                        Path.Combine(drive.RootDirectory.FullName, "Program Files", "XboxGames"),
                        Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "XboxGames")
                    };

                    foreach (var path in possibleXboxPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            xboxGamesPaths.Add(path);
                            System.Diagnostics.Debug.WriteLine($"找到 Xbox 游戏目录: {path}");
                        }
                    }
                }

                // 并行扫描所有找到的Xbox游戏目录
                await Task.WhenAll(xboxGamesPaths.Select(async path => 
                    await ScanXboxGamesInDirectoryAsync(path, games, foundIdentifiers)));

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描 XboxGames 目录时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 扫描Xbox注册表位置
        /// </summary>
        private static async Task ScanXboxRegistryLocationsAsync(List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("--- 开始扫描 Xbox 注册表位置 ---");

                var registryPaths = new[]
                {
                    @"Software\Microsoft\GamingServices",
                    @"Software\Microsoft\Windows\CurrentVersion\GameDVR\KnownGames",
                    @"Software\Microsoft\XboxLive\Games"
                };

                foreach (var registryPath in registryPaths)
                {
                    await ScanXboxRegistryPathAsync(registryPath, games, foundIdentifiers);
                }

                // 获取从注册表中的Xbox游戏安装目录
                var xboxInstallDirs = GetXboxInstallDirectoriesFromRegistry();
                foreach (var dir in xboxInstallDirs)
                {
                    await ScanXboxGamesInDirectoryAsync(dir, games, foundIdentifiers);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描Xbox注册表时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 专门扫描XboxGames目录中的游戏
        /// </summary>
        private static async Task ScanXboxGamesInDirectoryAsync(string xboxGamesDirectory, List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                if (!Directory.Exists(xboxGamesDirectory))
                    return;

                System.Diagnostics.Debug.WriteLine($"扫描Xbox游戏目录: {xboxGamesDirectory}");

                var gameDirectories = Directory.GetDirectories(xboxGamesDirectory);
                
                await Task.Run(() =>
                {
                    Parallel.ForEach(gameDirectories, gameDir =>
                    {
                        try
                        {
                            var game = CreateXboxGameFromDirectory(gameDir);
                            if (game != null)
                            {
                                var identifier = !string.IsNullOrEmpty(game.PackageFamilyName) 
                                    ? game.PackageFamilyName 
                                    : $"Xbox_{game.Name}_{Path.GetFileName(gameDir)}".GetHashCode().ToString();

                                lock (foundIdentifiers)
                                {
                                    if (!foundIdentifiers.Contains(identifier))
                                    {
                                        games.Add(game);
                                        foundIdentifiers.Add(identifier);
                                        System.Diagnostics.Debug.WriteLine($"在 {Path.GetFileName(xboxGamesDirectory)} 中找到Xbox游戏: {game.Name}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"处理Xbox游戏目录时出错: {gameDir} - {ex.Message}");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描Xbox游戏目录时出错: {xboxGamesDirectory} - {ex.Message}");
            }
        }

        /// <summary>
        /// 从XboxGames目录创建游戏对象
        /// </summary>
        private static XboxGame? CreateXboxGameFromDirectory(string gameDirectory)
        {
            try
            {
                if (!Directory.Exists(gameDirectory))
                    return null;

                var directoryName = Path.GetFileName(gameDirectory);
                
                // 首先尝试从AppxManifest.xml获取信息
                var manifestPath = Path.Combine(gameDirectory, "AppxManifest.xml");
                if (File.Exists(manifestPath))
                {
                    var gameFromManifest = CreateGameFromXboxManifest(manifestPath, gameDirectory);
                    if (gameFromManifest != null)
                        return gameFromManifest;
                }

                // 检查是否有游戏可执行文件
                var executablePath = FindXboxGameExecutable(gameDirectory, directoryName);
                if (string.IsNullOrEmpty(executablePath))
                    return null;

                // 尝试从目录结构推断游戏信息
                var gameName = ExtractGameNameFromDirectory(gameDirectory, directoryName);

                return new XboxGame
                {
                    PackageFamilyName = $"Xbox_{directoryName}",
                    AppId = directoryName,
                    Name = CleanGameName(gameName),
                    InstallLocation = gameDirectory,
                    ExecutablePath = executablePath,
                    IsInstalled = true,
                    Version = "Unknown",
                    Publisher = "Unknown"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从Xbox目录创建游戏对象时出错: {gameDirectory} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从Xbox清单文件创建游戏对象
        /// </summary>
        private static XboxGame? CreateGameFromXboxManifest(string manifestPath, string gameDirectory)
        {
            try
            {
                var manifestDoc = XDocument.Load(manifestPath);
                var ns = manifestDoc.Root?.GetDefaultNamespace();
                
                if (ns == null) return null;

                var identity = manifestDoc.Root?.Element(ns + "Identity");
                var properties = manifestDoc.Root?.Element(ns + "Properties");
                var applications = manifestDoc.Root?.Element(ns + "Applications");

                if (identity == null) return null;

                var packageName = identity.Attribute("Name")?.Value ?? "";
                var publisher = identity.Attribute("Publisher")?.Value ?? "";
                var packageFamilyName = $"{packageName}_{publisher}";
                var version = identity.Attribute("Version")?.Value ?? "1.0.0.0";

                // 获取显示名称
                var displayName = properties?.Element(ns + "DisplayName")?.Value;
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = ExtractGameNameFromDirectory(gameDirectory, packageName);
                }

                // 查找可执行文件
                var executable = applications?.Elements(ns + "Application")
                    .FirstOrDefault()?.Attribute("Executable")?.Value;

                var executablePath = "";
                if (!string.IsNullOrEmpty(executable))
                {
                    executablePath = Path.Combine(gameDirectory, executable);
                    if (!File.Exists(executablePath))
                    {
                        executablePath = FindXboxGameExecutable(gameDirectory, displayName);
                    }
                }
                else
                {
                    executablePath = FindXboxGameExecutable(gameDirectory, displayName);
                }

                return new XboxGame
                {
                    PackageFamilyName = packageFamilyName,
                    AppId = packageName,
                    Name = CleanGameName(displayName),
                    InstallLocation = gameDirectory,
                    ExecutablePath = executablePath,
                    IsInstalled = true,
                    Version = version,
                    Publisher = publisher
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从Xbox清单创建游戏对象时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 专门查找Xbox游戏的可执行文件
        /// </summary>
        private static string FindXboxGameExecutable(string gamePath, string gameName)
        {
            try
            {
                if (!Directory.Exists(gamePath))
                    return string.Empty;

                var exeFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.AllDirectories);
                if (exeFiles.Length == 0)
                    return string.Empty;

                var candidates = new List<(string path, int priority, long size)>();

                foreach (var exeFile in exeFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(exeFile).ToLowerInvariant();
                    var fileInfo = new FileInfo(exeFile);
                    var priority = 0;

                    // 排除明显的非游戏文件
                    var excludePatterns = new[]
                    {
                        "unins", "setup", "install", "config", "crash", "report", 
                        "update", "patch", "tool", "util", "helper", "service",
                        "redist", "vcredist", "directx", "dotnet"
                    };

                    if (excludePatterns.Any(pattern => fileName.Contains(pattern)))
                        continue;

                    // Xbox游戏特定的优先级计算
                    var gameNameLower = gameName.ToLowerInvariant();
                    
                    if (fileName.Equals(gameNameLower))
                        priority += 100;
                    if (fileName.Contains(gameNameLower))
                        priority += 50;
                        
                    // Xbox游戏常见的可执行文件模式
                    var xboxGamePatterns = new[]
                    {
                        ("game", 30), ("main", 25), ("start", 20), ("play", 15),
                        ("launcher", 40), ("gamelaunchhelper", 45)
                    };

                    foreach (var (pattern, score) in xboxGamePatterns)
                    {
                        if (fileName.Contains(pattern))
                            priority += score;
                    }

                    // 文件大小考虑（Xbox游戏通常较大）
                    if (fileInfo.Length > 100 * 1024 * 1024) // > 100MB
                        priority += 25;
                    else if (fileInfo.Length > 50 * 1024 * 1024) // > 50MB
                        priority += 20;
                    else if (fileInfo.Length > 10 * 1024 * 1024) // > 10MB
                        priority += 10;
                    else if (fileInfo.Length > 1024 * 1024) // > 1MB
                        priority += 5;

                    // 偏好在根目录或bin目录的可执行文件
                    var relativePath = Path.GetRelativePath(gamePath, exeFile).ToLowerInvariant();
                    if (!relativePath.Contains(Path.DirectorySeparatorChar)) // 根目录
                        priority += 15;
                    else if (relativePath.Contains("bin"))
                        priority += 10;

                    candidates.Add((exeFile, priority, fileInfo.Length));
                }

                if (candidates.Count == 0)
                    return string.Empty;

                // 选择优先级最高的可执行文件
                var bestCandidate = candidates
                    .OrderByDescending(c => c.priority)
                    .ThenByDescending(c => c.size)
                    .First();

                System.Diagnostics.Debug.WriteLine($"为 {gameName} 选择可执行文件: {Path.GetFileName(bestCandidate.path)} (优先级: {bestCandidate.priority})");
                return bestCandidate.path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"查找Xbox游戏可执行文件时出错: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 从目录名提取游戏名称
        /// </summary>
        private static string ExtractGameNameFromDirectory(string gameDirectory, string directoryName)
        {
            try
            {
                // 尝试从多个来源获取更友好的游戏名称
                
                // 1. 检查是否有显示名称文件
                var displayNameFiles = new[] { "DisplayName.txt", "GameName.txt", "Title.txt" };
                foreach (var file in displayNameFiles)
                {
                    var filePath = Path.Combine(gameDirectory, file);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var content = File.ReadAllText(filePath).Trim();
                            if (!string.IsNullOrEmpty(content))
                                return content;
                        }
                        catch { }
                    }
                }

                // 2. 尝试从包目录名称中提取（通常格式为 GameName_版本号_架构）
                var parts = directoryName.Split('_');
                if (parts.Length > 0)
                {
                    var gameName = parts[0];
                    // 清理常见的后缀
                    gameName = gameName.Replace("Game", "").Replace("App", "").Trim();
                    if (!string.IsNullOrEmpty(gameName))
                        return gameName;
                }

                // 3. 直接使用目录名
                return directoryName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"提取游戏名称时出错: {ex.Message}");
                return directoryName;
            }
        }

        /// <summary>
        /// 扫描Xbox注册表路径
        /// </summary>
        private static async Task ScanXboxRegistryPathAsync(string registryPath, List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(registryPath);
                if (key == null) 
                {
                    // 也尝试LocalMachine
                    using var lmKey = Registry.LocalMachine.OpenSubKey(registryPath);
                    if (lmKey == null) return;
                    
                    await ProcessRegistryKey(lmKey, games, foundIdentifiers);
                    return;
                }

                await ProcessRegistryKey(key, games, foundIdentifiers);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描Xbox注册表路径时出错: {registryPath} - {ex.Message}");
            }
        }

        /// <summary>
        /// 处理注册表键
        /// </summary>
        private static async Task ProcessRegistryKey(RegistryKey key, List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var installPath = subKey.GetValue("InstallPath") as string ?? 
                                    subKey.GetValue("InstallLocation") as string ?? 
                                    subKey.GetValue("GamePath") as string;
                    
                    if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                    {
                        await ScanXboxGamesInDirectoryAsync(installPath, games, foundIdentifiers);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"处理注册表子键时出错: {subKeyName} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取Xbox游戏安装目录
        /// </summary>
        private static List<string> GetXboxInstallDirectoriesFromRegistry()
        {
            var directories = new List<string>();
            
            try
            {
                var registryKeys = new[]
                {
                    Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GamingServices"),
                    Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\GamingServices"),
                    Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR"),
                    Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR")
                };

                foreach (var key in registryKeys)
                {
                    if (key == null) continue;
                    
                    using (key)
                    {
                        // Xbox游戏默认安装位置
                        var defaultLocation = key.GetValue("DefaultInstallLocation") as string ??
                                             key.GetValue("XboxGamesPath") as string;
                        if (!string.IsNullOrEmpty(defaultLocation) && Directory.Exists(defaultLocation))
                        {
                            directories.Add(defaultLocation);
                        }

                        // 扫描挂载点和游戏库
                        var subKeyNames = new[] { "MountPoints", "GameLibraries", "InstallLocations" };
                        foreach (var subKeyName in subKeyNames)
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey != null)
                            {
                                foreach (var valueName in subKey.GetValueNames())
                                {
                                    var path = subKey.GetValue(valueName) as string;
                                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                                    {
                                        directories.Add(path);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从注册表获取Xbox目录时出错: {ex.Message}");
            }

            return directories.Distinct().ToList();
        }

        /// <summary>
        /// 获取游戏的启动参数
        /// </summary>
        public static string GetGameLaunchArguments(XboxGame game)
        {
            if (string.IsNullOrEmpty(game.PackageFamilyName))
                return string.Empty;

            return $"shell:appsFolder\\{game.PackageFamilyName}!App";
        }

        /// <summary>
        /// 检查游戏是否正在运行
        /// </summary>
        public static bool IsGameRunning(XboxGame game)
        {
            try
            {
                if (string.IsNullOrEmpty(game.ExecutablePath))
                    return false;

                var processName = Path.GetFileNameWithoutExtension(game.ExecutablePath);
                var processes = Process.GetProcessesByName(processName);
                
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查游戏运行状态时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启动 Xbox 游戏
        /// </summary>
        public static bool LaunchXboxGame(string packageFamilyName)
        {
            try
            {
                // 使用 Windows 应用协议启动游戏
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"shell:appsFolder\\{packageFamilyName}!App",
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动 Xbox 游戏时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 通过可执行文件启动游戏（备选方案）
        /// </summary>
        public static bool LaunchXboxGameByExecutable(string executablePath)
        {
            try
            {
                if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                {
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通过可执行文件启动 Xbox 游戏时出错: {ex.Message}");
                return false;
            }
        }
    }
}
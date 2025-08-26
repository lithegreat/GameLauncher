using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;

namespace GameLauncher.Services
{
    public class SteamGame
    {
        public string AppId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string InstallDir { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
    }

    public static class SteamService
    {
        /// <summary>
        /// 检测 Steam 是否已安装
        /// </summary>
        public static bool IsSteamInstalled()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始检测 Steam 安装状态");

                // 检查注册表中的 Steam 安装路径
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var steamPath = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"从注册表找到 Steam 路径: {steamPath}");
                        return true;
                    }
                }

                // 检查默认安装路径
                var defaultPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
                };

                foreach (var path in defaultPaths)
                {
                    if (Directory.Exists(path) && File.Exists(Path.Combine(path, "Steam.exe")))
                    {
                        System.Diagnostics.Debug.WriteLine($"在默认路径找到 Steam: {path}");
                        return true;
                    }
                }

                System.Diagnostics.Debug.WriteLine("未找到 Steam 安装");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检测 Steam 安装时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取 Steam 安装路径
        /// </summary>
        public static string? GetSteamInstallPath()
        {
            try
            {
                // 首先尝试从注册表获取
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var steamPath = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        return steamPath.Replace('/', '\\'); // 统一使用 Windows 路径分隔符
                    }
                }

                // 检查默认路径
                var defaultPaths = new[]
                {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam"
                };

                foreach (var path in defaultPaths)
                {
                    if (Directory.Exists(path) && File.Exists(Path.Combine(path, "Steam.exe")))
                    {
                        return path;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取 Steam 路径时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取 Steam 游戏库路径列表
        /// </summary>
        public static List<string> GetSteamLibraryPaths()
        {
            var libraryPaths = new List<string>();

            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    return libraryPaths;
                }

                // 添加默认的 Steam 安装目录
                var defaultLibraryPath = Path.Combine(steamPath, "steamapps");
                if (Directory.Exists(defaultLibraryPath))
                {
                    // 标准化路径
                    var normalizedPath = Path.GetFullPath(defaultLibraryPath);
                    libraryPaths.Add(normalizedPath);
                    System.Diagnostics.Debug.WriteLine($"添加默认库路径: {normalizedPath}");
                }

                // 读取 libraryfolders.vdf 文件获取其他库路径
                var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFoldersPath))
                {
                    var vdfContent = File.ReadAllText(libraryFoldersPath);
                    var pathMatches = Regex.Matches(vdfContent, @"""path""\s*""([^""]+)""");

                    foreach (Match match in pathMatches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var path = match.Groups[1].Value.Replace("\\\\", "\\");
                            var steamappsPath = Path.Combine(path, "steamapps");
                            
                            if (Directory.Exists(steamappsPath))
                            {
                                // 标准化路径并检查重复
                                var normalizedPath = Path.GetFullPath(steamappsPath);
                                if (!libraryPaths.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase)))
                                {
                                    libraryPaths.Add(normalizedPath);
                                    System.Diagnostics.Debug.WriteLine($"添加库路径: {normalizedPath}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"跳过重复库路径: {normalizedPath}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取 Steam 库路径时出错: {ex.Message}");
            }

            return libraryPaths;
        }

        /// <summary>
        /// 扫描 Steam 游戏
        /// </summary>
        public static async Task<List<SteamGame>> ScanSteamGamesAsync()
        {
            var games = new List<SteamGame>();

            try
            {
                System.Diagnostics.Debug.WriteLine("开始扫描 Steam 游戏");

                var libraryPaths = GetSteamLibraryPaths();
                if (libraryPaths.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("未找到 Steam 库路径");
                    return games;
                }

                foreach (var libraryPath in libraryPaths)
                {
                    System.Diagnostics.Debug.WriteLine($"扫描库路径: {libraryPath}");
                    var libraryGames = await ScanLibraryAsync(libraryPath);
                    games.AddRange(libraryGames);
                }

                System.Diagnostics.Debug.WriteLine($"共找到 {games.Count} 个 Steam 游戏");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描 Steam 游戏时出错: {ex.Message}");
            }

            return games;
        }

        /// <summary>
        /// 扫描单个库目录
        /// </summary>
        private static async Task<List<SteamGame>> ScanLibraryAsync(string libraryPath)
        {
            var games = new List<SteamGame>();

            try
            {
                var acfFiles = Directory.GetFiles(libraryPath, "appmanifest_*.acf");
                System.Diagnostics.Debug.WriteLine($"在 {libraryPath} 找到 {acfFiles.Length} 个 ACF 文件");

                foreach (var acfFile in acfFiles)
                {
                    try
                    {
                        var game = await ParseAcfFileAsync(acfFile, libraryPath);
                        if (game != null && game.IsInstalled)
                        {
                            games.Add(game);
                            System.Diagnostics.Debug.WriteLine($"找到游戏: {game.Name} ({game.AppId})");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"解析 ACF 文件 {acfFile} 时出错: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描库目录 {libraryPath} 时出错: {ex.Message}");
            }

            return games;
        }

        /// <summary>
        /// 解析 ACF 文件
        /// </summary>
        private static async Task<SteamGame?> ParseAcfFileAsync(string acfFilePath, string libraryPath)
        {
            try
            {
                var content = await File.ReadAllTextAsync(acfFilePath);
                
                // 提取 AppID
                var appIdMatch = Regex.Match(content, @"""appid""\s*""(\d+)""");
                if (!appIdMatch.Success)
                    return null;

                var appId = appIdMatch.Groups[1].Value;

                // 提取游戏名称
                var nameMatch = Regex.Match(content, @"""name""\s*""([^""]+)""");
                if (!nameMatch.Success)
                    return null;

                var name = nameMatch.Groups[1].Value;

                // 提取安装目录
                var installDirMatch = Regex.Match(content, @"""installdir""\s*""([^""]+)""");
                if (!installDirMatch.Success)
                    return null;

                var installDir = installDirMatch.Groups[1].Value;

                // 检查状态
                var stateMatch = Regex.Match(content, @"""StateFlags""\s*""(\d+)""");
                var isInstalled = true;
                if (stateMatch.Success)
                {
                    var stateFlags = int.Parse(stateMatch.Groups[1].Value);
                    // StateFlags 为 4 表示已完全安装
                    isInstalled = (stateFlags & 4) == 4;
                }

                if (!isInstalled)
                    return null;

                // 构建游戏路径
                var gamePath = Path.Combine(libraryPath, "common", installDir);
                if (!Directory.Exists(gamePath))
                    return null;

                // 查找可执行文件
                var executablePath = FindGameExecutable(gamePath, name);

                var game = new SteamGame
                {
                    AppId = appId,
                    Name = name,
                    InstallDir = installDir,
                    ExecutablePath = executablePath,
                    IsInstalled = isInstalled
                };

                return game;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析 ACF 文件时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 查找游戏可执行文件
        /// </summary>
        private static string FindGameExecutable(string gamePath, string gameName)
        {
            try
            {
                // 标准化游戏路径
                gamePath = Path.GetFullPath(gamePath);
                
                // 常见的可执行文件模式
                var patterns = new[]
                {
                    $"{gameName}.exe",
                    $"{gameName.Replace(" ", "")}.exe",
                    $"{gameName.Replace(" ", "_")}.exe",
                    "*.exe"
                };

                foreach (var pattern in patterns)
                {
                    var files = Directory.GetFiles(gamePath, pattern, SearchOption.TopDirectoryOnly);
                    
                    // 排除一些不相关的可执行文件
                    var excludePatterns = new[] { "unins", "redist", "vcredist", "directx", "crash", "error", "setup" };
                    
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                        if (!excludePatterns.Any(exclude => fileName.Contains(exclude)))
                        {
                            var normalizedPath = Path.GetFullPath(file);
                            System.Diagnostics.Debug.WriteLine($"找到可执行文件: {normalizedPath}");
                            return normalizedPath;
                        }
                    }
                }

                // 如果在根目录找不到，尝试在子目录中查找
                var subDirs = Directory.GetDirectories(gamePath);
                foreach (var subDir in subDirs)
                {
                    var subDirName = Path.GetFileName(subDir).ToLower();
                    if (subDirName.Contains("bin") || subDirName.Contains("game") || 
                        subDirName.Contains(gameName.ToLower().Replace(" ", "")))
                    {
                        var subFiles = Directory.GetFiles(subDir, "*.exe", SearchOption.TopDirectoryOnly);
                        if (subFiles.Length > 0)
                        {
                            var normalizedPath = Path.GetFullPath(subFiles[0]);
                            System.Diagnostics.Debug.WriteLine($"在子目录找到可执行文件: {normalizedPath}");
                            return normalizedPath;
                        }
                    }
                }

                // 最后尝试递归查找（限制深度为2层）
                var allExeFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.AllDirectories)
                                            .Where(f => Path.GetDirectoryName(f)?.Split(Path.DirectorySeparatorChar).Length <= 
                                                       gamePath.Split(Path.DirectorySeparatorChar).Length + 2)
                                            .ToArray();

                if (allExeFiles.Length > 0)
                {
                    // 选择最可能的可执行文件
                    var bestMatch = allExeFiles
                        .OrderBy(f => Path.GetDirectoryName(f)?.Count(c => c == Path.DirectorySeparatorChar) ?? 0) // 优先选择层级较浅的
                        .ThenByDescending(f => new FileInfo(f).Length) // 然后选择较大的文件
                        .FirstOrDefault();

                    if (bestMatch != null)
                    {
                        var normalizedPath = Path.GetFullPath(bestMatch);
                        System.Diagnostics.Debug.WriteLine($"递归查找到可执行文件: {normalizedPath}");
                        return normalizedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"查找游戏可执行文件时出错: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// 启动 Steam 游戏
        /// </summary>
        public static bool LaunchSteamGame(string appId)
        {
            try
            {
                var steamProtocolUrl = $"steam://rungameid/{appId}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = steamProtocolUrl,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动 Steam 游戏时出错: {ex.Message}");
                return false;
            }
        }
    }
}
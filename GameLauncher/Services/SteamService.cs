using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Globalization;

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
        public ulong Playtime { get; set; } // 游戏时长（分钟）
        public DateTime? LastActivity { get; set; } // 最后游玩时间
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }

    public class SteamUser
    {
        public ulong SteamId { get; set; }
        public string PersonaName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public bool IsRecent { get; set; }
    }

    public class SteamApiGame
    {
        public uint appid { get; set; }
        public string name { get; set; } = string.Empty;
        public uint playtime_forever { get; set; }
        public uint playtime_2weeks { get; set; }
        public string img_icon_url { get; set; } = string.Empty;
    }

    public class SteamApiResponse
    {
        public SteamApiGameList? response { get; set; }
    }

    public class SteamApiGameList
    {
        public uint game_count { get; set; }
        public List<SteamApiGame>? games { get; set; }
    }

    public static class SteamService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static string? _cachedSteamPath;
        private static List<string>? _cachedLibraryPaths;

        static SteamService()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// 检查 Steam 是否已安装
        /// </summary>
        public static bool IsSteamInstalled()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始检查 Steam 安装状态");

                // 从注册表中的 Steam 安装路径
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
                        System.Diagnostics.Debug.WriteLine($"从默认路径找到 Steam: {path}");
                        return true;
                    }
                }

                System.Diagnostics.Debug.WriteLine("未找到 Steam 安装");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查 Steam 安装时出错: {ex.Message}");
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
                if (!string.IsNullOrEmpty(_cachedSteamPath))
                    return _cachedSteamPath;

                // 首先尝试从注册表获取
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var steamPath = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        _cachedSteamPath = steamPath.Replace('/', '\\'); // 统一使用 Windows 路径分隔符
                        return _cachedSteamPath;
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
                        _cachedSteamPath = path;
                        return _cachedSteamPath;
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
            try
            {
                if (_cachedLibraryPaths != null)
                    return _cachedLibraryPaths;

                var libraryPaths = new List<string>();

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

                // 获取 libraryfolders.vdf 文件来获取额外的路径
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
                                // 标准化路径，避免重复
                                var normalizedPath = Path.GetFullPath(steamappsPath);
                                if (!libraryPaths.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase)))
                                {
                                    libraryPaths.Add(normalizedPath);
                                    System.Diagnostics.Debug.WriteLine($"添加库路径: {normalizedPath}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"跳过重复的路径: {normalizedPath}");
                                }
                            }
                        }
                    }
                }

                _cachedLibraryPaths = libraryPaths;
                return libraryPaths;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取 Steam 库路径时出错: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// 获取本地 Steam 用户列表
        /// </summary>
        public static List<SteamUser> GetSteamUsers()
        {
            var users = new List<SteamUser>();

            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                    return users;

                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (!File.Exists(loginUsersPath))
                    return users;

                var content = File.ReadAllText(loginUsersPath);
                
                // 简单的VDF解析来获取用户信息
                var userMatches = Regex.Matches(content, @"""(\d+)""\s*\{([^}]+)\}");
                
                foreach (Match match in userMatches)
                {
                    if (match.Groups.Count > 2)
                    {
                        var steamId = ulong.Parse(match.Groups[1].Value);
                        var userBlock = match.Groups[2].Value;
                        
                        var personaNameMatch = Regex.Match(userBlock, @"""PersonaName""\s*""([^""]+)""");
                        var accountNameMatch = Regex.Match(userBlock, @"""AccountName""\s*""([^""]+)""");
                        var recentMatch = Regex.Match(userBlock, @"""MostRecent""\s*""([^""]+)""");

                        users.Add(new SteamUser
                        {
                            SteamId = steamId,
                            PersonaName = personaNameMatch.Success ? personaNameMatch.Groups[1].Value : "",
                            AccountName = accountNameMatch.Success ? accountNameMatch.Groups[1].Value : "",
                            IsRecent = recentMatch.Success && recentMatch.Groups[1].Value == "1"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取 Steam 用户时出错: {ex.Message}");
            }

            return users;
        }

        /// <summary>
        /// 从本地配置获取游戏最后活动时间
        /// </summary>
        public static Dictionary<string, DateTime> GetGamesLastActivity(ulong steamId)
        {
            var result = new Dictionary<string, DateTime>();

            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                    return result;

                // 计算Account ID (SteamID的低32位)
                var accountId = steamId & 0xFFFFFFFF;
                var configPath = Path.Combine(steamPath, "userdata", accountId.ToString(), "config", "localconfig.vdf");

                if (!File.Exists(configPath))
                    return result;

                var content = File.ReadAllText(configPath);
                
                // 查找apps段落
                var appsMatch = Regex.Match(content, @"""apps""\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}");
                if (!appsMatch.Success)
                    return result;

                var appsContent = appsMatch.Groups[1].Value;
                
                // 匹配每个应用的信息
                var appMatches = Regex.Matches(appsContent, @"""(\d+(?:_\d+)?)""\s*\{([^}]+)\}");
                
                foreach (Match match in appMatches)
                {
                    if (match.Groups.Count > 2)
                    {
                        var appId = match.Groups[1].Value;
                        var appBlock = match.Groups[2].Value;
                        
                        var lastPlayedMatch = Regex.Match(appBlock, @"""LastPlayed""\s*""(\d+)""");
                        if (lastPlayedMatch.Success && long.TryParse(lastPlayedMatch.Groups[1].Value, out var timestamp))
                        {
                            var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                            if (dateTime.Year > 1970) // 有效的时间戳
                            {
                                result[appId] = dateTime;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取游戏最后活动时间时出错: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 通过 Steam Web API 获取用户的游戏列表和游玩时长
        /// </summary>
        public static async Task<List<SteamApiGame>?> GetOwnedGamesFromWebApiAsync(ulong steamId, string apiKey)
        {
            try
            {
                var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={apiKey}&steamid={steamId}&include_appinfo=1&include_played_free_games=1&format=json";
                
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        var response = await _httpClient.GetStringAsync(url);
                        var apiResponse = JsonSerializer.Deserialize<SteamApiResponse>(response);
                        
                        if (apiResponse?.response?.games != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"从 Steam Web API 获取到 {apiResponse.response.games.Count} 个游戏");
                            return apiResponse.response.games;
                        }
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                    {
                        // Rate limited, wait and retry
                        System.Diagnostics.Debug.WriteLine($"Steam API 限流，等待后重试 (尝试 {retry + 1}/3)");
                        await Task.Delay(5000);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Steam Web API 请求失败 (尝试 {retry + 1}/3): {ex.Message}");
                        if (retry == 2) throw;
                        await Task.Delay(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Steam Web API 请求失败: {ex.Message}");
            }

            return null;
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
                
                // 获取 AppID
                var appIdMatch = Regex.Match(content, @"""appid""\s*""(\d+)""");
                if (!appIdMatch.Success)
                    return null;

                var appId = appIdMatch.Groups[1].Value;

                // 获取游戏名称
                var nameMatch = Regex.Match(content, @"""name""\s*""([^""]+)""");
                if (!nameMatch.Success)
                    return null;

                var name = nameMatch.Groups[1].Value;

                // 获取安装目录
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
                    // StateFlags 为 4 表示完全安装
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
        /// 查找游戏的可执行文件
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
                    
                    // 排除一些无关的可执行文件
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

                // 如果在根目录找不到，则在子目录中查找
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
                        System.Diagnostics.Debug.WriteLine($"递归找到可执行文件: {normalizedPath}");
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

        /// <summary>
        /// 同步 Steam 游戏时长和最后活动时间
        /// </summary>
        public static async Task<Dictionary<string, (ulong playtime, DateTime? lastActivity)>> SyncGamePlaytimeAsync(ulong steamId, string? apiKey = null)
        {
            var result = new Dictionary<string, (ulong playtime, DateTime? lastActivity)>();

            try
            {
                System.Diagnostics.Debug.WriteLine($"开始同步 Steam 用户 {steamId} 的游戏时长");

                // 获取本地最后活动时间
                var localLastActivity = GetGamesLastActivity(steamId);
                System.Diagnostics.Debug.WriteLine($"从本地配置获取到 {localLastActivity.Count} 个游戏的最后活动时间");

                // 如果有API Key，尝试从Web API获取游玩时长
                if (!string.IsNullOrEmpty(apiKey))
                {
                    var webApiGames = await GetOwnedGamesFromWebApiAsync(steamId, apiKey);
                    if (webApiGames != null)
                    {
                        foreach (var game in webApiGames)
                        {
                            var appId = game.appid.ToString();
                            var playtime = (ulong)game.playtime_forever; // Web API返回的是分钟
                            
                            localLastActivity.TryGetValue(appId, out var lastActivity);
                            
                            result[appId] = (playtime, lastActivity);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"从 Steam Web API 同步了 {webApiGames.Count} 个游戏的时长信息");
                    }
                }
                else
                {
                    // 没有API Key时，只能获取本地的最后活动时间，时长设为0
                    foreach (var kvp in localLastActivity)
                    {
                        result[kvp.Key] = (0, kvp.Value);
                    }
                    
                    System.Diagnostics.Debug.WriteLine("没有 Steam API Key，只同步了最后活动时间");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"同步 Steam 游戏时长时出错: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 更新游戏列表的时长信息
        /// </summary>
        public static async Task UpdateGamesWithPlaytimeAsync(List<SteamGame> games, ulong steamId, string? apiKey = null)
        {
            try
            {
                var playtimeData = await SyncGamePlaytimeAsync(steamId, apiKey);
                
                foreach (var game in games)
                {
                    if (playtimeData.TryGetValue(game.AppId, out var data))
                    {
                        game.Playtime = data.playtime;
                        game.LastActivity = data.lastActivity;
                        
                        System.Diagnostics.Debug.WriteLine($"更新游戏 {game.Name}: 时长 {data.playtime} 分钟, 最后游玩 {data.lastActivity?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知"}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新游戏时长信息时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化游玩时长为可读字符串
        /// </summary>
        public static string FormatPlaytime(ulong playtimeMinutes)
        {
            if (playtimeMinutes == 0)
                return "未游玩";

            var hours = playtimeMinutes / 60;
            var minutes = playtimeMinutes % 60;

            if (hours == 0)
                return $"{minutes} 分钟";
            
            if (minutes == 0)
                return $"{hours} 小时";
                
            return $"{hours} 小时 {minutes} 分钟";
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public static void ClearCache()
        {
            _cachedSteamPath = null;
            _cachedLibraryPaths = null;
        }
    }
}
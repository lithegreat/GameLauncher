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
        public ulong Playtime { get; set; } // ��Ϸʱ�������ӣ�
        public DateTime? LastActivity { get; set; } // �������ʱ��
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
        /// ��� Steam �Ƿ��Ѱ�װ
        /// </summary>
        public static bool IsSteamInstalled()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("��ʼ��� Steam ��װ״̬");

                // ��ע����е� Steam ��װ·��
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var steamPath = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"��ע����ҵ� Steam ·��: {steamPath}");
                        return true;
                    }
                }

                // ���Ĭ�ϰ�װ·��
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
                        System.Diagnostics.Debug.WriteLine($"��Ĭ��·���ҵ� Steam: {path}");
                        return true;
                    }
                }

                System.Diagnostics.Debug.WriteLine("δ�ҵ� Steam ��װ");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��� Steam ��װʱ����: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��ȡ Steam ��װ·��
        /// </summary>
        public static string? GetSteamInstallPath()
        {
            try
            {
                if (!string.IsNullOrEmpty(_cachedSteamPath))
                    return _cachedSteamPath;

                // ���ȳ��Դ�ע����ȡ
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var steamPath = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        _cachedSteamPath = steamPath.Replace('/', '\\'); // ͳһʹ�� Windows ·���ָ���
                        return _cachedSteamPath;
                    }
                }

                // ���Ĭ��·��
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
                System.Diagnostics.Debug.WriteLine($"��ȡ Steam ·��ʱ����: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ��ȡ Steam ��Ϸ��·���б�
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

                // ���Ĭ�ϵ� Steam ��װĿ¼
                var defaultLibraryPath = Path.Combine(steamPath, "steamapps");
                if (Directory.Exists(defaultLibraryPath))
                {
                    // ��׼��·��
                    var normalizedPath = Path.GetFullPath(defaultLibraryPath);
                    libraryPaths.Add(normalizedPath);
                    System.Diagnostics.Debug.WriteLine($"���Ĭ�Ͽ�·��: {normalizedPath}");
                }

                // ��ȡ libraryfolders.vdf �ļ�����ȡ�����·��
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
                                // ��׼��·���������ظ�
                                var normalizedPath = Path.GetFullPath(steamappsPath);
                                if (!libraryPaths.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase)))
                                {
                                    libraryPaths.Add(normalizedPath);
                                    System.Diagnostics.Debug.WriteLine($"��ӿ�·��: {normalizedPath}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"�����ظ���·��: {normalizedPath}");
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
                System.Diagnostics.Debug.WriteLine($"��ȡ Steam ��·��ʱ����: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// ��ȡ���� Steam �û��б�
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
                
                // �򵥵�VDF��������ȡ�û���Ϣ
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
                System.Diagnostics.Debug.WriteLine($"��ȡ Steam �û�ʱ����: {ex.Message}");
            }

            return users;
        }

        /// <summary>
        /// �ӱ������û�ȡ��Ϸ���ʱ��
        /// </summary>
        public static Dictionary<string, DateTime> GetGamesLastActivity(ulong steamId)
        {
            var result = new Dictionary<string, DateTime>();

            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                    return result;

                // ����Account ID (SteamID�ĵ�32λ)
                var accountId = steamId & 0xFFFFFFFF;
                var configPath = Path.Combine(steamPath, "userdata", accountId.ToString(), "config", "localconfig.vdf");

                if (!File.Exists(configPath))
                    return result;

                var content = File.ReadAllText(configPath);
                
                // ����apps����
                var appsMatch = Regex.Match(content, @"""apps""\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}");
                if (!appsMatch.Success)
                    return result;

                var appsContent = appsMatch.Groups[1].Value;
                
                // ƥ��ÿ��Ӧ�õ���Ϣ
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
                            if (dateTime.Year > 1970) // ��Ч��ʱ���
                            {
                                result[appId] = dateTime;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ȡ��Ϸ���ʱ��ʱ����: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// ͨ�� Steam Web API ��ȡ�û�����Ϸ�б������ʱ��
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
                            System.Diagnostics.Debug.WriteLine($"�� Steam Web API ��ȡ�� {apiResponse.response.games.Count} ����Ϸ");
                            return apiResponse.response.games;
                        }
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                    {
                        // Rate limited, wait and retry
                        System.Diagnostics.Debug.WriteLine($"Steam API �������ȴ������� (���� {retry + 1}/3)");
                        await Task.Delay(5000);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Steam Web API ����ʧ�� (���� {retry + 1}/3): {ex.Message}");
                        if (retry == 2) throw;
                        await Task.Delay(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Steam Web API ����ʧ��: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// ɨ�� Steam ��Ϸ
        /// </summary>
        public static async Task<List<SteamGame>> ScanSteamGamesAsync()
        {
            var games = new List<SteamGame>();

            try
            {
                System.Diagnostics.Debug.WriteLine("��ʼɨ�� Steam ��Ϸ");

                var libraryPaths = GetSteamLibraryPaths();
                if (libraryPaths.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("δ�ҵ� Steam ��·��");
                    return games;
                }

                foreach (var libraryPath in libraryPaths)
                {
                    System.Diagnostics.Debug.WriteLine($"ɨ���·��: {libraryPath}");
                    var libraryGames = await ScanLibraryAsync(libraryPath);
                    games.AddRange(libraryGames);
                }

                System.Diagnostics.Debug.WriteLine($"���ҵ� {games.Count} �� Steam ��Ϸ");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɨ�� Steam ��Ϸʱ����: {ex.Message}");
            }

            return games;
        }

        /// <summary>
        /// ɨ�赥����Ŀ¼
        /// </summary>
        private static async Task<List<SteamGame>> ScanLibraryAsync(string libraryPath)
        {
            var games = new List<SteamGame>();

            try
            {
                var acfFiles = Directory.GetFiles(libraryPath, "appmanifest_*.acf");
                System.Diagnostics.Debug.WriteLine($"�� {libraryPath} �ҵ� {acfFiles.Length} �� ACF �ļ�");

                foreach (var acfFile in acfFiles)
                {
                    try
                    {
                        var game = await ParseAcfFileAsync(acfFile, libraryPath);
                        if (game != null && game.IsInstalled)
                        {
                            games.Add(game);
                            System.Diagnostics.Debug.WriteLine($"�ҵ���Ϸ: {game.Name} ({game.AppId})");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"���� ACF �ļ� {acfFile} ʱ����: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɨ���Ŀ¼ {libraryPath} ʱ����: {ex.Message}");
            }

            return games;
        }

        /// <summary>
        /// ���� ACF �ļ�
        /// </summary>
        private static async Task<SteamGame?> ParseAcfFileAsync(string acfFilePath, string libraryPath)
        {
            try
            {
                var content = await File.ReadAllTextAsync(acfFilePath);
                
                // ��ȡ AppID
                var appIdMatch = Regex.Match(content, @"""appid""\s*""(\d+)""");
                if (!appIdMatch.Success)
                    return null;

                var appId = appIdMatch.Groups[1].Value;

                // ��ȡ��Ϸ����
                var nameMatch = Regex.Match(content, @"""name""\s*""([^""]+)""");
                if (!nameMatch.Success)
                    return null;

                var name = nameMatch.Groups[1].Value;

                // ��ȡ��װĿ¼
                var installDirMatch = Regex.Match(content, @"""installdir""\s*""([^""]+)""");
                if (!installDirMatch.Success)
                    return null;

                var installDir = installDirMatch.Groups[1].Value;

                // ���״̬
                var stateMatch = Regex.Match(content, @"""StateFlags""\s*""(\d+)""");
                var isInstalled = true;
                if (stateMatch.Success)
                {
                    var stateFlags = int.Parse(stateMatch.Groups[1].Value);
                    // StateFlags Ϊ 4 ��ʾ��ȫ��װ
                    isInstalled = (stateFlags & 4) == 4;
                }

                if (!isInstalled)
                    return null;

                // ������Ϸ·��
                var gamePath = Path.Combine(libraryPath, "common", installDir);
                if (!Directory.Exists(gamePath))
                    return null;

                // ���ҿ�ִ���ļ�
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
                System.Diagnostics.Debug.WriteLine($"���� ACF �ļ�ʱ����: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ������Ϸ�Ŀ�ִ���ļ�
        /// </summary>
        private static string FindGameExecutable(string gamePath, string gameName)
        {
            try
            {
                // ��׼����Ϸ·��
                gamePath = Path.GetFullPath(gamePath);
                
                // �����Ŀ�ִ���ļ�ģʽ
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
                    
                    // �ų�һЩ�޹صĿ�ִ���ļ�
                    var excludePatterns = new[] { "unins", "redist", "vcredist", "directx", "crash", "error", "setup" };
                    
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                        if (!excludePatterns.Any(exclude => fileName.Contains(exclude)))
                        {
                            var normalizedPath = Path.GetFullPath(file);
                            System.Diagnostics.Debug.WriteLine($"�ҵ���ִ���ļ�: {normalizedPath}");
                            return normalizedPath;
                        }
                    }
                }

                // ����ڸ�Ŀ¼�Ҳ�����������Ŀ¼�в���
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
                            System.Diagnostics.Debug.WriteLine($"����Ŀ¼�ҵ���ִ���ļ�: {normalizedPath}");
                            return normalizedPath;
                        }
                    }
                }

                // ����Եݹ���ң��������Ϊ2�㣩
                var allExeFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.AllDirectories)
                                            .Where(f => Path.GetDirectoryName(f)?.Split(Path.DirectorySeparatorChar).Length <= 
                                                       gamePath.Split(Path.DirectorySeparatorChar).Length + 2)
                                            .ToArray();

                if (allExeFiles.Length > 0)
                {
                    // ѡ������ܵĿ�ִ���ļ�
                    var bestMatch = allExeFiles
                        .OrderBy(f => Path.GetDirectoryName(f)?.Count(c => c == Path.DirectorySeparatorChar) ?? 0) // ����ѡ��㼶��ǳ��
                        .ThenByDescending(f => new FileInfo(f).Length) // Ȼ��ѡ��ϴ���ļ�
                        .FirstOrDefault();

                    if (bestMatch != null)
                    {
                        var normalizedPath = Path.GetFullPath(bestMatch);
                        System.Diagnostics.Debug.WriteLine($"�ݹ��ҵ���ִ���ļ�: {normalizedPath}");
                        return normalizedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"������Ϸ��ִ���ļ�ʱ����: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// ���� Steam ��Ϸ
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
                System.Diagnostics.Debug.WriteLine($"���� Steam ��Ϸʱ����: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ͬ�� Steam ��Ϸʱ�������ʱ��
        /// </summary>
        public static async Task<Dictionary<string, (ulong playtime, DateTime? lastActivity)>> SyncGamePlaytimeAsync(ulong steamId, string? apiKey = null)
        {
            var result = new Dictionary<string, (ulong playtime, DateTime? lastActivity)>();

            try
            {
                System.Diagnostics.Debug.WriteLine($"��ʼͬ�� Steam �û� {steamId} ����Ϸʱ��");

                // ��ȡ�������ʱ��
                var localLastActivity = GetGamesLastActivity(steamId);
                System.Diagnostics.Debug.WriteLine($"�ӱ������û�ȡ�� {localLastActivity.Count} ����Ϸ�����ʱ��");

                // �����API Key�����Դ�Web API��ȡ����ʱ��
                if (!string.IsNullOrEmpty(apiKey))
                {
                    var webApiGames = await GetOwnedGamesFromWebApiAsync(steamId, apiKey);
                    if (webApiGames != null)
                    {
                        foreach (var game in webApiGames)
                        {
                            var appId = game.appid.ToString();
                            var playtime = (ulong)game.playtime_forever; // Web API���ص��Ƿ���
                            
                            localLastActivity.TryGetValue(appId, out var lastActivity);
                            
                            result[appId] = (playtime, lastActivity);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"�� Steam Web API ͬ���� {webApiGames.Count} ����Ϸ��ʱ����Ϣ");
                    }
                }
                else
                {
                    // û��API Keyʱ��ֻ�ܻ�ȡ���ص����ʱ�䣬ʱ����Ϊ0
                    foreach (var kvp in localLastActivity)
                    {
                        result[kvp.Key] = (0, kvp.Value);
                    }
                    
                    System.Diagnostics.Debug.WriteLine("û�� Steam API Key��ֻͬ�������ʱ��");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ͬ�� Steam ��Ϸʱ��ʱ����: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// ������Ϸ�б��ʱ����Ϣ
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
                        
                        System.Diagnostics.Debug.WriteLine($"������Ϸ {game.Name}: ʱ�� {data.playtime} ����, ������� {data.lastActivity?.ToString("yyyy-MM-dd HH:mm:ss") ?? "δ֪"}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"������Ϸʱ����Ϣʱ����: {ex.Message}");
            }
        }

        /// <summary>
        /// ��ʽ������ʱ��Ϊ�ɶ��ַ���
        /// </summary>
        public static string FormatPlaytime(ulong playtimeMinutes)
        {
            if (playtimeMinutes == 0)
                return "δ����";

            var hours = playtimeMinutes / 60;
            var minutes = playtimeMinutes % 60;

            if (hours == 0)
                return $"{minutes} ����";
            
            if (minutes == 0)
                return $"{hours} Сʱ";
                
            return $"{hours} Сʱ {minutes} ����";
        }

        /// <summary>
        /// ������
        /// </summary>
        public static void ClearCache()
        {
            _cachedSteamPath = null;
            _cachedLibraryPaths = null;
        }
    }
}
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
        /// ��� Steam �Ƿ��Ѱ�װ
        /// </summary>
        public static bool IsSteamInstalled()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("��ʼ��� Steam ��װ״̬");

                // ���ע����е� Steam ��װ·��
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
                // ���ȳ��Դ�ע����ȡ
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var steamPath = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        return steamPath.Replace('/', '\\'); // ͳһʹ�� Windows ·���ָ���
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
                        return path;
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
            var libraryPaths = new List<string>();

            try
            {
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

                // ��ȡ libraryfolders.vdf �ļ���ȡ������·��
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
                                // ��׼��·��������ظ�
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ȡ Steam ��·��ʱ����: {ex.Message}");
            }

            return libraryPaths;
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
                    // StateFlags Ϊ 4 ��ʾ����ȫ��װ
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
        /// ������Ϸ��ִ���ļ�
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
                    
                    // �ų�һЩ����صĿ�ִ���ļ�
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

                // ����ڸ�Ŀ¼�Ҳ�������������Ŀ¼�в���
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
                        System.Diagnostics.Debug.WriteLine($"�ݹ���ҵ���ִ���ļ�: {normalizedPath}");
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
    }
}
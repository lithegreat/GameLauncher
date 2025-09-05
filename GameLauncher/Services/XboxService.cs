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
        public ulong Playtime { get; set; } // ��Ϸʱ�����룩
    }

    public static class XboxService
    {
        private const string XboxPassAppFamilyName = "Microsoft.GamingApp_8wekyb3d8bbwe";
        private static bool? _isXboxPassAppInstalled;

        /// <summary>
        /// ��� Xbox PC Pass Ӧ���Ƿ��Ѱ�װ
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
                    
                    System.Diagnostics.Debug.WriteLine($"Xbox Pass App ��װ״̬: {_isXboxPassAppInstalled}");
                    return _isXboxPassAppInstalled.Value;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"��� Xbox Pass App ʱ����: {ex.Message}");
                    _isXboxPassAppInstalled = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// �� Xbox PC Pass Ӧ��
        /// </summary>
        public static bool OpenXboxPassApp()
        {
            try
            {
                var uwpApps = Programs.GetUWPApps();
                var xboxApp = uwpApps.FirstOrDefault(app => app.AppId == XboxPassAppFamilyName);
                
                if (xboxApp == null)
                {
                    System.Diagnostics.Debug.WriteLine("Xbox PC Pass Ӧ��δ��װ");
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
                    // ʹ��Э������
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
                System.Diagnostics.Debug.WriteLine($"�� Xbox Pass App ʱ����: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ɨ�� Xbox ��Ϸ - ���� UWP ��Ŀ¼ɨ��
        /// </summary>
        public static async Task<List<XboxGame>> ScanXboxGamesAsync()
        {
            var games = new List<XboxGame>();
            var foundIdentifiers = new HashSet<string>();

            System.Diagnostics.Debug.WriteLine("=== ��ʼȫ��ɨ�� Xbox ��Ϸ ===");

            // ����1: ɨ���Ѱ�װ�� UWP Xbox ��Ϸ
            await ScanInstalledXboxUwpGamesAsync(games, foundIdentifiers);

            // ����2: ɨ�������������� XboxGames Ŀ¼
            await ScanXboxGamesDirectoriesAsync(games, foundIdentifiers);

            // ����3: ͨ��ע������Xbox��Ϸ��װ·��
            await ScanXboxRegistryLocationsAsync(games, foundIdentifiers);

            System.Diagnostics.Debug.WriteLine($"=== Xbox ��Ϸɨ����ɣ��ܹ��ҵ� {games.Count} ����Ϸ ===");
            
            // ���������򲢷���
            return games.OrderBy(g => g.Name).ToList();
        }

        /// <summary>
        /// ɨ���Ѱ�װ�� UWP Xbox ��Ϸ
        /// </summary>
        private static async Task ScanInstalledXboxUwpGamesAsync(List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("--- ��ʼɨ�� UWP Xbox ��Ϸ ---");

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
                            System.Diagnostics.Debug.WriteLine($"ͨ�� UWP �ҵ�Xbox��Ϸ: {game.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"���� UWP Xbox ��Ϸʱ����: {uwpGame.Name} - {ex.Message}");
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɨ�� UWP Xbox ��Ϸʱ����: {ex.Message}");
            }
        }

        /// <summary>
        /// �ж� UWP Ӧ���Ƿ�Ϊ Xbox �����Ϸ
        /// </summary>
        private static bool IsXboxRelatedApp(UwpApp app)
        {
            if (!app.IsGame)
                return false;

            // ����Ƿ�װ�� Xbox ���Ŀ¼
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

            // ���Ӧ��ID�Ƿ������֪��Xbox��Ϸ������
            var xboxPublishers = new[]
            {
                "microsoft", "xbox", "8wekyb3d8bbwe" // Microsoft�ķ�����ID
            };

            return xboxPublishers.Any(pub => app.AppId.ToLowerInvariant().Contains(pub));
        }

        /// <summary>
        /// �� UWP Ӧ�ô��� Xbox ��Ϸ����
        /// </summary>
        private static XboxGame CreateGameFromUwpApp(UwpApp uwpApp)
        {
            try
            {
                // ���ԴӰ�װĿ¼�ҵ���ѵĿ�ִ���ļ�
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
                System.Diagnostics.Debug.WriteLine($"�� UWP Ӧ�ô�����Ϸ����ʱ����: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ������Ϸ���ƣ��Ƴ�������׺
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
        /// ɨ�������������� XboxGames Ŀ¼
        /// </summary>
        private static async Task ScanXboxGamesDirectoriesAsync(List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("--- ��ʼɨ�� XboxGames Ŀ¼ ---");

                var xboxGamesPaths = new List<string>();

                // ��ȡ���й̶�������
                var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady);

                foreach (var drive in drives)
                {
                    var possibleXboxPaths = new[]
                    {
                        // ��Ҫ��XboxGamesĿ¼λ��
                        Path.Combine(drive.RootDirectory.FullName, "XboxGames"),
                        
                        // ���ܵ�����Xbox��ϷĿ¼
                        Path.Combine(drive.RootDirectory.FullName, "Program Files", "XboxGames"),
                        Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "XboxGames")
                    };

                    foreach (var path in possibleXboxPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            xboxGamesPaths.Add(path);
                            System.Diagnostics.Debug.WriteLine($"�ҵ� Xbox ��ϷĿ¼: {path}");
                        }
                    }
                }

                // ����ɨ�������ҵ���Xbox��ϷĿ¼
                await Task.WhenAll(xboxGamesPaths.Select(async path => 
                    await ScanXboxGamesInDirectoryAsync(path, games, foundIdentifiers)));

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɨ�� XboxGames Ŀ¼ʱ����: {ex.Message}");
            }
        }

        /// <summary>
        /// ɨ��Xboxע���λ��
        /// </summary>
        private static async Task ScanXboxRegistryLocationsAsync(List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("--- ��ʼɨ�� Xbox ע���λ�� ---");

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

                // ��ȡ��ע����е�Xbox��Ϸ��װĿ¼
                var xboxInstallDirs = GetXboxInstallDirectoriesFromRegistry();
                foreach (var dir in xboxInstallDirs)
                {
                    await ScanXboxGamesInDirectoryAsync(dir, games, foundIdentifiers);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɨ��Xboxע���ʱ����: {ex.Message}");
            }
        }

        /// <summary>
        /// ר��ɨ��XboxGamesĿ¼�е���Ϸ
        /// </summary>
        private static async Task ScanXboxGamesInDirectoryAsync(string xboxGamesDirectory, List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                if (!Directory.Exists(xboxGamesDirectory))
                    return;

                System.Diagnostics.Debug.WriteLine($"ɨ��Xbox��ϷĿ¼: {xboxGamesDirectory}");

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
                                        System.Diagnostics.Debug.WriteLine($"�� {Path.GetFileName(xboxGamesDirectory)} ���ҵ�Xbox��Ϸ: {game.Name}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"����Xbox��ϷĿ¼ʱ����: {gameDir} - {ex.Message}");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɨ��Xbox��ϷĿ¼ʱ����: {xboxGamesDirectory} - {ex.Message}");
            }
        }

        /// <summary>
        /// ��XboxGamesĿ¼������Ϸ����
        /// </summary>
        private static XboxGame? CreateXboxGameFromDirectory(string gameDirectory)
        {
            try
            {
                if (!Directory.Exists(gameDirectory))
                    return null;

                var directoryName = Path.GetFileName(gameDirectory);
                
                // ���ȳ��Դ�AppxManifest.xml��ȡ��Ϣ
                var manifestPath = Path.Combine(gameDirectory, "AppxManifest.xml");
                if (File.Exists(manifestPath))
                {
                    var gameFromManifest = CreateGameFromXboxManifest(manifestPath, gameDirectory);
                    if (gameFromManifest != null)
                        return gameFromManifest;
                }

                // ����Ƿ�����Ϸ��ִ���ļ�
                var executablePath = FindXboxGameExecutable(gameDirectory, directoryName);
                if (string.IsNullOrEmpty(executablePath))
                    return null;

                // ���Դ�Ŀ¼�ṹ�ƶ���Ϸ��Ϣ
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
                System.Diagnostics.Debug.WriteLine($"��XboxĿ¼������Ϸ����ʱ����: {gameDirectory} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ��Xbox�嵥�ļ�������Ϸ����
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

                // ��ȡ��ʾ����
                var displayName = properties?.Element(ns + "DisplayName")?.Value;
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = ExtractGameNameFromDirectory(gameDirectory, packageName);
                }

                // ���ҿ�ִ���ļ�
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
                System.Diagnostics.Debug.WriteLine($"��Xbox�嵥������Ϸ����ʱ����: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ר�Ų���Xbox��Ϸ�Ŀ�ִ���ļ�
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

                    // �ų����Եķ���Ϸ�ļ�
                    var excludePatterns = new[]
                    {
                        "unins", "setup", "install", "config", "crash", "report", 
                        "update", "patch", "tool", "util", "helper", "service",
                        "redist", "vcredist", "directx", "dotnet"
                    };

                    if (excludePatterns.Any(pattern => fileName.Contains(pattern)))
                        continue;

                    // Xbox��Ϸ�ض������ȼ�����
                    var gameNameLower = gameName.ToLowerInvariant();
                    
                    if (fileName.Equals(gameNameLower))
                        priority += 100;
                    if (fileName.Contains(gameNameLower))
                        priority += 50;
                        
                    // Xbox��Ϸ�����Ŀ�ִ���ļ�ģʽ
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

                    // �ļ���С���ǣ�Xbox��Ϸͨ���ϴ�
                    if (fileInfo.Length > 100 * 1024 * 1024) // > 100MB
                        priority += 25;
                    else if (fileInfo.Length > 50 * 1024 * 1024) // > 50MB
                        priority += 20;
                    else if (fileInfo.Length > 10 * 1024 * 1024) // > 10MB
                        priority += 10;
                    else if (fileInfo.Length > 1024 * 1024) // > 1MB
                        priority += 5;

                    // ƫ���ڸ�Ŀ¼��binĿ¼�Ŀ�ִ���ļ�
                    var relativePath = Path.GetRelativePath(gamePath, exeFile).ToLowerInvariant();
                    if (!relativePath.Contains(Path.DirectorySeparatorChar)) // ��Ŀ¼
                        priority += 15;
                    else if (relativePath.Contains("bin"))
                        priority += 10;

                    candidates.Add((exeFile, priority, fileInfo.Length));
                }

                if (candidates.Count == 0)
                    return string.Empty;

                // ѡ�����ȼ���ߵĿ�ִ���ļ�
                var bestCandidate = candidates
                    .OrderByDescending(c => c.priority)
                    .ThenByDescending(c => c.size)
                    .First();

                System.Diagnostics.Debug.WriteLine($"Ϊ {gameName} ѡ���ִ���ļ�: {Path.GetFileName(bestCandidate.path)} (���ȼ�: {bestCandidate.priority})");
                return bestCandidate.path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"����Xbox��Ϸ��ִ���ļ�ʱ����: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// ��Ŀ¼����ȡ��Ϸ����
        /// </summary>
        private static string ExtractGameNameFromDirectory(string gameDirectory, string directoryName)
        {
            try
            {
                // ���ԴӶ����Դ��ȡ���Ѻõ���Ϸ����
                
                // 1. ����Ƿ�����ʾ�����ļ�
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

                // 2. ���ԴӰ�Ŀ¼��������ȡ��ͨ����ʽΪ GameName_�汾��_�ܹ���
                var parts = directoryName.Split('_');
                if (parts.Length > 0)
                {
                    var gameName = parts[0];
                    // �������ĺ�׺
                    gameName = gameName.Replace("Game", "").Replace("App", "").Trim();
                    if (!string.IsNullOrEmpty(gameName))
                        return gameName;
                }

                // 3. ֱ��ʹ��Ŀ¼��
                return directoryName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ȡ��Ϸ����ʱ����: {ex.Message}");
                return directoryName;
            }
        }

        /// <summary>
        /// ɨ��Xboxע���·��
        /// </summary>
        private static async Task ScanXboxRegistryPathAsync(string registryPath, List<XboxGame> games, HashSet<string> foundIdentifiers)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(registryPath);
                if (key == null) 
                {
                    // Ҳ����LocalMachine
                    using var lmKey = Registry.LocalMachine.OpenSubKey(registryPath);
                    if (lmKey == null) return;
                    
                    await ProcessRegistryKey(lmKey, games, foundIdentifiers);
                    return;
                }

                await ProcessRegistryKey(key, games, foundIdentifiers);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɨ��Xboxע���·��ʱ����: {registryPath} - {ex.Message}");
            }
        }

        /// <summary>
        /// ����ע����
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
                    System.Diagnostics.Debug.WriteLine($"����ע����Ӽ�ʱ����: {subKeyName} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ��ȡXbox��Ϸ��װĿ¼
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
                        // Xbox��ϷĬ�ϰ�װλ��
                        var defaultLocation = key.GetValue("DefaultInstallLocation") as string ??
                                             key.GetValue("XboxGamesPath") as string;
                        if (!string.IsNullOrEmpty(defaultLocation) && Directory.Exists(defaultLocation))
                        {
                            directories.Add(defaultLocation);
                        }

                        // ɨ����ص����Ϸ��
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
                System.Diagnostics.Debug.WriteLine($"��ע����ȡXboxĿ¼ʱ����: {ex.Message}");
            }

            return directories.Distinct().ToList();
        }

        /// <summary>
        /// ��ȡ��Ϸ����������
        /// </summary>
        public static string GetGameLaunchArguments(XboxGame game)
        {
            if (string.IsNullOrEmpty(game.PackageFamilyName))
                return string.Empty;

            return $"shell:appsFolder\\{game.PackageFamilyName}!App";
        }

        /// <summary>
        /// �����Ϸ�Ƿ���������
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
                System.Diagnostics.Debug.WriteLine($"�����Ϸ����״̬ʱ����: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ���� Xbox ��Ϸ
        /// </summary>
        public static bool LaunchXboxGame(string packageFamilyName)
        {
            try
            {
                // ʹ�� Windows Ӧ��Э��������Ϸ
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
                System.Diagnostics.Debug.WriteLine($"���� Xbox ��Ϸʱ����: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ͨ����ִ���ļ�������Ϸ����ѡ������
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
                System.Diagnostics.Debug.WriteLine($"ͨ����ִ���ļ����� Xbox ��Ϸʱ����: {ex.Message}");
                return false;
            }
        }
    }
}
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Threading;
using System.Text.Json.Serialization;

namespace GameLauncher.Services
{
    public static class UpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _githubApiUrl = "https://api.github.com/repos/lithegreat/GameLauncher/releases/latest";
        private static Timer? _updateTimer;
        
        // 添加更强的状态跟踪变量
        private static bool _isCheckingUpdate = false;
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static string? _lastSkippedVersion = null;
        private static UpdateCheckResult? _lastCheckResult = null;
        private static readonly TimeSpan _minimumCheckInterval = TimeSpan.FromMinutes(30); // 最小检查间隔30分钟
        private static bool _hasShownUpdateDialogThisSession = false; // 本次会话是否已显示过更新对话框
        private static string? _lastShownVersion = null; // 上次显示对话框的版本

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GameLauncher-UpdateService");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceCheck = false)
        {
            try
            {
                // 防止重复检查
                if (!forceCheck && _isCheckingUpdate)
                {
                    Debug.WriteLine("UpdateService: Update check already in progress");
                    return _lastCheckResult ?? new UpdateCheckResult { Success = false, Error = "检查进行中" };
                }

                // 检查是否需要跳过（距离上次检查时间太短）
                if (!forceCheck && DateTime.Now - _lastCheckTime < _minimumCheckInterval)
                {
                    Debug.WriteLine($"UpdateService: Skipping check, last check was {DateTime.Now - _lastCheckTime} ago");
                    return _lastCheckResult ?? new UpdateCheckResult { Success = true, HasUpdate = false };
                }

                _isCheckingUpdate = true;
                Debug.WriteLine("UpdateService: Starting update check");
                
                var response = await _httpClient.GetAsync(_githubApiUrl);
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                };
                var releaseInfo = JsonSerializer.Deserialize<GitHubRelease>(jsonContent, options);
                
                if (releaseInfo == null)
                {
                    var errorResult = new UpdateCheckResult { Success = false, Error = "无法解析发布信息" };
                    _lastCheckResult = errorResult;
                    return errorResult;
                }

                Debug.WriteLine($"UpdateService: Found release {releaseInfo.TagName}");

                // 获取当前版本
                var currentVersion = GetCurrentVersion();
                var latestVersion = ParseVersion(releaseInfo.TagName);
                
                Debug.WriteLine($"UpdateService: Current: {currentVersion}, Latest: {latestVersion}");

                bool hasUpdate = latestVersion > currentVersion;
                
                // 检查是否是用户跳过的版本
                if (hasUpdate && _lastSkippedVersion == releaseInfo.TagName)
                {
                    Debug.WriteLine($"UpdateService: Version {releaseInfo.TagName} was skipped by user");
                    hasUpdate = false;
                }
                
                // 查找 MSIX 安装包
                var msixAsset = releaseInfo.Assets?.FirstOrDefault(a => 
                    a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));

                if (hasUpdate && msixAsset == null)
                {
                    var errorResult = new UpdateCheckResult 
                    { 
                        Success = false, 
                        Error = "找不到 MSIX 安装包" 
                    };
                    _lastCheckResult = errorResult;
                    return errorResult;
                }

                var result = new UpdateCheckResult
                {
                    Success = true,
                    HasUpdate = hasUpdate,
                    LatestVersion = releaseInfo.TagName,
                    CurrentVersion = currentVersion.ToString(),
                    DownloadUrl = msixAsset?.DownloadUrl,
                    ReleaseNotes = releaseInfo.Body
                };

                _lastCheckResult = result;
                _lastCheckTime = DateTime.Now;
                return result;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"UpdateService: Network error: {ex.Message}");
                var errorResult = new UpdateCheckResult { Success = false, Error = $"网络错误: {ex.Message}" };
                _lastCheckResult = errorResult;
                return errorResult;
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("UpdateService: Request timeout");
                var errorResult = new UpdateCheckResult { Success = false, Error = "请求超时，请检查网络连接" };
                _lastCheckResult = errorResult;
                return errorResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Unexpected error: {ex.Message}");
                var errorResult = new UpdateCheckResult { Success = false, Error = $"检查更新失败: {ex.Message}" };
                _lastCheckResult = errorResult;
                return errorResult;
            }
            finally
            {
                _isCheckingUpdate = false;
            }
        }

        public static async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
        {
            try
            {
                Debug.WriteLine($"UpdateService: Starting download from {downloadUrl}");
                
                // 创建临时文件
                var tempPath = Path.Combine(Path.GetTempPath(), "GameLauncher_Update.msix");
                
                // 下载文件
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;
                
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                var buffer = new byte[8192];
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    
                    if (totalBytes > 0)
                    {
                        var progressPercentage = (int)((downloadedBytes * 100) / totalBytes);
                        progress?.Report(progressPercentage);
                    }
                }
                
                Debug.WriteLine("UpdateService: Download completed, starting installation");
                
                // 安装更新
                var packageManager = new PackageManager();
                var deploymentResult = await packageManager.AddPackageAsync(
                    new Uri(tempPath),
                    null,
                    DeploymentOptions.ForceApplicationShutdown);
                
                if (deploymentResult.IsRegistered)
                {
                    Debug.WriteLine("UpdateService: Update installed successfully");
                    
                    // 清理临时文件
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UpdateService: Failed to delete temp file: {ex.Message}");
                    }
                    
                    return true;
                }
                else
                {
                    Debug.WriteLine($"UpdateService: Installation failed: {deploymentResult.ErrorText}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Download/Install error: {ex.Message}");
                return false;
            }
        }

        public static void StartAutoUpdateCheck()
        {
            var settings = UpdateSettings.GetSettings();
            if (!settings.AutoUpdateEnabled)
            {
                Debug.WriteLine("UpdateService: Auto update is disabled");
                StopAutoUpdateCheck(); // 确保停止定时器
                return;
            }

            // 如果已经有定时器在运行，不重复启动
            if (_updateTimer != null)
            {
                Debug.WriteLine("UpdateService: Auto update check already running, skipping restart");
                return;
            }

            var interval = settings.UpdateFrequency switch
            {
                UpdateFrequency.OnStartup => TimeSpan.Zero, // 立即执行一次
                UpdateFrequency.Weekly => TimeSpan.FromDays(7),
                _ => TimeSpan.Zero
            };

            if (interval > TimeSpan.Zero)
            {
                Debug.WriteLine($"UpdateService: Starting auto update check with interval: {interval}");
                _updateTimer = new Timer(async _ => await PerformAutoUpdateCheck(false), null, TimeSpan.Zero, interval);
            }
            else if (settings.UpdateFrequency == UpdateFrequency.OnStartup)
            {
                Debug.WriteLine("UpdateService: Performing startup update check");
                _ = Task.Run(() => PerformAutoUpdateCheck(false));
            }
        }

        public static void StopAutoUpdateCheck()
        {
            _updateTimer?.Dispose();
            _updateTimer = null;
            Debug.WriteLine("UpdateService: Auto update check stopped");
        }

        // 添加手动检查更新的方法（强制检查，会弹窗）
        public static async Task<UpdateCheckResult> CheckForUpdatesManuallyAsync()
        {
            return await CheckForUpdatesAsync(forceCheck: true);
        }

        // 添加跳过版本的方法
        public static void SkipVersion(string version)
        {
            _lastSkippedVersion = version;
            _hasShownUpdateDialogThisSession = true;
            _lastShownVersion = version;
            Debug.WriteLine($"UpdateService: Version {version} marked as skipped");
        }

        // 重置会话状态（用于测试或特殊情况）
        public static void ResetSessionState()
        {
            _hasShownUpdateDialogThisSession = false;
            _lastShownVersion = null;
            _lastSkippedVersion = null;
            Debug.WriteLine("UpdateService: Session state reset");
        }

        private static async Task PerformAutoUpdateCheck(bool showErrorDialog = true)
        {
            try
            {
                Debug.WriteLine("UpdateService: Performing automatic update check");
                
                var result = await CheckForUpdatesAsync(forceCheck: false);
                
                if (!result.Success)
                {
                    Debug.WriteLine($"UpdateService: Auto update check failed: {result.Error}");
                    // 只在指定时显示错误对话框
                    if (showErrorDialog && App.Current?.MainWindow != null)
                    {
                        App.Current.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            await ShowUpdateErrorDialog(result.Error);
                        });
                    }
                    return;
                }

                if (result.HasUpdate)
                {
                    Debug.WriteLine($"UpdateService: Update available: {result.LatestVersion}");
                    
                    // 检查是否已经在本次会话中显示过此版本的对话框
                    if (_hasShownUpdateDialogThisSession && _lastShownVersion == result.LatestVersion)
                    {
                        Debug.WriteLine($"UpdateService: Update dialog for version {result.LatestVersion} already shown this session");
                        return;
                    }
                    
                    // 在UI线程上显示更新提示
                    if (App.Current?.MainWindow != null)
                    {
                        App.Current.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            await ShowUpdateAvailableDialog(result);
                        });
                    }
                }
                else
                {
                    Debug.WriteLine("UpdateService: No updates available");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Auto update check exception: {ex.Message}");
            }
        }

        private static async Task ShowUpdateErrorDialog(string error)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "更新检查失败",
                    Content = $"无法检查更新：{error}",
                    CloseButtonText = "确定",
                    XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Error showing update error dialog: {ex.Message}");
            }
        }

        private static async Task ShowUpdateAvailableDialog(UpdateCheckResult result)
        {
            try
            {
                // 标记已显示对话框
                _hasShownUpdateDialogThisSession = true;
                _lastShownVersion = result.LatestVersion;
                
                var dialog = new ContentDialog
                {
                    Title = "发现新版本",
                    Content = $"发现新版本 {result.LatestVersion}，当前版本 {result.CurrentVersion}。\n\n是否立即下载并安装更新？",
                    PrimaryButtonText = "立即更新",
                    SecondaryButtonText = "稍后提醒",
                    CloseButtonText = "跳过此版本",
                    XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                };

                var dialogResult = await dialog.ShowAsync();
                
                if (dialogResult == ContentDialogResult.Primary && !string.IsNullOrEmpty(result.DownloadUrl))
                {
                    await DownloadAndInstallWithProgress(result.DownloadUrl);
                }
                else if (dialogResult == ContentDialogResult.None) // 用户点击了"跳过此版本"
                {
                    SkipVersion(result.LatestVersion ?? "");
                }
                else if (dialogResult == ContentDialogResult.Secondary) // 用户点击了"稍后提醒"
                {
                    // 稍后提醒：重置会话状态，但不跳过版本
                    _hasShownUpdateDialogThisSession = false;
                    _lastShownVersion = null;
                    Debug.WriteLine("UpdateService: User chose to be reminded later");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Error showing update available dialog: {ex.Message}");
            }
        }

        private static async Task DownloadAndInstallWithProgress(string downloadUrl)
        {
            try
            {
                var progressDialog = new ContentDialog
                {
                    Title = "正在更新",
                    Content = "正在下载更新...",
                    XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                };

                var progressBar = new ProgressBar
                {
                    IsIndeterminate = false,
                    Value = 0,
                    Minimum = 0,
                    Maximum = 100
                };

                var stackPanel = new StackPanel();
                stackPanel.Children.Add(new TextBlock { Text = "正在下载更新..." });
                stackPanel.Children.Add(progressBar);

                progressDialog.Content = stackPanel;

                var showTask = progressDialog.ShowAsync();

                var progress = new Progress<int>(value =>
                {
                    App.Current?.MainWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        progressBar.Value = value;
                    });
                });

                var success = await DownloadAndInstallUpdateAsync(downloadUrl, progress);

                progressDialog.Hide();

                if (success)
                {
                    var successDialog = new ContentDialog
                    {
                        Title = "更新完成",
                        Content = "更新已成功安装，应用程序将重新启动。",
                        CloseButtonText = "确定",
                        XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                    };

                    await successDialog.ShowAsync();
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "更新失败",
                        Content = "更新安装失败，请稍后重试或手动下载安装。",
                        CloseButtonText = "确定",
                        XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                    };

                    await errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Error in download and install with progress: {ex.Message}");
            }
        }

        private static Version GetCurrentVersion()
        {
            try
            {
                var package = Package.Current;
                var version = package.Id.Version;
                return new Version(version.Major, version.Minor, version.Build, version.Revision);
            }
            catch
            {
                return new Version(1, 0, 0, 0);
            }
        }

        private static Version ParseVersion(string versionString)
        {
            try
            {
                // 移除版本字符串中的 'v' 前缀
                var cleanVersion = versionString.TrimStart('v', 'V');
                return Version.Parse(cleanVersion);
            }
            catch
            {
                return new Version(0, 0, 0, 0);
            }
        }
    }

    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool HasUpdate { get; set; }
        public string? LatestVersion { get; set; }
        public string? CurrentVersion { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ReleaseNotes { get; set; }
        public string? Error { get; set; }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
        
        [JsonPropertyName("draft")]
        public bool Draft { get; set; }
        
        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
        
        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
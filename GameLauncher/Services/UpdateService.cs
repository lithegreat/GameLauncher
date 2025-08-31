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
        private static readonly string _githubAllReleasesApiUrl = "https://api.github.com/repos/lithegreat/GameLauncher/releases";
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

                // 获取用户设置
                var settings = UpdateSettings.GetSettings();
                GitHubRelease? releaseInfo;

                if (settings.IncludePrerelease)
                {
                    // 如果包含预发布版本，获取所有releases并找到最新的
                    releaseInfo = await GetLatestReleaseIncludingPrerelease();
                }
                else
                {
                    // 只获取正式版本
                    releaseInfo = await GetLatestStableRelease();
                }
                
                if (releaseInfo == null)
                {
                    var errorResult = new UpdateCheckResult { Success = false, Error = "无法解析发布信息" };
                    _lastCheckResult = errorResult;
                    return errorResult;
                }

                Debug.WriteLine($"UpdateService: Found release {releaseInfo.TagName} (Prerelease: {releaseInfo.Prerelease})");

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
                    ReleaseNotes = releaseInfo.Body,
                    IsPrerelease = releaseInfo.Prerelease
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

        private static async Task<GitHubRelease?> GetLatestStableRelease()
        {
            try
            {
                var response = await _httpClient.GetAsync(_githubApiUrl);
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                };
                
                return JsonSerializer.Deserialize<GitHubRelease>(jsonContent, options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Error getting stable release: {ex.Message}");
                return null;
            }
        }

        private static async Task<GitHubRelease?> GetLatestReleaseIncludingPrerelease()
        {
            try
            {
                var response = await _httpClient.GetAsync(_githubAllReleasesApiUrl);
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                };
                
                var releases = JsonSerializer.Deserialize<GitHubRelease[]>(jsonContent, options);
                
                if (releases == null || releases.Length == 0)
                {
                    return null;
                }

                // 过滤掉草稿版本，按版本号排序，返回最新的
                var validReleases = releases
                    .Where(r => !r.Draft)
                    .OrderByDescending(r => ParseVersion(r.TagName))
                    .ToArray();

                return validReleases.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Error getting releases including prerelease: {ex.Message}");
                return null;
            }
        }

        public static async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
        {
            string? tempPath = null;
            try
            {
                Debug.WriteLine($"UpdateService: Starting download from {downloadUrl}");
                
                // 创建临时文件，使用更安全的文件名
                var tempFileName = $"GameLauncher_Update_{DateTime.Now:yyyyMMdd_HHmmss}.msix";
                tempPath = Path.Combine(Path.GetTempPath(), tempFileName);
                
                Debug.WriteLine($"UpdateService: Downloading to temporary path: {tempPath}");
                
                // 确保临时目录存在且可写
                var tempDir = Path.GetDirectoryName(tempPath);
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir!);
                }
                
                // 下载文件
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;
                
                Debug.WriteLine($"UpdateService: Content length: {totalBytes} bytes");
                
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
                
                Debug.WriteLine($"UpdateService: Download completed. Downloaded {downloadedBytes} bytes");
                
                // 验证下载的文件
                if (!File.Exists(tempPath))
                {
                    Debug.WriteLine("UpdateService: Downloaded file does not exist");
                    return false;
                }
                
                var fileInfo = new FileInfo(tempPath);
                if (fileInfo.Length == 0)
                {
                    Debug.WriteLine("UpdateService: Downloaded file is empty");
                    return false;
                }
                
                if (totalBytes > 0 && fileInfo.Length != totalBytes)
                {
                    Debug.WriteLine($"UpdateService: File size mismatch. Expected: {totalBytes}, Actual: {fileInfo.Length}");
                    return false;
                }
                
                Debug.WriteLine($"UpdateService: File validation passed. File size: {fileInfo.Length} bytes");
                Debug.WriteLine("UpdateService: Starting MSIX installation...");
                
                // 安装更新
                var packageManager = new PackageManager();
                
                // 使用 file:// URI 格式
                var packageUri = new Uri($"file:///{tempPath.Replace('\\', '/')}");
                Debug.WriteLine($"UpdateService: Installing package from URI: {packageUri}");
                
                var deploymentResult = await packageManager.AddPackageAsync(
                    packageUri,
                    null,
                    DeploymentOptions.ForceApplicationShutdown);
                
                Debug.WriteLine($"UpdateService: Deployment operation completed");
                Debug.WriteLine($"UpdateService: IsRegistered: {deploymentResult.IsRegistered}");
                Debug.WriteLine($"UpdateService: ExtendedErrorCode: {deploymentResult.ExtendedErrorCode}");
                Debug.WriteLine($"UpdateService: ErrorText: {deploymentResult.ErrorText}");
                
                if (deploymentResult.IsRegistered)
                {
                    Debug.WriteLine("UpdateService: Update installed successfully");
                    return true;
                }
                else
                {
                    // 记录详细的错误信息
                    var errorCode = deploymentResult.ExtendedErrorCode;
                    var errorText = deploymentResult.ErrorText;
                    
                    Debug.WriteLine($"UpdateService: Installation failed with extended error code: 0x{errorCode:X8}");
                    Debug.WriteLine($"UpdateService: Error text: {errorText}");
                    
                    // 根据常见错误代码提供更友好的错误信息
                    var friendlyError = GetFriendlyErrorMessage((int)errorCode.HResult);
                    if (!string.IsNullOrEmpty(friendlyError))
                    {
                        Debug.WriteLine($"UpdateService: Friendly error message: {friendlyError}");
                    }
                    
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"UpdateService: Access denied during installation: {ex.Message}");
                Debug.WriteLine("UpdateService: This might be due to insufficient permissions or the app being in use");
                return false;
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine($"UpdateService: File not found during installation: {ex.Message}");
                return false;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"UpdateService: Network error during download: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"UpdateService: IO error during download/install: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Unexpected error during download/install: {ex.Message}");
                Debug.WriteLine($"UpdateService: Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"UpdateService: Stack trace: {ex.StackTrace}");
                return false;
            }
            finally
            {
                // 清理临时文件
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                        Debug.WriteLine($"UpdateService: Temporary file deleted: {tempPath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UpdateService: Failed to delete temp file {tempPath}: {ex.Message}");
                    }
                }
            }
        }

        private static string GetFriendlyErrorMessage(int errorCode)
        {
            return errorCode switch
            {
                unchecked((int)0x80073CF3) => "应用包格式无效或损坏",
                unchecked((int)0x80073CF6) => "应用包的签名无效",
                unchecked((int)0x80073CF9) => "应用包依赖项缺失",
                unchecked((int)0x80073CFA) => "应用包不被信任",
                unchecked((int)0x80073CFB) => "存储空间不足",
                unchecked((int)0x80073D01) => "应用包已安装，但版本不匹配",
                unchecked((int)0x80073D02) => "应用程序正在运行，无法更新",
                unchecked((int)0x80070005) => "访问被拒绝，可能需要管理员权限",
                unchecked((int)0x80070002) => "找不到指定的文件",
                unchecked((int)0x80070057) => "参数无效",
                _ => ""
            };
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

                // 构建详细的版本信息UI
                var stackPanel = new StackPanel { Spacing = 16, MaxWidth = 500 };

                // 版本信息区域
                var versionInfoPanel = new StackPanel { Spacing = 8 };
                
                // 标题
                var titleText = new TextBlock
                {
                    Text = result.IsPrerelease ? "发现新的预发布版本" : "发现新版本",
                    Style = (Style)App.Current.Resources["SubtitleTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                versionInfoPanel.Children.Add(titleText);

                // 版本对比信息
                var versionComparePanel = new Grid();
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 当前版本
                var currentVersionPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                currentVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = "当前版本", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                });
                currentVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = result.CurrentVersion ?? "未知", 
                    Style = (Style)App.Current.Resources["BodyStrongTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                Grid.SetColumn(currentVersionPanel, 0);
                versionComparePanel.Children.Add(currentVersionPanel);

                // 箭头 - 使用更通用的方法显示箭头
                var arrowIcon = new SymbolIcon(Symbol.Forward)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.6
                };
                Grid.SetColumn(arrowIcon, 1);
                versionComparePanel.Children.Add(arrowIcon);

                // 最新版本
                var latestVersionPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                latestVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = result.IsPrerelease ? "最新预发布版本" : "最新版本", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                });
                
                var latestVersionText = new TextBlock 
                { 
                    Text = result.LatestVersion ?? "未知", 
                    Style = (Style)App.Current.Resources["BodyStrongTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                
                if (result.IsPrerelease)
                {
                    latestVersionText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
                
                latestVersionPanel.Children.Add(latestVersionText);
                Grid.SetColumn(latestVersionPanel, 2);
                versionComparePanel.Children.Add(latestVersionPanel);

                versionInfoPanel.Children.Add(versionComparePanel);

                // 预发布版本警告 - 使用SymbolIcon替代FontIcon
                if (result.IsPrerelease)
                {
                    var warningPanel = new StackPanel 
                    { 
                        Orientation = Orientation.Horizontal, 
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 8
                    };
                    
                    var warningIcon = new SymbolIcon(Symbol.Important)
                    {
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    warningPanel.Children.Add(warningIcon);
                    
                    warningPanel.Children.Add(new TextBlock 
                    { 
                        Text = "这是一个预发布版本，可能包含不稳定的功能", 
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    
                    versionInfoPanel.Children.Add(warningPanel);
                }

                stackPanel.Children.Add(versionInfoPanel);

                // 发布说明
                if (!string.IsNullOrWhiteSpace(result.ReleaseNotes))
                {
                    var releaseNotesPanel = new StackPanel { Spacing = 8 };
                    
                    releaseNotesPanel.Children.Add(new TextBlock 
                    { 
                        Text = "更新内容", 
                        Style = (Style)App.Current.Resources["BodyStrongTextBlockStyle"] 
                    });

                    var scrollViewer = new ScrollViewer 
                    { 
                        MaxHeight = 150,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                    };

                    var releaseNotesText = new TextBlock 
                    { 
                        Text = result.ReleaseNotes.Trim(),
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                        Opacity = 0.8
                    };

                    scrollViewer.Content = releaseNotesText;
                    releaseNotesPanel.Children.Add(scrollViewer);
                    stackPanel.Children.Add(releaseNotesPanel);
                }

                // 操作提示
                var actionText = new TextBlock 
                { 
                    Text = "是否立即下载并安装更新？",
                    Style = (Style)App.Current.Resources["BodyTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                stackPanel.Children.Add(actionText);
                
                var dialog = new ContentDialog
                {
                    Title = result.IsPrerelease ? "发现新的预发布版本" : "发现新版本",
                    Content = stackPanel,
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
            ContentDialog? progressDialog = null;
            try
            {
                progressDialog = new ContentDialog
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

                var statusText = new TextBlock { Text = "正在下载更新..." };
                var stackPanel = new StackPanel();
                stackPanel.Children.Add(statusText);
                stackPanel.Children.Add(progressBar);

                progressDialog.Content = stackPanel;

                var showTask = progressDialog.ShowAsync();

                var progress = new Progress<int>(value =>
                {
                    App.Current?.MainWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        progressBar.Value = value;
                        statusText.Text = $"正在下载更新... {value}%";
                    });
                });

                // 更新状态为"正在安装"
                var installProgress = new Progress<string>(message =>
                {
                    App.Current?.MainWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        statusText.Text = message;
                        progressBar.IsIndeterminate = true;
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
                    // 构建更详细的错误信息
                    var errorContent = new StackPanel { Spacing = 12 };
                    
                    errorContent.Children.Add(new TextBlock 
                    { 
                        Text = "更新安装失败，可能的原因包括：",
                        Style = (Style)App.Current.Resources["BodyTextBlockStyle"]
                    });

                    var reasonsList = new StackPanel { Spacing = 4, Margin = new Thickness(16, 0, 0, 0) };
                    
                    reasonsList.Children.Add(new TextBlock 
                    { 
                        Text = "? 应用程序正在运行中（请关闭所有实例后重试）",
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"]
                    });
                    
                    reasonsList.Children.Add(new TextBlock 
                    { 
                        Text = "? 网络连接中断导致下载不完整",
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"]
                    });
                    
                    reasonsList.Children.Add(new TextBlock 
                    { 
                        Text = "? 系统权限不足",
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"]
                    });
                    
                    reasonsList.Children.Add(new TextBlock 
                    { 
                        Text = "? 磁盘空间不足",
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"]
                    });

                    errorContent.Children.Add(reasonsList);

                    errorContent.Children.Add(new TextBlock 
                    { 
                        Text = "建议解决方案：",
                        Style = (Style)App.Current.Resources["BodyStrongTextBlockStyle"],
                        Margin = new Thickness(0, 8, 0, 0)
                    });

                    var solutionsList = new StackPanel { Spacing = 4, Margin = new Thickness(16, 0, 0, 0) };
                    
                    solutionsList.Children.Add(new TextBlock 
                    { 
                        Text = "1. 完全关闭应用程序后重新尝试更新",
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"]
                    });
                    
                    solutionsList.Children.Add(new TextBlock 
                    { 
                        Text = "2. 检查网络连接状态",
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"]
                    });
                    
                    solutionsList.Children.Add(new TextBlock 
                    { 
                        Text = "3. 以管理员身份运行应用程序",
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"]
                    });
                    
                    solutionsList.Children.Add(new TextBlock 
                    { 
                        Text = "4. 手动从 GitHub 下载并安装最新版本",
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"]
                    });

                    errorContent.Children.Add(solutionsList);

                    var errorDialog = new ContentDialog
                    {
                        Title = "更新失败",
                        Content = errorContent,
                        PrimaryButtonText = "重试",
                        SecondaryButtonText = "手动下载",
                        CloseButtonText = "取消",
                        XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                    };

                    var result = await errorDialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        // 用户选择重试
                        await DownloadAndInstallWithProgress(downloadUrl);
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        // 用户选择手动下载，打开 GitHub 发布页面
                        try
                        {
                            var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "https://github.com/lithegreat/GameLauncher/releases/latest",
                                    UseShellExecute = true
                                }
                            };
                            process.Start();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"UpdateService: Failed to open browser: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Error in download and install with progress: {ex.Message}");
                
                // 确保关闭进度对话框
                progressDialog?.Hide();
                
                // 显示通用错误对话框
                try
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "更新失败",
                        Content = $"更新过程中发生错误：{ex.Message}\n\n请稍后重试或手动下载安装。",
                        CloseButtonText = "确定",
                        XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                    };

                    await errorDialog.ShowAsync();
                }
                catch (Exception dialogEx)
                {
                    Debug.WriteLine($"UpdateService: Failed to show error dialog: {dialogEx.Message}");
                }
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
        public bool IsPrerelease { get; set; } = false; // 新增：是否为预发布版本
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
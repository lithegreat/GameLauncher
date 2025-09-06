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
        
        // ��Ӹ�ǿ��״̬���ٱ���
        private static bool _isCheckingUpdate = false;
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static string? _lastSkippedVersion = null;
        private static UpdateCheckResult? _lastCheckResult = null;
        private static readonly TimeSpan _minimumCheckInterval = TimeSpan.FromMinutes(30); // ��С�����30����
        private static bool _hasShownUpdateDialogThisSession = false; // ���λỰ�Ƿ�����ʾ�����¶Ի���
        private static string? _lastShownVersion = null; // �ϴ���ʾ�Ի���İ汾
        private static DateTime _lastRemindLaterTime = DateTime.MinValue; // �ϴε��"�Ժ�����"��ʱ��
        private static string? _remindLaterVersion = null; // �Ժ����ѵİ汾
        private static readonly TimeSpan _remindLaterInterval = TimeSpan.FromHours(6); // �Ժ����ѵļ��ʱ�䣨6Сʱ��

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GameLauncher-UpdateService");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceCheck = false)
        {
            try
            {
                // ��ֹ�ظ����
                if (!forceCheck && _isCheckingUpdate)
                {
                    Debug.WriteLine("UpdateService: Update check already in progress");
                    return _lastCheckResult ?? new UpdateCheckResult { Success = false, Error = "��������" };
                }

                // ����Ƿ���Ҫ�����������ϴμ��ʱ��̫�̣�
                if (!forceCheck && DateTime.Now - _lastCheckTime < _minimumCheckInterval)
                {
                    Debug.WriteLine($"UpdateService: Skipping check, last check was {DateTime.Now - _lastCheckTime} ago");
                    return _lastCheckResult ?? new UpdateCheckResult { Success = true, HasUpdate = false };
                }

                _isCheckingUpdate = true;
                Debug.WriteLine("UpdateService: Starting update check");

                // ��ȡ�û�����
                var settings = UpdateSettings.GetSettings();
                GitHubRelease? releaseInfo;

                if (settings.IncludePrerelease)
                {
                    // �������Ԥ�����汾����ȡ����releases���ҵ����µ�
                    releaseInfo = await GetLatestReleaseIncludingPrerelease();
                }
                else
                {
                    // ֻ��ȡ��ʽ�汾
                    releaseInfo = await GetLatestStableRelease();
                }
                
                if (releaseInfo == null)
                {
                    var errorResult = new UpdateCheckResult { Success = false, Error = "�޷�����������Ϣ" };
                    _lastCheckResult = errorResult;
                    return errorResult;
                }

                Debug.WriteLine($"UpdateService: Found release {releaseInfo.TagName} (Prerelease: {releaseInfo.Prerelease})");

                // ��ȡ��ǰ�汾
                var currentVersion = GetCurrentVersion();
                var latestVersion = ParseVersion(releaseInfo.TagName);
                
                Debug.WriteLine($"UpdateService: Current: {currentVersion}, Latest: {latestVersion}");

                bool hasUpdate = latestVersion > currentVersion;
                
                // ����Ƿ����û������İ汾
                if (hasUpdate && _lastSkippedVersion == releaseInfo.TagName)
                {
                    Debug.WriteLine($"UpdateService: Version {releaseInfo.TagName} was skipped by user");
                    hasUpdate = false;
                }
                
                // ���� MSIX ��װ��
                var msixAsset = releaseInfo.Assets?.FirstOrDefault(a => 
                    !string.IsNullOrEmpty(a.Name) && a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));

                if (hasUpdate && msixAsset == null)
                {
                    var errorResult = new UpdateCheckResult 
                    { 
                        Success = false, 
                        Error = "�Ҳ��� MSIX ��װ��" 
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
                var errorResult = new UpdateCheckResult { Success = false, Error = $"�������: {ex.Message}" };
                _lastCheckResult = errorResult;
                return errorResult;
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("UpdateService: Request timeout");
                var errorResult = new UpdateCheckResult { Success = false, Error = "����ʱ��������������" };
                _lastCheckResult = errorResult;
                return errorResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateService: Unexpected error: {ex.Message}");
                var errorResult = new UpdateCheckResult { Success = false, Error = $"������ʧ��: {ex.Message}" };
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

                // ���˵��ݸ�汾�����汾�����򣬷������µ�
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
                
                // ������ʱ�ļ���ʹ�ø���ȫ���ļ���
                var tempFileName = $"GameLauncher_Update_{DateTime.Now:yyyyMMdd_HHmmss}.msix";
                tempPath = Path.Combine(Path.GetTempPath(), tempFileName);
                
                Debug.WriteLine($"UpdateService: Downloading to temporary path: {tempPath}");
                
                // ȷ����ʱĿ¼�����ҿ�д
                var tempDir = Path.GetDirectoryName(tempPath);
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir!);
                }
                
                // �����ļ�
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
                
                // ��֤���ص��ļ�
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
                
                // ��װ����
                var packageManager = new PackageManager();
                
                // ʹ�� file:// URI ��ʽ
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
                    // ��¼��ϸ�Ĵ�����Ϣ
                    var errorCode = deploymentResult.ExtendedErrorCode;
                    var errorText = deploymentResult.ErrorText;
                    
                    Debug.WriteLine($"UpdateService: Installation failed with extended error code: 0x{errorCode:X8}");
                    Debug.WriteLine($"UpdateService: Error text: {errorText}");
                    
                    // ���ݳ�����������ṩ���ѺõĴ�����Ϣ
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
                // ������ʱ�ļ�
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
                unchecked((int)0x80073CF3) => "Ӧ�ð���ʽ��Ч����",
                unchecked((int)0x80073CF6) => "Ӧ�ð���ǩ����Ч",
                unchecked((int)0x80073CF9) => "Ӧ�ð�������ȱʧ",
                unchecked((int)0x80073CFA) => "Ӧ�ð���������",
                unchecked((int)0x80073CFB) => "�洢�ռ䲻��",
                unchecked((int)0x80073D01) => "Ӧ�ð��Ѱ�װ�����汾��ƥ��",
                unchecked((int)0x80073D02) => "Ӧ�ó����������У��޷�����",
                unchecked((int)0x80070005) => "���ʱ��ܾ���������Ҫ����ԱȨ��",
                unchecked((int)0x80070002) => "�Ҳ���ָ�����ļ�",
                unchecked((int)0x80070057) => "������Ч",
                _ => ""
            };
        }

        public static void StartAutoUpdateCheck()
        {
            var settings = UpdateSettings.GetSettings();
            if (!settings.AutoUpdateEnabled)
            {
                Debug.WriteLine("UpdateService: Auto update is disabled");
                StopAutoUpdateCheck(); // ȷ��ֹͣ��ʱ��
                return;
            }

            // ����Ѿ��ж�ʱ�������У����ظ�����
            if (_updateTimer != null)
            {
                Debug.WriteLine("UpdateService: Auto update check already running, skipping restart");
                return;
            }

            var interval = settings.UpdateFrequency switch
            {
                UpdateFrequency.OnStartup => TimeSpan.Zero, // ����ִ��һ��
                UpdateFrequency.Weekly => TimeSpan.FromDays(7),
                _ => TimeSpan.Zero
            };

            Debug.WriteLine($"UpdateService: Starting auto update check with interval: {interval}");
            Debug.WriteLine($"UpdateService: Current session state - HasShownDialog: {_hasShownUpdateDialogThisSession}, " +
                          $"LastShownVersion: {_lastShownVersion}, RemindLaterVersion: {_remindLaterVersion}");

            if (interval > TimeSpan.Zero)
            {
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

        // ����ֶ������µķ�����ǿ�Ƽ�飬�ᵯ����
        public static async Task<UpdateCheckResult> CheckForUpdatesManuallyAsync()
        {
            return await CheckForUpdatesAsync(forceCheck: true);
        }

        // ��������汾�ķ���
        public static void SkipVersion(string version)
        {
            _lastSkippedVersion = version;
            _hasShownUpdateDialogThisSession = true;
            _lastShownVersion = version;
            Debug.WriteLine($"UpdateService: Version {version} marked as skipped");
        }

        // ���ûỰ״̬�����ڲ��Ի����������
        public static void ResetSessionState()
        {
            _hasShownUpdateDialogThisSession = false;
            _lastShownVersion = null;
            _lastSkippedVersion = null;
            _remindLaterVersion = null;
            _lastRemindLaterTime = DateTime.MinValue;
            Debug.WriteLine("UpdateService: Session state reset");
        }

        // �������Ժ�����״̬�ķ���
        public static void ClearRemindLaterState()
        {
            _remindLaterVersion = null;
            _lastRemindLaterTime = DateTime.MinValue;
            Debug.WriteLine("UpdateService: Remind later state cleared");
        }

        // ��ȡ�Ժ����ѵ�ʣ��ʱ�䣨���ڵ��Ի�UI��ʾ��
        public static TimeSpan? GetRemindLaterRemainingTime()
        {
            if (_remindLaterVersion == null) return null;
            
            var elapsed = DateTime.Now - _lastRemindLaterTime;
            var remaining = _remindLaterInterval - elapsed;
            
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
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
                    // ֻ��ָ��ʱ��ʾ����Ի���
                    if (showErrorDialog && App.Current?.MainWindow != null)
                    {
                        App.Current.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            await ShowUpdateErrorDialog(result.Error ?? "��?????");
                        });
                    }
                    return;
                }

                if (result.HasUpdate)
                {
                    Debug.WriteLine($"UpdateService: Update available: {result.LatestVersion}");
                    
                    // ����Ƿ��Ѿ��ڱ��λỰ����ʾ���˰汾�ĶԻ���
                    if (_hasShownUpdateDialogThisSession && _lastShownVersion == result.LatestVersion)
                    {
                        Debug.WriteLine($"UpdateService: Update dialog for version {result.LatestVersion} already shown this session");
                        return;
                    }
                    
                    // ����Ժ�����״̬
                    if (_remindLaterVersion == result.LatestVersion && 
                        DateTime.Now - _lastRemindLaterTime < _remindLaterInterval)
                    {
                        var remainingTime = _remindLaterInterval - (DateTime.Now - _lastRemindLaterTime);
                        Debug.WriteLine($"UpdateService: Version {result.LatestVersion} is in 'remind later' period. " +
                                      $"Will remind again in {remainingTime.TotalMinutes:F0} minutes");
                        return;
                    }
                    
                    // ��UI�߳�����ʾ������ʾ
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
                    Title = "���¼��ʧ��",
                    Content = $"�޷������£�{error}",
                    CloseButtonText = "ȷ��",
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
                // �������ʾ�Ի���
                _hasShownUpdateDialogThisSession = true;
                _lastShownVersion = result.LatestVersion;

                // ������ϸ�İ汾��ϢUI
                var stackPanel = new StackPanel { Spacing = 16, MaxWidth = 500 };

                // �汾��Ϣ����
                var versionInfoPanel = new StackPanel { Spacing = 8 };
                
                // ����
                var titleText = new TextBlock
                {
                    Text = result.IsPrerelease ? "�����µ�Ԥ�����汾" : "�����°汾",
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Try to get style safely, fallback to default if null
                if (App.Current?.Resources?.TryGetValue("SubtitleTextBlockStyle", out var subtitleStyle) == true)
                {
                    titleText.Style = (Style)subtitleStyle;
                }

                versionInfoPanel.Children.Add(titleText);

                // �汾�Ա���Ϣ
                var versionComparePanel = new Grid();
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // ��ǰ�汾
                var currentVersionPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var currentVersionCaptionText = new TextBlock 
                { 
                    Text = "��ǰ�汾", 
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                };
                if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle) == true)
                {
                    currentVersionCaptionText.Style = (Style)captionStyle;
                }
                currentVersionPanel.Children.Add(currentVersionCaptionText);

                var currentVersionStrongText = new TextBlock 
                { 
                    Text = result.CurrentVersion ?? "δ֪", 
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                if (App.Current?.Resources?.TryGetValue("BodyStrongTextBlockStyle", out var bodyStrongStyle) == true)
                {
                    currentVersionStrongText.Style = (Style)bodyStrongStyle;
                }
                currentVersionPanel.Children.Add(currentVersionStrongText);
                Grid.SetColumn(currentVersionPanel, 0);
                versionComparePanel.Children.Add(currentVersionPanel);

                // ��ͷ - ʹ�ø�ͨ�õķ�����ʾ��ͷ
                var arrowIcon = new SymbolIcon(Symbol.Forward)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.6
                };
                Grid.SetColumn(arrowIcon, 1);
                versionComparePanel.Children.Add(arrowIcon);

                // ���°汾
                var latestVersionPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var latestVersionCaptionText = new TextBlock 
                { 
                    Text = result.IsPrerelease ? "����Ԥ�����汾" : "���°汾", 
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                };
                if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle2) == true)
                {
                    latestVersionCaptionText.Style = (Style)captionStyle2;
                }
                latestVersionPanel.Children.Add(latestVersionCaptionText);
                
                var latestVersionText = new TextBlock 
                { 
                    Text = result.LatestVersion ?? "δ֪", 
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                if (App.Current?.Resources?.TryGetValue("BodyStrongTextBlockStyle", out var bodyStrongStyle2) == true)
                {
                    latestVersionText.Style = (Style)bodyStrongStyle2;
                }
                
                if (result.IsPrerelease)
                {
                    latestVersionText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
                
                latestVersionPanel.Children.Add(latestVersionText);
                Grid.SetColumn(latestVersionPanel, 2);
                versionComparePanel.Children.Add(latestVersionPanel);

                versionInfoPanel.Children.Add(versionComparePanel);

                // Ԥ�����汾���� - ʹ��FontIcon���ı�������Ϊ��ѡ����
                if (result.IsPrerelease)
                {
                    var warningPanel = new StackPanel 
                    { 
                        Orientation = Orientation.Horizontal, 
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 8
                    };
                    
                    // ʹ�� FontIcon ��Ϊ����ͼ�꣬ʹ�� Segoe MDL2 Assets ����
                    var warningIcon = new FontIcon
                    {
                        Glyph = "\uE814", // Warning symbol from Segoe MDL2 Assets
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 16
                    };
                    warningPanel.Children.Add(warningIcon);
                    
                    var warningTextBlock = new TextBlock 
                    { 
                        Text = "����һ��Ԥ�����汾�����ܰ������ȶ��Ĺ���", 
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle3) == true)
                    {
                        warningTextBlock.Style = (Style)captionStyle3;
                    }
                    warningPanel.Children.Add(warningTextBlock);
                    
                    versionInfoPanel.Children.Add(warningPanel);
                }

                stackPanel.Children.Add(versionInfoPanel);

                // ����˵��
                if (!string.IsNullOrWhiteSpace(result.ReleaseNotes))
                {
                    var releaseNotesPanel = new StackPanel { Spacing = 8 };
                    
                    var releaseNotesTitle = new TextBlock 
                    { 
                        Text = "��������"
                    };
                    if (App.Current?.Resources?.TryGetValue("BodyStrongTextBlockStyle", out var bodyStrongStyle3) == true)
                    {
                        releaseNotesTitle.Style = (Style)bodyStrongStyle3;
                    }
                    releaseNotesPanel.Children.Add(releaseNotesTitle);

                    var scrollViewer = new ScrollViewer 
                    { 
                        MaxHeight = 150,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                    };

                    var releaseNotesText = new TextBlock 
                    { 
                        Text = result.ReleaseNotes.Trim(),
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                        Opacity = 0.8
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle4) == true)
                    {
                        releaseNotesText.Style = (Style)captionStyle4;
                    }

                    scrollViewer.Content = releaseNotesText;
                    releaseNotesPanel.Children.Add(scrollViewer);
                    stackPanel.Children.Add(releaseNotesPanel);
                }

                // ������ʾ
                var actionText = new TextBlock 
                { 
                    Text = "�Ƿ��������ز���װ���£�",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                if (App.Current?.Resources?.TryGetValue("BodyTextBlockStyle", out var bodyStyle) == true)
                {
                    actionText.Style = (Style)bodyStyle;
                }
                stackPanel.Children.Add(actionText);
                
                var dialog = new ContentDialog
                {
                    Title = result.IsPrerelease ? "�����µ�Ԥ�����汾" : "�����°汾",
                    Content = stackPanel,
                    PrimaryButtonText = "��������",
                    SecondaryButtonText = "�Ժ�����",
                    CloseButtonText = "�����˰汾",
                    XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                };

                var dialogResult = await dialog.ShowAsync();
                
                if (dialogResult == ContentDialogResult.Primary && !string.IsNullOrEmpty(result.DownloadUrl))
                {
                    await DownloadAndInstallWithProgress(result.DownloadUrl);
                }
                else if (dialogResult == ContentDialogResult.None) // �û������"�����˰汾"
                {
                    SkipVersion(result.LatestVersion ?? "");
                }
                else if (dialogResult == ContentDialogResult.Secondary) // �û������"�Ժ�����"
                {
                    // �Ժ����ѣ������Ժ�����״̬�������ûỰ״̬
                    _remindLaterVersion = result.LatestVersion;
                    _lastRemindLaterTime = DateTime.Now;
                    _hasShownUpdateDialogThisSession = true; // ���ֻỰ״̬�����������ٴε���
                    _lastShownVersion = result.LatestVersion;
                    Debug.WriteLine($"UpdateService: User chose to be reminded later for version {result.LatestVersion}. " +
                                  $"Will remind again after {_remindLaterInterval.TotalHours} hours");
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
                    Title = "���ڸ���",
                    Content = "�������ظ���...",
                    XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                };

                var progressBar = new ProgressBar
                {
                    IsIndeterminate = false,
                    Value = 0,
                    Minimum = 0,
                    Maximum = 100
                };

                var statusText = new TextBlock { Text = "�������ظ���..." };
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
                        statusText.Text = $"�������ظ���... {value}%";
                    });
                });

                // ����״̬Ϊ"���ڰ�װ"
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
                        Title = "�������",
                        Content = "�����ѳɹ���װ��Ӧ�ó�������������",
                        CloseButtonText = "ȷ��",
                        XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                    };

                    await successDialog.ShowAsync();
                }
                else
                {
                    // ��������ϸ�Ĵ�����Ϣ
                    var errorContent = new StackPanel { Spacing = 12 };
                    
                    var errorTitleText = new TextBlock 
                    { 
                        Text = "���°�װʧ�ܣ����ܵ�ԭ�������"
                    };
                    if (App.Current?.Resources?.TryGetValue("BodyTextBlockStyle", out var bodyStyle2) == true)
                    {
                        errorTitleText.Style = (Style)bodyStyle2;
                    }
                    errorContent.Children.Add(errorTitleText);

                    var reasonsList = new StackPanel { Spacing = 4, Margin = new Thickness(16, 0, 0, 0) };
                    
                    var reasonText1 = new TextBlock 
                    { 
                        Text = "Ӧ�ó������������У���ر�����ʵ�������ԣ�"
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle5) == true)
                    {
                        reasonText1.Style = (Style)captionStyle5;
                    }
                    reasonsList.Children.Add(reasonText1);
                    
                    var reasonText2 = new TextBlock 
                    { 
                        Text = "? ���������жϵ������ز�����"
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle6) == true)
                    {
                        reasonText2.Style = (Style)captionStyle6;
                    }
                    reasonsList.Children.Add(reasonText2);
                    
                    var reasonText3 = new TextBlock 
                    { 
                        Text = "ϵͳȨ�޲���"
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle7) == true)
                    {
                        reasonText3.Style = (Style)captionStyle7;
                    }
                    reasonsList.Children.Add(reasonText3);
                    
                    var reasonText4 = new TextBlock 
                    { 
                        Text = "���̿ռ䲻��"
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle8) == true)
                    {
                        reasonText4.Style = (Style)captionStyle8;
                    }
                    reasonsList.Children.Add(reasonText4);

                    errorContent.Children.Add(reasonsList);

                    var solutionsTitle = new TextBlock 
                    { 
                        Text = "������������",
                        Margin = new Thickness(0, 8, 0, 0)
                    };
                    if (App.Current?.Resources?.TryGetValue("BodyStrongTextBlockStyle", out var bodyStrongStyle4) == true)
                    {
                        solutionsTitle.Style = (Style)bodyStrongStyle4;
                    }
                    errorContent.Children.Add(solutionsTitle);

                    var solutionsList = new StackPanel { Spacing = 4, Margin = new Thickness(16, 0, 0, 0) };
                    
                    var solutionText1 = new TextBlock 
                    { 
                        Text = "1. ��ȫ�ر�Ӧ�ó�������³��Ը���"
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle9) == true)
                    {
                        solutionText1.Style = (Style)captionStyle9;
                    }
                    solutionsList.Children.Add(solutionText1);
                    
                    var solutionText2 = new TextBlock 
                    { 
                        Text = "2. �����������״̬"
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle10) == true)
                    {
                        solutionText2.Style = (Style)captionStyle10;
                    }
                    solutionsList.Children.Add(solutionText2);
                    
                    var solutionText3 = new TextBlock 
                    { 
                        Text = "3. �Թ���Ա�������Ӧ�ó���"
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle11) == true)
                    {
                        solutionText3.Style = (Style)captionStyle11;
                    }
                    solutionsList.Children.Add(solutionText3);
                    
                    var solutionText4 = new TextBlock 
                    { 
                        Text = "4. �ֶ��� GitHub ���ز���װ���°汾"
                    };
                    if (App.Current?.Resources?.TryGetValue("CaptionTextBlockStyle", out var captionStyle12) == true)
                    {
                        solutionText4.Style = (Style)captionStyle12;
                    }
                    solutionsList.Children.Add(solutionText4);

                    errorContent.Children.Add(solutionsList);

                    var errorDialog = new ContentDialog
                    {
                        Title = "����ʧ��",
                        Content = errorContent,
                        PrimaryButtonText = "����",
                        SecondaryButtonText = "�ֶ�����",
                        CloseButtonText = "ȡ��",
                        XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                    };

                    var result = await errorDialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        // �û�ѡ������
                        await DownloadAndInstallWithProgress(downloadUrl);
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        // �û�ѡ���ֶ����أ��� GitHub ����ҳ��
                        try
                        {
                            using var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "https://github.com/lithegreat/GameLauncher/releases/latest",
                                    UseShellExecute = true
                                }
                            };
                            _ = process.Start(); // Explicitly ignore the return value to fix CS8602 warning
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
                
                // ȷ���رս��ȶԻ���
                progressDialog?.Hide();
                
                // ��ʾͨ�ô���Ի���
                try
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "����ʧ��",
                        Content = $"���¹����з�������{ex.Message}\n\n���Ժ����Ի��ֶ����ذ�װ��",
                        CloseButtonText = "ȷ��",
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
                // �Ƴ��汾�ַ����е� 'v' ǰ׺
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
        public bool IsPrerelease { get; set; } = false; // �������Ƿ�ΪԤ�����汾
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
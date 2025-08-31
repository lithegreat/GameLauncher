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
        
        // ��Ӹ�ǿ��״̬���ٱ���
        private static bool _isCheckingUpdate = false;
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static string? _lastSkippedVersion = null;
        private static UpdateCheckResult? _lastCheckResult = null;
        private static readonly TimeSpan _minimumCheckInterval = TimeSpan.FromMinutes(30); // ��С�����30����
        private static bool _hasShownUpdateDialogThisSession = false; // ���λỰ�Ƿ�����ʾ�����¶Ի���
        private static string? _lastShownVersion = null; // �ϴ���ʾ�Ի���İ汾

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
                    var errorResult = new UpdateCheckResult { Success = false, Error = "�޷�����������Ϣ" };
                    _lastCheckResult = errorResult;
                    return errorResult;
                }

                Debug.WriteLine($"UpdateService: Found release {releaseInfo.TagName}");

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
                    a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));

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
                    ReleaseNotes = releaseInfo.Body
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

        public static async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
        {
            try
            {
                Debug.WriteLine($"UpdateService: Starting download from {downloadUrl}");
                
                // ������ʱ�ļ�
                var tempPath = Path.Combine(Path.GetTempPath(), "GameLauncher_Update.msix");
                
                // �����ļ�
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
                
                // ��װ����
                var packageManager = new PackageManager();
                var deploymentResult = await packageManager.AddPackageAsync(
                    new Uri(tempPath),
                    null,
                    DeploymentOptions.ForceApplicationShutdown);
                
                if (deploymentResult.IsRegistered)
                {
                    Debug.WriteLine("UpdateService: Update installed successfully");
                    
                    // ������ʱ�ļ�
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
                    // ֻ��ָ��ʱ��ʾ����Ի���
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
                    
                    // ����Ƿ��Ѿ��ڱ��λỰ����ʾ���˰汾�ĶԻ���
                    if (_hasShownUpdateDialogThisSession && _lastShownVersion == result.LatestVersion)
                    {
                        Debug.WriteLine($"UpdateService: Update dialog for version {result.LatestVersion} already shown this session");
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
                
                var dialog = new ContentDialog
                {
                    Title = "�����°汾",
                    Content = $"�����°汾 {result.LatestVersion}����ǰ�汾 {result.CurrentVersion}��\n\n�Ƿ��������ز���װ���£�",
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
                    // �Ժ����ѣ����ûỰ״̬�����������汾
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

                var stackPanel = new StackPanel();
                stackPanel.Children.Add(new TextBlock { Text = "�������ظ���..." });
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
                        Title = "�������",
                        Content = "�����ѳɹ���װ��Ӧ�ó�������������",
                        CloseButtonText = "ȷ��",
                        XamlRoot = App.Current?.MainWindow?.Content?.XamlRoot
                    };

                    await successDialog.ShowAsync();
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "����ʧ��",
                        Content = "���°�װʧ�ܣ����Ժ����Ի��ֶ����ذ�װ��",
                        CloseButtonText = "ȷ��",
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
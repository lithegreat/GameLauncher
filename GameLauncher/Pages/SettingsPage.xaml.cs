using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using GameLauncher.Services;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace GameLauncher.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsPage: Starting initialization");
                this.InitializeComponent();
                System.Diagnostics.Debug.WriteLine("SettingsPage: InitializeComponent completed");
                
                this.Loaded += SettingsPage_Loaded;
                System.Diagnostics.Debug.WriteLine("SettingsPage: Constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsPage constructor error: {ex}");
                // Don't re-throw, try to continue
            }
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsPage: Page loaded, loading settings");
                LoadThemeSettings();
                LoadUpdateSettings();
                LoadVersionInfo();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsPage_Loaded error: {ex}");
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsPage: OnNavigatedTo");
                base.OnNavigatedTo(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnNavigatedTo error: {ex}");
            }
        }

        private void LoadThemeSettings()
        {
            try
            {
                // Wait a bit to ensure controls are fully loaded
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var savedTheme = ThemeService.GetSavedThemeString();
                        System.Diagnostics.Debug.WriteLine($"Loading saved theme: {savedTheme}");

                        // Clear all selections first
                        if (LightThemeRadio != null) LightThemeRadio.IsChecked = false;
                        if (DarkThemeRadio != null) DarkThemeRadio.IsChecked = false;
                        if (SystemThemeRadio != null) SystemThemeRadio.IsChecked = false;

                        // Set the appropriate radio button
                        switch (savedTheme)
                        {
                            case "Light":
                                if (LightThemeRadio != null)
                                {
                                    LightThemeRadio.IsChecked = true;
                                    System.Diagnostics.Debug.WriteLine("Set Light theme");
                                }
                                break;
                            case "Dark":
                                if (DarkThemeRadio != null)
                                {
                                    DarkThemeRadio.IsChecked = true;
                                    System.Diagnostics.Debug.WriteLine("Set Dark theme");
                                }
                                break;
                            case "Default":
                            default:
                                if (SystemThemeRadio != null)
                                {
                                    SystemThemeRadio.IsChecked = true;
                                    System.Diagnostics.Debug.WriteLine("Set System theme");
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in dispatched LoadThemeSettings: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadThemeSettings error: {ex}");
            }
        }

        private void LoadUpdateSettings()
        {
            try
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var settings = UpdateSettings.GetSettings();
                        System.Diagnostics.Debug.WriteLine($"Loading update settings: AutoUpdate={settings.AutoUpdateEnabled}, Frequency={settings.UpdateFrequency}");

                        // 设置自动更新开关
                        if (AutoUpdateToggle != null)
                        {
                            AutoUpdateToggle.IsOn = settings.AutoUpdateEnabled;
                        }

                        // 设置检查频率
                        if (OnStartupRadio != null) OnStartupRadio.IsChecked = false;
                        if (WeeklyRadio != null) WeeklyRadio.IsChecked = false;

                        switch (settings.UpdateFrequency)
                        {
                            case UpdateFrequency.OnStartup:
                                if (OnStartupRadio != null)
                                {
                                    OnStartupRadio.IsChecked = true;
                                }
                                break;
                            case UpdateFrequency.Weekly:
                                if (WeeklyRadio != null)
                                {
                                    WeeklyRadio.IsChecked = true;
                                }
                                break;
                        }

                        // 根据自动更新状态启用/禁用频率设置
                        UpdateFrequencyPanelVisibility(settings.AutoUpdateEnabled);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in dispatched LoadUpdateSettings: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadUpdateSettings error: {ex}");
            }
        }

        private void LoadVersionInfo()
        {
            try
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var package = Package.Current;
                        var version = package.Id.Version;
                        var versionString = $"GameLauncher v{version.Major}.{version.Minor}.{version.Build}";
                        
                        if (VersionTextBlock != null)
                        {
                            VersionTextBlock.Text = versionString;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Version info loaded: {versionString}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading version info: {ex}");
                        if (VersionTextBlock != null)
                        {
                            VersionTextBlock.Text = "GameLauncher v1.0.0";
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadVersionInfo error: {ex}");
            }
        }

        private void UpdateFrequencyPanelVisibility(bool isEnabled)
        {
            if (UpdateFrequencyPanel != null)
            {
                UpdateFrequencyPanel.Opacity = isEnabled ? 1.0 : 0.5;
                UpdateFrequencyPanel.IsHitTestVisible = isEnabled;
            }
        }

        private void ThemeRadio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is RadioButton radioButton && radioButton.Tag is string themeValue)
                {
                    System.Diagnostics.Debug.WriteLine($"Theme radio clicked: {themeValue}");
                    
                    ElementTheme theme = themeValue switch
                    {
                        "Light" => ElementTheme.Light,
                        "Dark" => ElementTheme.Dark,
                        "Default" => ElementTheme.Default,
                        _ => ElementTheme.Default
                    };

                    // Apply theme using the service
                    ThemeService.ApplyTheme(theme);
                    System.Diagnostics.Debug.WriteLine($"Applied theme: {theme}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeRadio_Click error: {ex}");
            }
        }

        private void AutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ToggleSwitch toggle)
                {
                    var isEnabled = toggle.IsOn;
                    System.Diagnostics.Debug.WriteLine($"Auto update toggled: {isEnabled}");

                    // 更新频率面板的可用性
                    UpdateFrequencyPanelVisibility(isEnabled);

                    // 保存设置
                    SaveUpdateSettings();

                    // 根据设置启动或停止自动更新检查（但不重复启动）
                    if (isEnabled)
                    {
                        // 只有在没有运行时才启动
                        UpdateService.StartAutoUpdateCheck();
                    }
                    else
                    {
                        UpdateService.StopAutoUpdateCheck();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoUpdateToggle_Toggled error: {ex}");
            }
        }

        private void UpdateFrequencyRadio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is RadioButton radioButton && radioButton.Tag is string frequencyValue)
                {
                    System.Diagnostics.Debug.WriteLine($"Update frequency radio clicked: {frequencyValue}");
                    
                    // 保存设置
                    SaveUpdateSettings();

                    // 仅在自动更新启用时重新启动检查
                    if (AutoUpdateToggle?.IsOn == true)
                    {
                        // 先停止再启动以应用新的频率设置
                        UpdateService.StopAutoUpdateCheck();
                        UpdateService.StartAutoUpdateCheck();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateFrequencyRadio_Click error: {ex}");
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Manual update check initiated");
                
                if (CheckUpdateButton != null)
                {
                    CheckUpdateButton.IsEnabled = false;
                    CheckUpdateButton.Content = "检查中...";
                }

                // 使用手动检查方法（强制检查）
                var result = await UpdateService.CheckForUpdatesManuallyAsync();

                if (CheckUpdateButton != null)
                {
                    CheckUpdateButton.IsEnabled = true;
                    CheckUpdateButton.Content = "立即检查更新";
                }

                if (!result.Success)
                {
                    await ShowDialog("检查更新失败", result.Error ?? "未知错误");
                    return;
                }

                if (result.HasUpdate)
                {
                    var message = $"发现新版本 {result.LatestVersion}，当前版本 {result.CurrentVersion}。\n\n是否立即下载并安装更新？";
                    var updateDialog = new ContentDialog
                    {
                        Title = "发现新版本",
                        Content = message,
                        PrimaryButtonText = "立即更新",
                        SecondaryButtonText = "稍后提醒",
                        CloseButtonText = "跳过此版本",
                        XamlRoot = this.XamlRoot
                    };

                    var dialogResult = await updateDialog.ShowAsync();

                    if (dialogResult == ContentDialogResult.Primary && !string.IsNullOrEmpty(result.DownloadUrl))
                    {
                        await PerformUpdateWithProgress(result.DownloadUrl);
                    }
                    else if (dialogResult == ContentDialogResult.None) // 用户点击了"跳过此版本"
                    {
                        UpdateService.SkipVersion(result.LatestVersion ?? "");
                        await ShowDialog("版本已跳过", "此版本更新已跳过，如需重新检查请重启应用。");
                    }
                }
                else
                {
                    await ShowDialog("检查更新", "您当前使用的已是最新版本。");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckUpdateButton_Click error: {ex}");
                
                if (CheckUpdateButton != null)
                {
                    CheckUpdateButton.IsEnabled = true;
                    CheckUpdateButton.Content = "立即检查更新";
                }
                
                await ShowDialog("检查更新失败", $"检查更新时发生错误: {ex.Message}");
            }
        }

        private async Task PerformUpdateWithProgress(string downloadUrl)
        {
            try
            {
                var progressDialog = new ContentDialog
                {
                    Title = "正在更新",
                    XamlRoot = this.XamlRoot
                };

                var progressBar = new ProgressBar
                {
                    IsIndeterminate = false,
                    Value = 0,
                    Minimum = 0,
                    Maximum = 100,
                    Width = 300
                };

                var textBlock = new TextBlock { Text = "正在下载更新..." };

                var stackPanel = new StackPanel { Spacing = 16 };
                stackPanel.Children.Add(textBlock);
                stackPanel.Children.Add(progressBar);

                progressDialog.Content = stackPanel;

                var showTask = progressDialog.ShowAsync();

                var progress = new Progress<int>(value =>
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        progressBar.Value = value;
                        textBlock.Text = $"正在下载更新... {value}%";
                    });
                });

                var success = await UpdateService.DownloadAndInstallUpdateAsync(downloadUrl, progress);

                progressDialog.Hide();

                if (success)
                {
                    await ShowDialog("更新完成", "更新已成功安装，应用程序将重新启动。");
                }
                else
                {
                    await ShowDialog("更新失败", "更新安装失败，请稍后重试或手动下载安装。");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PerformUpdateWithProgress error: {ex}");
                await ShowDialog("更新失败", $"更新过程中发生错误: {ex.Message}");
            }
        }

        private async Task ShowDialog(string title, string content)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowDialog error: {ex}");
            }
        }

        private void SaveUpdateSettings()
        {
            try
            {
                var autoUpdateEnabled = AutoUpdateToggle?.IsOn ?? false;
                
                var updateFrequency = UpdateFrequency.OnStartup;
                if (WeeklyRadio?.IsChecked == true)
                {
                    updateFrequency = UpdateFrequency.Weekly;
                }

                UpdateSettings.SaveSettings(autoUpdateEnabled, updateFrequency);
                System.Diagnostics.Debug.WriteLine($"Update settings saved: AutoUpdate={autoUpdateEnabled}, Frequency={updateFrequency}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveUpdateSettings error: {ex}");
            }
        }
    }
}
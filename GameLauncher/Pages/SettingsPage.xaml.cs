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
                        System.Diagnostics.Debug.WriteLine($"Loading update settings: AutoUpdate={settings.AutoUpdateEnabled}, Frequency={settings.UpdateFrequency}, IncludePrerelease={settings.IncludePrerelease}");

                        // 设置自动更新开关
                        if (AutoUpdateToggle != null)
                        {
                            AutoUpdateToggle.IsOn = settings.AutoUpdateEnabled;
                        }

                        // 设置预发布版本开关
                        if (IncludePrereleaseToggle != null)
                        {
                            IncludePrereleaseToggle.IsOn = settings.IncludePrerelease;
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

                        // 根据自动更新状态启用/禁用相关设置
                        UpdateSettingsPanelVisibility(settings.AutoUpdateEnabled);
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

        private void UpdateSettingsPanelVisibility(bool isEnabled)
        {
            // 更新频率面板
            if (UpdateFrequencyPanel != null)
            {
                UpdateFrequencyPanel.Opacity = isEnabled ? 1.0 : 0.5;
                UpdateFrequencyPanel.IsHitTestVisible = isEnabled;
            }

            // 预发布版本开关
            if (IncludePrereleaseToggle != null)
            {
                IncludePrereleaseToggle.Opacity = isEnabled ? 1.0 : 0.5;
                IncludePrereleaseToggle.IsHitTestVisible = isEnabled;
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

                    // 更新相关设置的可用性
                    UpdateSettingsPanelVisibility(isEnabled);

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

        private void IncludePrereleaseToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ToggleSwitch toggle)
                {
                    var includePrerelease = toggle.IsOn;
                    System.Diagnostics.Debug.WriteLine($"Include prerelease toggled: {includePrerelease}");

                    // 保存设置
                    SaveUpdateSettings();

                    // 如果自动更新启用，重新启动检查以应用新设置
                    if (AutoUpdateToggle?.IsOn == true)
                    {
                        UpdateService.StopAutoUpdateCheck();
                        UpdateService.StartAutoUpdateCheck();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IncludePrereleaseToggle_Toggled error: {ex}");
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
                    await ShowUpdateDetailsDialog(result);
                }
                else
                {
                    await ShowNoUpdateDialog(result);
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

        private async Task ShowUpdateDetailsDialog(UpdateCheckResult result)
        {
            try
            {
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

                // 箭头 - 使用SymbolIcon替代FontIcon
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

                var updateDialog = new ContentDialog
                {
                    Title = result.IsPrerelease ? "发现新的预发布版本" : "发现新版本",
                    Content = stackPanel,
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowUpdateDetailsDialog error: {ex}");
                await ShowDialog("显示更新信息失败", $"显示更新详情时发生错误: {ex.Message}");
            }
        }

        private async Task ShowNoUpdateDialog(UpdateCheckResult result)
        {
            try
            {
                // 构建无更新信息UI，包含远端版本信息用于调试
                var stackPanel = new StackPanel { Spacing = 16, MaxWidth = 450 };

                // 成功图标和标题
                var headerPanel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
                
                var successIcon = new SymbolIcon(Symbol.Accept)
                {
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                headerPanel.Children.Add(successIcon);

                headerPanel.Children.Add(new TextBlock 
                { 
                    Text = "您使用的已是最新版本", 
                    Style = (Style)App.Current.Resources["SubtitleTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                stackPanel.Children.Add(headerPanel);

                // 版本信息对比 - 显示当前版本和远端版本
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

                // 等于号图标
                var equalText = new TextBlock
                {
                    Text = "=",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.6
                };
                Grid.SetColumn(equalText, 1);
                versionComparePanel.Children.Add(equalText);

                // 远端版本
                var remoteVersionPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                remoteVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = result.IsPrerelease ? "远端预发布版本" : "远端最新版本", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                });
                
                var remoteVersionText = new TextBlock 
                { 
                    Text = result.LatestVersion ?? "未知", 
                    Style = (Style)App.Current.Resources["BodyStrongTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                
                if (result.IsPrerelease)
                {
                    remoteVersionText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
                
                remoteVersionPanel.Children.Add(remoteVersionText);
                Grid.SetColumn(remoteVersionPanel, 2);
                versionComparePanel.Children.Add(remoteVersionPanel);

                stackPanel.Children.Add(versionComparePanel);

                // 预发布版本说明
                if (result.IsPrerelease)
                {
                    var prereleaseInfoPanel = new StackPanel 
                    { 
                        Orientation = Orientation.Horizontal, 
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 8
                    };
                    
                    var infoIcon = new SymbolIcon(Symbol.Help)
                    {
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    prereleaseInfoPanel.Children.Add(infoIcon);
                    
                    prereleaseInfoPanel.Children.Add(new TextBlock 
                    { 
                        Text = "已启用预发布版本检测", 
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    
                    stackPanel.Children.Add(prereleaseInfoPanel);
                }

                // 调试信息
                var debugInfoPanel = new StackPanel { Spacing = 4 };
                debugInfoPanel.Children.Add(new TextBlock 
                { 
                    Text = "调试信息", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.5
                });

                var debugText = $"检查类型: {(result.IsPrerelease ? "包含预发布版本" : "仅正式版本")}\n" +
                               $"远端版本: {result.LatestVersion ?? "未获取"}\n" +
                               $"版本类型: {(result.IsPrerelease ? "预发布版本" : "正式版本")}";

                debugInfoPanel.Children.Add(new TextBlock 
                { 
                    Text = debugText,
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.4,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                });

                stackPanel.Children.Add(debugInfoPanel);

                var dialog = new ContentDialog
                {
                    Title = "检查更新",
                    Content = stackPanel,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowNoUpdateDialog error: {ex}");
                await ShowDialog("检查更新", "您当前使用的已是最新版本。");
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
                var includePrerelease = IncludePrereleaseToggle?.IsOn ?? false;
                
                var updateFrequency = UpdateFrequency.OnStartup;
                if (WeeklyRadio?.IsChecked == true)
                {
                    updateFrequency = UpdateFrequency.Weekly;
                }

                UpdateSettings.SaveSettings(autoUpdateEnabled, updateFrequency, includePrerelease);
                System.Diagnostics.Debug.WriteLine($"Update settings saved: AutoUpdate={autoUpdateEnabled}, Frequency={updateFrequency}, IncludePrerelease={includePrerelease}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveUpdateSettings error: {ex}");
            }
        }
    }
}
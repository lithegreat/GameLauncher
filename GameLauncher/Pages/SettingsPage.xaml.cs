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

                        // �����Զ����¿���
                        if (AutoUpdateToggle != null)
                        {
                            AutoUpdateToggle.IsOn = settings.AutoUpdateEnabled;
                        }

                        // ����Ԥ�����汾����
                        if (IncludePrereleaseToggle != null)
                        {
                            IncludePrereleaseToggle.IsOn = settings.IncludePrerelease;
                        }

                        // ���ü��Ƶ��
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

                        // �����Զ�����״̬����/�����������
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
            // ����Ƶ�����
            if (UpdateFrequencyPanel != null)
            {
                UpdateFrequencyPanel.Opacity = isEnabled ? 1.0 : 0.5;
                UpdateFrequencyPanel.IsHitTestVisible = isEnabled;
            }

            // Ԥ�����汾����
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

                    // ����������õĿ�����
                    UpdateSettingsPanelVisibility(isEnabled);

                    // ��������
                    SaveUpdateSettings();

                    // ��������������ֹͣ�Զ����¼�飨�����ظ�������
                    if (isEnabled)
                    {
                        // ֻ����û������ʱ������
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
                    
                    // ��������
                    SaveUpdateSettings();

                    // �����Զ���������ʱ�����������
                    if (AutoUpdateToggle?.IsOn == true)
                    {
                        // ��ֹͣ��������Ӧ���µ�Ƶ������
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

                    // ��������
                    SaveUpdateSettings();

                    // ����Զ��������ã��������������Ӧ��������
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
                    CheckUpdateButton.Content = "�����...";
                }

                // ʹ���ֶ���鷽����ǿ�Ƽ�飩
                var result = await UpdateService.CheckForUpdatesManuallyAsync();

                if (CheckUpdateButton != null)
                {
                    CheckUpdateButton.IsEnabled = true;
                    CheckUpdateButton.Content = "����������";
                }

                if (!result.Success)
                {
                    await ShowDialog("������ʧ��", result.Error ?? "δ֪����");
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
                    CheckUpdateButton.Content = "����������";
                }
                
                await ShowDialog("������ʧ��", $"������ʱ��������: {ex.Message}");
            }
        }

        private async Task ShowUpdateDetailsDialog(UpdateCheckResult result)
        {
            try
            {
                // ������ϸ�İ汾��ϢUI
                var stackPanel = new StackPanel { Spacing = 16, MaxWidth = 500 };

                // �汾��Ϣ����
                var versionInfoPanel = new StackPanel { Spacing = 8 };
                
                // ����
                var titleText = new TextBlock
                {
                    Text = result.IsPrerelease ? "�����µ�Ԥ�����汾" : "�����°汾",
                    Style = (Style)App.Current.Resources["SubtitleTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                versionInfoPanel.Children.Add(titleText);

                // �汾�Ա���Ϣ
                var versionComparePanel = new Grid();
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // ��ǰ�汾
                var currentVersionPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                currentVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = "��ǰ�汾", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                });
                currentVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = result.CurrentVersion ?? "δ֪", 
                    Style = (Style)App.Current.Resources["BodyStrongTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                Grid.SetColumn(currentVersionPanel, 0);
                versionComparePanel.Children.Add(currentVersionPanel);

                // ��ͷ - ʹ��SymbolIcon���FontIcon
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
                latestVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = result.IsPrerelease ? "����Ԥ�����汾" : "���°汾", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                });
                
                var latestVersionText = new TextBlock 
                { 
                    Text = result.LatestVersion ?? "δ֪", 
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

                // Ԥ�����汾���� - ʹ��SymbolIcon���FontIcon
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
                        Text = "����һ��Ԥ�����汾�����ܰ������ȶ��Ĺ���", 
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    
                    versionInfoPanel.Children.Add(warningPanel);
                }

                stackPanel.Children.Add(versionInfoPanel);

                // ����˵��
                if (!string.IsNullOrWhiteSpace(result.ReleaseNotes))
                {
                    var releaseNotesPanel = new StackPanel { Spacing = 8 };
                    
                    releaseNotesPanel.Children.Add(new TextBlock 
                    { 
                        Text = "��������", 
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

                // ������ʾ
                var actionText = new TextBlock 
                { 
                    Text = "�Ƿ��������ز���װ���£�",
                    Style = (Style)App.Current.Resources["BodyTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                stackPanel.Children.Add(actionText);

                var updateDialog = new ContentDialog
                {
                    Title = result.IsPrerelease ? "�����µ�Ԥ�����汾" : "�����°汾",
                    Content = stackPanel,
                    PrimaryButtonText = "��������",
                    SecondaryButtonText = "�Ժ�����",
                    CloseButtonText = "�����˰汾",
                    XamlRoot = this.XamlRoot
                };

                var dialogResult = await updateDialog.ShowAsync();

                if (dialogResult == ContentDialogResult.Primary && !string.IsNullOrEmpty(result.DownloadUrl))
                {
                    await PerformUpdateWithProgress(result.DownloadUrl);
                }
                else if (dialogResult == ContentDialogResult.None) // �û������"�����˰汾"
                {
                    UpdateService.SkipVersion(result.LatestVersion ?? "");
                    await ShowDialog("�汾������", "�˰汾�������������������¼��������Ӧ�á�");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowUpdateDetailsDialog error: {ex}");
                await ShowDialog("��ʾ������Ϣʧ��", $"��ʾ��������ʱ��������: {ex.Message}");
            }
        }

        private async Task ShowNoUpdateDialog(UpdateCheckResult result)
        {
            try
            {
                // �����޸�����ϢUI������Զ�˰汾��Ϣ���ڵ���
                var stackPanel = new StackPanel { Spacing = 16, MaxWidth = 450 };

                // �ɹ�ͼ��ͱ���
                var headerPanel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
                
                var successIcon = new SymbolIcon(Symbol.Accept)
                {
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                headerPanel.Children.Add(successIcon);

                headerPanel.Children.Add(new TextBlock 
                { 
                    Text = "��ʹ�õ��������°汾", 
                    Style = (Style)App.Current.Resources["SubtitleTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                stackPanel.Children.Add(headerPanel);

                // �汾��Ϣ�Ա� - ��ʾ��ǰ�汾��Զ�˰汾
                var versionComparePanel = new Grid();
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                versionComparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // ��ǰ�汾
                var currentVersionPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                currentVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = "��ǰ�汾", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                });
                currentVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = result.CurrentVersion ?? "δ֪", 
                    Style = (Style)App.Current.Resources["BodyStrongTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                Grid.SetColumn(currentVersionPanel, 0);
                versionComparePanel.Children.Add(currentVersionPanel);

                // ���ں�ͼ��
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

                // Զ�˰汾
                var remoteVersionPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                remoteVersionPanel.Children.Add(new TextBlock 
                { 
                    Text = result.IsPrerelease ? "Զ��Ԥ�����汾" : "Զ�����°汾", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                });
                
                var remoteVersionText = new TextBlock 
                { 
                    Text = result.LatestVersion ?? "δ֪", 
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

                // Ԥ�����汾˵��
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
                        Text = "������Ԥ�����汾���", 
                        Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    
                    stackPanel.Children.Add(prereleaseInfoPanel);
                }

                // ������Ϣ
                var debugInfoPanel = new StackPanel { Spacing = 4 };
                debugInfoPanel.Children.Add(new TextBlock 
                { 
                    Text = "������Ϣ", 
                    Style = (Style)App.Current.Resources["CaptionTextBlockStyle"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.5
                });

                var debugText = $"�������: {(result.IsPrerelease ? "����Ԥ�����汾" : "����ʽ�汾")}\n" +
                               $"Զ�˰汾: {result.LatestVersion ?? "δ��ȡ"}\n" +
                               $"�汾����: {(result.IsPrerelease ? "Ԥ�����汾" : "��ʽ�汾")}";

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
                    Title = "������",
                    Content = stackPanel,
                    CloseButtonText = "ȷ��",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowNoUpdateDialog error: {ex}");
                await ShowDialog("������", "����ǰʹ�õ��������°汾��");
            }
        }

        private async Task PerformUpdateWithProgress(string downloadUrl)
        {
            try
            {
                var progressDialog = new ContentDialog
                {
                    Title = "���ڸ���",
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

                var textBlock = new TextBlock { Text = "�������ظ���..." };

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
                        textBlock.Text = $"�������ظ���... {value}%";
                    });
                });

                var success = await UpdateService.DownloadAndInstallUpdateAsync(downloadUrl, progress);

                progressDialog.Hide();

                if (success)
                {
                    await ShowDialog("�������", "�����ѳɹ���װ��Ӧ�ó�������������");
                }
                else
                {
                    await ShowDialog("����ʧ��", "���°�װʧ�ܣ����Ժ����Ի��ֶ����ذ�װ��");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PerformUpdateWithProgress error: {ex}");
                await ShowDialog("����ʧ��", $"���¹����з�������: {ex.Message}");
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
                    CloseButtonText = "ȷ��",
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
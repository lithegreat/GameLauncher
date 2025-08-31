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

                        // �����Զ����¿���
                        if (AutoUpdateToggle != null)
                        {
                            AutoUpdateToggle.IsOn = settings.AutoUpdateEnabled;
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

                        // �����Զ�����״̬����/����Ƶ������
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

                    // ����Ƶ�����Ŀ�����
                    UpdateFrequencyPanelVisibility(isEnabled);

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
                    var message = $"�����°汾 {result.LatestVersion}����ǰ�汾 {result.CurrentVersion}��\n\n�Ƿ��������ز���װ���£�";
                    var updateDialog = new ContentDialog
                    {
                        Title = "�����°汾",
                        Content = message,
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
                else
                {
                    await ShowDialog("������", "����ǰʹ�õ��������°汾��");
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
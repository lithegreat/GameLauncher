using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using GameLauncher.Services;
using System;

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
                System.Diagnostics.Debug.WriteLine("SettingsPage: Page loaded, loading theme settings");
                LoadThemeSettings();
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
    }
}
using Microsoft.UI.Xaml;
using System;

namespace GameLauncher.Services
{
    public static class ThemeService
    {
        private const string ThemeSettingKey = "AppTheme";

        public static ElementTheme GetSavedTheme()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("ThemeService.GetSavedTheme started");
                
                var savedTheme = DataStorageService.ReadSetting(ThemeSettingKey, "Default");
                
                System.Diagnostics.Debug.WriteLine($"Retrieved saved theme string: {savedTheme}");

                var theme = savedTheme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    "Default" => ElementTheme.Default,
                    _ => ElementTheme.Default
                };
                
                System.Diagnostics.Debug.WriteLine($"Converted to ElementTheme: {theme}");
                return theme;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSavedTheme error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GetSavedTheme stack trace: {ex.StackTrace}");
                return ElementTheme.Default;
            }
        }

        public static void SaveTheme(ElementTheme theme)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ThemeService.SaveTheme started with theme: {theme}");
                
                var themeString = theme switch
                {
                    ElementTheme.Light => "Light",
                    ElementTheme.Dark => "Dark",
                    ElementTheme.Default => "Default",
                    _ => "Default"
                };

                System.Diagnostics.Debug.WriteLine($"Converting theme to string: {themeString}");

                DataStorageService.WriteSetting(ThemeSettingKey, themeString);
                
                System.Diagnostics.Debug.WriteLine($"Theme saved successfully: {themeString}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveTheme error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SaveTheme stack trace: {ex.StackTrace}");
            }
        }

        public static void ApplyTheme(ElementTheme theme)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ThemeService.ApplyTheme started with theme: {theme}");
                
                // Check if App.Current and MainWindow exist
                if (App.Current == null)
                {
                    System.Diagnostics.Debug.WriteLine("App.Current is null");
                    return;
                }

                if (App.Current.MainWindow == null)
                {
                    System.Diagnostics.Debug.WriteLine("App.Current.MainWindow is null");
                    return;
                }

                // Apply theme to the main window
                if (App.Current.MainWindow.Content is FrameworkElement rootElement)
                {
                    System.Diagnostics.Debug.WriteLine($"Applying theme {theme} to root element");
                    rootElement.RequestedTheme = theme;
                    System.Diagnostics.Debug.WriteLine("Theme applied to root element successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow.Content is not a FrameworkElement");
                }

                // Save the theme setting
                SaveTheme(theme);
                
                System.Diagnostics.Debug.WriteLine($"Theme applied and saved successfully: {theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyTheme error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"ApplyTheme stack trace: {ex.StackTrace}");
            }
        }

        public static string GetSavedThemeString()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("ThemeService.GetSavedThemeString started");
                
                var result = DataStorageService.ReadSetting(ThemeSettingKey, "Default");
                
                System.Diagnostics.Debug.WriteLine($"Retrieved theme string: {result}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSavedThemeString error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GetSavedThemeString stack trace: {ex.StackTrace}");
                return "Default";
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using GameLauncher.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GameLauncher
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? m_window;

        /// <summary>
        /// Gets the main window of the application.
        /// </summary>
        public Window? MainWindow => m_window;

        /// <summary>
        /// Gets the current App instance.
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            try
            {
                // Initialize COM wrappers to prevent COM registration issues
                WinRT.ComWrappersSupport.InitializeComWrappers();
                
                InitializeComponent();
                
                // Test data storage service on startup
                TestDataStorageService();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App initialization error: {ex.Message}");
                throw;
            }
        }

        private void TestDataStorageService()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Testing DataStorageService...");
                
                // Test the data storage service
                var testValue = DataStorageService.ReadSetting("TestKey", "DefaultValue");
                System.Diagnostics.Debug.WriteLine($"DataStorageService test read: {testValue}");
                
                DataStorageService.WriteSetting("TestKey", "TestValue");
                System.Diagnostics.Debug.WriteLine("DataStorageService test write completed");
                
                var verifyValue = DataStorageService.ReadSetting("TestKey", "DefaultValue");
                System.Diagnostics.Debug.WriteLine($"DataStorageService test verify: {verifyValue}");
                
                System.Diagnostics.Debug.WriteLine($"DataStorageService working in mode: {(DataStorageService.IsPackaged ? "Packaged" : "Unpackaged")}");
                System.Diagnostics.Debug.WriteLine($"DataStorageService local path: {DataStorageService.LocalDataPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataStorageService test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                m_window = new MainWindow();
                
                // Apply saved theme before activating the window
                ApplySavedTheme();
                
                m_window.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnLaunched error: {ex.Message}");
                // Create a basic error window if main window creation fails
                ShowErrorWindow(ex);
            }
        }

        private void ApplySavedTheme()
        {
            try
            {
                if (m_window == null) return;
                
                var savedTheme = ThemeService.GetSavedTheme();

                // Apply theme to the main window
                if (m_window.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = savedTheme;
                }

                System.Diagnostics.Debug.WriteLine($"Startup theme applied: {savedTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplySavedTheme error: {ex.Message}");
            }
        }

        private void ShowErrorWindow(Exception ex)
        {
            try
            {
                var errorWindow = new Window();
                var grid = new Grid();
                var textBlock = new TextBlock
                {
                    Text = $"Application failed to start:\n{ex.Message}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                grid.Children.Add(textBlock);
                errorWindow.Content = grid;
                errorWindow.Title = "GameLauncher Error";
                errorWindow.Activate();
            }
            catch
            {
                // If even error window fails, just exit
                Environment.Exit(1);
            }
        }
    }
}

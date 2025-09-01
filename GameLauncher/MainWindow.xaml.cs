using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using GameLauncher.Pages;
using GameLauncher.Services;

namespace GameLauncher
{
    public sealed partial class MainWindow : Window
    {
        private bool _dragRegionSet = false;
        private bool _initialized = false;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Activated += MainWindow_Activated;

            // Apply saved theme before showing the window
            ApplySavedTheme();
            TrySetBackdrop();
        }

        private void ApplySavedTheme()
        {
            try
            {
                var savedTheme = ThemeService.GetSavedTheme();

                // Apply theme to the content
                if (this.Content is FrameworkElement content)
                {
                    content.RequestedTheme = savedTheme;
                }

                System.Diagnostics.Debug.WriteLine($"MainWindow theme applied: {savedTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow ApplySavedTheme error: {ex.Message}");
            }
        }

        private void TrySetBackdrop()
        {
            try
            {
                // MicaBackdrop can throw when not supported; guard it
                var mica = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                this.SystemBackdrop = mica;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Backdrop init failed: {ex.Message}");
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (!_initialized && e.WindowActivationState != WindowActivationState.Deactivated)
            {
                _initialized = true;
                try
                {
                    SetupCustomTitleBar();
                    this.SizeChanged += MainWindow_SizeChanged;
                    
                    // Set up navigation after the window is activated
                    SetupNavigation();
                    
                    // Initialize auto update check
                    InitializeAutoUpdate();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MainWindow_Activated error: {ex.Message}");
                }
            }
        }

        private void InitializeAutoUpdate()
        {
            try
            {
                Debug.WriteLine("MainWindow: Initializing auto update");
                
                // 延迟启动自动更新检查，让应用完全加载完成
                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    try
                    {
                        UpdateService.StartAutoUpdateCheck();
                        Debug.WriteLine("MainWindow: Auto update check started");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"MainWindow: Auto update initialization error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeAutoUpdate error: {ex.Message}");
            }
        }

        private void SetupNavigation()
        {
            try
            {
                // Set up navigation
                nvSample.SelectionChanged += NavigationView_SelectionChanged;
                nvSample.Loaded += NavigationView_Loaded;
                
                // Navigate to GamesPage by default if not already navigated
                if (contentFrame.Content == null)
                {
                    contentFrame.Navigate(typeof(GamesPage));
                    nvSample.SelectedItem = nvSample.MenuItems[0];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetupNavigation error: {ex.Message}");
            }
        }

        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure we have a default page loaded
            if (contentFrame.Content == null)
            {
                contentFrame.Navigate(typeof(GamesPage));
                nvSample.SelectedItem = nvSample.MenuItems[0];
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            try
            {
                if (args.SelectedItemContainer != null)
                {
                    var navItemTag = args.SelectedItemContainer.Tag?.ToString();
                    if (!string.IsNullOrEmpty(navItemTag))
                    {
                        Debug.WriteLine($"Navigation selection changed to: {navItemTag}");
                        NavigateToPage(navItemTag);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigationView_SelectionChanged error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void NavigateToPage(string? navItemTag)
        {
            try
            {
                if (string.IsNullOrEmpty(navItemTag))
                {
                    Debug.WriteLine("Navigation tag is null or empty");
                    return;
                }

                Type? pageType = navItemTag switch
                {
                    "GamesPage" => typeof(GamesPage),
                    "SettingsPage" => typeof(SettingsPage),
                    _ => null
                };

                if (pageType == null)
                {
                    Debug.WriteLine($"Unknown navigation tag: {navItemTag}");
                    return;
                }

                if (contentFrame.CurrentSourcePageType != pageType)
                {
                    Debug.WriteLine($"Attempting to navigate to: {pageType.Name}");
                    
                    // Add extra protection for SettingsPage navigation
                    if (pageType == typeof(SettingsPage))
                    {
                        Debug.WriteLine("Navigating to SettingsPage with extra error handling");
                        try
                        {
                            contentFrame.Navigate(pageType);
                            Debug.WriteLine("SettingsPage navigation completed successfully");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"SettingsPage navigation failed: {ex.Message}");
                            Debug.WriteLine($"SettingsPage navigation stack trace: {ex.StackTrace}");
                            
                            // Try to stay on current page if navigation fails
                            return;
                        }
                    }
                    else
                    {
                        contentFrame.Navigate(pageType);
                    }
                    
                    Debug.WriteLine($"Navigated to: {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigateToPage error: {ex.Message}");
                Debug.WriteLine($"NavigateToPage stack trace: {ex.StackTrace}");
            }
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (!_dragRegionSet)
            {
                SetDragRegions();
                _dragRegionSet = true;
            }
        }

        private void SetupCustomTitleBar()
        {
            try
            {
                var appWindow = GetAppWindowForCurrentWindow();
                var titleBar = appWindow?.TitleBar;
                if (titleBar != null)
                {
                    titleBar.ExtendsContentIntoTitleBar = true;
                    titleBar.ButtonBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetupCustomTitleBar error: {ex.Message}");
            }
        }

        private AppWindow? GetAppWindowForCurrentWindow()
        {
            try
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
                return AppWindow.GetFromWindowId(wndId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAppWindowForCurrentWindow error: {ex.Message}");
                return null;
            }
        }

        private void SetDragRegions()
        {
            try
            {
                var appWindow = GetAppWindowForCurrentWindow();
                var titleBar = appWindow?.TitleBar;
                if (titleBar != null)
                {
                    var titleBarHeight = 32; // Changed from 48 to 32 to match Windows system buttons
                    var systemButtonsWidth = 138;
                    var windowWidth = (int)this.Bounds.Width;
                    
                    if (windowWidth > systemButtonsWidth)
                    {
                        var dragRect = new Windows.Graphics.RectInt32
                        {
                            X = 0,
                            Y = 0,
                            Width = windowWidth - systemButtonsWidth,
                            Height = titleBarHeight
                        };
                        
                        titleBar.SetDragRectangles(new[] { dragRect });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetDragRegions error: {ex.Message}");
            }
        }
    }
}

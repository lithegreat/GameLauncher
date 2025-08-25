using System;
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

namespace GameLauncher
{
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<CustomDataObject> Items { get; } = new ObservableCollection<CustomDataObject>();
        
        private const string GamesDataFileName = "games.json";
        private bool _dragRegionSet = false;
        private bool _initialized = false;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Activated += MainWindow_Activated;

            TrySetBackdrop();
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

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (!_initialized && e.WindowActivationState != WindowActivationState.Deactivated)
            {
                _initialized = true;
                try
                {
                    SetupCustomTitleBar();
                    this.SizeChanged += MainWindow_SizeChanged;
                    await LoadGamesData();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MainWindow_Activated error: {ex.Message}");
                }
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
                    var titleBarHeight = 48;
                    var addButtonWidth = 120;
                    var systemButtonsWidth = 138;
                    var reservedWidth = addButtonWidth + systemButtonsWidth;
                    var windowWidth = (int)this.Bounds.Width;
                    
                    if (windowWidth > reservedWidth)
                    {
                        var dragRect = new Windows.Graphics.RectInt32
                        {
                            X = 0,
                            Y = 0,
                            Width = windowWidth - reservedWidth,
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

        private Microsoft.UI.Xaml.XamlRoot? TryGetXamlRoot()
        {
            return (this.Content as FrameworkElement)?.XamlRoot;
        }

        private async void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowAddGameDialog();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"添加游戏时出错: {ex.Message}");
            }
        }

        private async Task ShowAddGameDialog()
        {
            var gameNameBox = new TextBox()
            {
                PlaceholderText = "输入游戏名称",
                Margin = new Thickness(0, 0, 0, 10)
            };

            var pathBox = new TextBox()
            {
                PlaceholderText = "游戏可执行文件路径",
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 5, 0)
            };

            var browseButton = new Button()
            {
                Content = "浏览..."
            };

            browseButton.Click += async (s, args) =>
            {
                try
                {
                    var picker = new FileOpenPicker();
                    picker.SuggestedStartLocation = PickerLocationId.Desktop;
                    picker.FileTypeFilter.Add(".exe");

                    var hWnd = WindowNative.GetWindowHandle(this);
                    InitializeWithWindow.Initialize(picker, hWnd);

                    var file = await picker.PickSingleFileAsync();
                    if (file != null)
                    {
                        pathBox.Text = file.Path;
                        if (string.IsNullOrWhiteSpace(gameNameBox.Text))
                        {
                            gameNameBox.Text = Path.GetFileNameWithoutExtension(file.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"File picker error: {ex.Message}");
                }
            };

            var pathPanel = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            pathPanel.Children.Add(pathBox);
            pathPanel.Children.Add(browseButton);

            var contentPanel = new StackPanel();
            contentPanel.Children.Add(new TextBlock() { Text = "游戏名称:", Margin = new Thickness(0, 0, 0, 5) });
            contentPanel.Children.Add(gameNameBox);
            contentPanel.Children.Add(new TextBlock() { Text = "可执行文件路径:", Margin = new Thickness(0, 10, 0, 5) });
            contentPanel.Children.Add(pathPanel);

            var xamlRoot = TryGetXamlRoot();
            if (xamlRoot is null)
            {
                await ShowErrorDialog("无法显示对话框，因UI尚未准备好。");
                return;
            }

            var dialog = new ContentDialog()
            {
                Title = "添加游戏",
                Content = contentPanel,
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消",
                XamlRoot = xamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(gameNameBox.Text))
                {
                    await ShowErrorDialog("请输入游戏名称");
                    return;
                }

                if (string.IsNullOrWhiteSpace(pathBox.Text))
                {
                    await ShowErrorDialog("请选择游戏可执行文件");
                    return;
                }

                if (!File.Exists(pathBox.Text))
                {
                    await ShowErrorDialog("指定的文件不存在");
                    return;
                }

                var iconImage = await IconExtractor.ExtractIconAsync(pathBox.Text);

                var gameData = new CustomDataObject
                {
                    Title = gameNameBox.Text.Trim(),
                    ExecutablePath = pathBox.Text.Trim(),
                    IconImage = iconImage
                };

                Items.Add(gameData);
                await SaveGamesData();
            }
        }

        private void ContentGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle selection changed if needed
        }

        private async void ContentGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (e.ClickedItem is CustomDataObject game)
                {
                    await LaunchGame(game);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"启动游戏时出错: {ex.Message}");
            }
        }

        private async Task LaunchGame(CustomDataObject game)
        {
            try
            {
                if (game == null)
                {
                    await ShowErrorDialog("游戏数据无效");
                    return;
                }

                if (string.IsNullOrEmpty(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
                {
                    await ShowErrorDialog($"游戏文件不存在: {game.ExecutablePath}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"启动游戏失败: {ex.Message}");
            }
        }

        private async Task LoadGamesData()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var file = await localFolder.TryGetItemAsync(GamesDataFileName) as StorageFile;
                
                if (file != null)
                {
                    var json = await FileIO.ReadTextAsync(file);
                    
                    if (!string.IsNullOrEmpty(json))
                    {
                        var options = new JsonSerializerOptions 
                        { 
                            WriteIndented = true,
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var gameDataList = JsonSerializer.Deserialize<GameDataJson[]>(json, options);
                        
                        if (gameDataList != null)
                        {
                            Items.Clear();
                            foreach (var gameJson in gameDataList)
                            {
                                if (gameJson != null && !string.IsNullOrEmpty(gameJson.Title) && !string.IsNullOrEmpty(gameJson.ExecutablePath))
                                {
                                    var iconImage = await IconExtractor.ExtractIconAsync(gameJson.ExecutablePath);
                                    
                                    var game = new CustomDataObject
                                    {
                                        Title = gameJson.Title,
                                        ExecutablePath = gameJson.ExecutablePath,
                                        IconImage = iconImage
                                    };
                                    Items.Add(game);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadGamesData error: {ex.Message}");
            }
        }

        private async Task SaveGamesData()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var file = await localFolder.CreateFileAsync(GamesDataFileName, CreationCollisionOption.ReplaceExisting);
                
                var gameDataList = Items.Where(item => item != null && !string.IsNullOrEmpty(item.Title))
                                       .Select(item => new GameDataJson
                                       {
                                           Title = item.Title,
                                           ExecutablePath = item.ExecutablePath
                                       }).ToList();
                
                var json = JsonSerializer.Serialize(gameDataList, new JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(file, json);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"保存游戏数据失败: {ex.Message}");
            }
        }

        private async Task ShowErrorDialog(string message)
        {
            try
            {
                var xamlRoot = TryGetXamlRoot();
                if (xamlRoot != null)
                {
                    var dialog = new ContentDialog()
                    {
                        Title = "错误",
                        Content = message ?? "发生未知错误",
                        CloseButtonText = "确定",
                        XamlRoot = xamlRoot
                    };

                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowErrorDialog error: {ex.Message}");
            }
        }
    }

    public class GameDataJson
    {
        public string Title { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
    }
}

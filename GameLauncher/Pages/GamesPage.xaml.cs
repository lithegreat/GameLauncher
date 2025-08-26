using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Input;

namespace GameLauncher.Pages
{
    public sealed partial class GamesPage : Page
    {
        public ObservableCollection<CustomDataObject> Items { get; } = new ObservableCollection<CustomDataObject>();
        private const string GamesDataFileName = "games.json";
        private bool _isDeleteMode = false;
        private CustomDataObject? _contextMenuGame = null;

        public GamesPage()
        {
            this.InitializeComponent();
            this.Loaded += GamesPage_Loaded;
        }

        private async void GamesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadGamesData();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        #region Context Menu Event Handlers

        private void ContentGridView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var gridView = sender as GridView;
            var tappedElement = e.OriginalSource as FrameworkElement;
            
            // Find the data context (game item) from the tapped element
            while (tappedElement != null && tappedElement.DataContext == null)
            {
                tappedElement = tappedElement.Parent as FrameworkElement;
            }
            
            if (tappedElement?.DataContext is CustomDataObject game)
            {
                _contextMenuGame = game;
                // Context menu will show automatically due to ContextFlyout in XAML
            }
        }

        private async void OpenGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuGame != null)
            {
                await LaunchGame(_contextMenuGame);
            }
        }

        private async void DeleteGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuGame != null)
            {
                await DeleteSingleGame(_contextMenuGame);
            }
        }

        private async void OpenGameDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuGame != null)
            {
                await OpenGameDirectory(_contextMenuGame);
            }
        }

        #endregion

        #region New Methods for Context Menu Actions

        private async Task DeleteSingleGame(CustomDataObject game)
        {
            try
            {
                var confirmDialog = new ContentDialog()
                {
                    Title = "确认删除",
                    Content = $"确定要删除游戏 \"{game.Title}\" 吗？此操作无法撤销。",
                    PrimaryButtonText = "删除",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    Items.Remove(game);
                    await SaveGamesData();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"删除游戏时错误: {ex.Message}");
            }
        }

        private async Task OpenGameDirectory(CustomDataObject game)
        {
            try
            {
                if (string.IsNullOrEmpty(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
                {
                    await ShowErrorDialog("游戏文件不存在，无法打开目录");
                    return;
                }

                var directory = Path.GetDirectoryName(game.ExecutablePath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{directory}\"",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                else
                {
                    await ShowErrorDialog("游戏目录不存在");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"打开游戏目录时错误: {ex.Message}");
            }
        }

        #endregion

        #region Existing Button Event Handlers

        private async void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowAddGameDialog();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"添加游戏时错误: {ex.Message}");
            }
        }

        private void DeleteModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDeleteMode = true;
            UpdateDeleteModeUI();
        }

        private void CancelDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            _isDeleteMode = false;
            UpdateDeleteModeUI();
        }

        private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = ContentGridView.SelectedItems.Cast<CustomDataObject>().ToList();
                
                if (selectedItems.Count == 0)
                {
                    await ShowErrorDialog("请选择要删除的游戏");
                    return;
                }

                var confirmDialog = new ContentDialog()
                {
                    Title = "确认删除",
                    Content = $"确定要删除 {selectedItems.Count} 个游戏吗？此操作无法撤销。",
                    PrimaryButtonText = "删除",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    // Remove selected items from the collection
                    foreach (var item in selectedItems)
                    {
                        Items.Remove(item);
                    }
                    
                    // Save the updated data
                    await SaveGamesData();
                    
                    // Exit delete mode
                    _isDeleteMode = false;
                    UpdateDeleteModeUI();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"删除游戏时错误: {ex.Message}");
            }
        }

        #endregion

        private void UpdateDeleteModeUI()
        {
            if (_isDeleteMode)
            {
                // Enter delete mode
                ContentGridView.SelectionMode = ListViewSelectionMode.Multiple;
                ContentGridView.IsItemClickEnabled = false;
                
                // Show delete mode buttons
                DeleteSelectedButton.Visibility = Visibility.Visible;
                CancelDeleteButton.Visibility = Visibility.Visible;
                
                // Hide normal mode buttons
                DeleteModeButton.Visibility = Visibility.Collapsed;
                AddGameButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Exit delete mode
                ContentGridView.SelectionMode = ListViewSelectionMode.None;
                ContentGridView.IsItemClickEnabled = true;
                ContentGridView.SelectedItems.Clear();
                
                // Hide delete mode buttons
                DeleteSelectedButton.Visibility = Visibility.Collapsed;
                CancelDeleteButton.Visibility = Visibility.Collapsed;
                
                // Show normal mode buttons
                DeleteModeButton.Visibility = Visibility.Visible;
                AddGameButton.Visibility = Visibility.Visible;
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

                    var mainWindow = App.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        var hWnd = WindowNative.GetWindowHandle(mainWindow);
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

            var dialog = new ContentDialog()
            {
                Title = "添加游戏",
                Content = contentPanel,
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消",
                XamlRoot = this.XamlRoot
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
            // Update delete button state based on selection
            if (_isDeleteMode)
            {
                DeleteSelectedButton.IsEnabled = ContentGridView.SelectedItems.Count > 0;
            }
        }

        private async void ContentGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                // Only launch game if not in delete mode
                if (!_isDeleteMode && e.ClickedItem is CustomDataObject game)
                {
                    await LaunchGame(game);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"启动游戏时错误: {ex.Message}");
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
                var dialog = new ContentDialog()
                {
                    Title = "错误",
                    Content = message ?? "发生未知错误",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
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
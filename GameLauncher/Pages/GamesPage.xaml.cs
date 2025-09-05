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
using GameLauncher.Services;
using GameLauncher.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Data;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ellipse = Microsoft.UI.Xaml.Shapes.Ellipse;

namespace GameLauncher.Pages
{
    public sealed partial class GamesPage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<CustomDataObject> Items { get; } = new ObservableCollection<CustomDataObject>();
        public ObservableCollection<CustomDataObject> FilteredItems { get; } = new ObservableCollection<CustomDataObject>();
        public ObservableCollection<GameCategory> Categories => CategoryService.Instance.Categories;
        
        private const string GamesDataFileName = "games.json";
        private bool _isDeleteMode = false;
        private CustomDataObject? _contextMenuGame = null;
        private GameCategory? _selectedCategory = null;
        private CustomDataObject? _selectedGame = null;

        public CustomDataObject? SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (_selectedGame != value)
                {
                    _selectedGame = value;
                    OnPropertyChanged();
                    UpdateGameDetails();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public GamesPage()
        {
            this.InitializeComponent();
            this.Loaded += GamesPage_Loaded;
            
            // 订阅分类删除事件
            CategoryService.Instance.CategoryDeleted += OnCategoryDeleted;
        }

        private async void OnCategoryDeleted(string deletedCategoryId)
        {
            try
            {
                // 将被删除分类下的所有游戏移动到"未分类"
                var affectedGames = Items.Where(g => g.CategoryId == deletedCategoryId).ToList();
                
                foreach (var game in affectedGames)
                {
                    game.CategoryId = "uncategorized";
                    game.Category = "未分类";
                    // CategoryColor 会自动通过 CategoryId 的变化来更新
                }
                
                if (affectedGames.Any())
                {
                    // 保存更新后的游戏数据
                    await SaveGamesData();
                    
                    // 刷新UI
                    ApplyCategoryFilter();
                    UpdateCategoryGameCounts();
                    
                    // 如果当前选中的游戏受到影响，更新详情显示
                    if (SelectedGame != null && affectedGames.Contains(SelectedGame))
                    {
                        UpdateGameDetails();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理分类删除事件时出错: {ex.Message}");
            }
        }

        private async void GamesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CategoryService.Instance.LoadCategoriesAsync();
            await LoadGamesData();
            
            // 默认选择"全部游戏"
            _selectedCategory = Categories.FirstOrDefault(c => c.Id == "all");
            ApplyCategoryFilter();
            UpdateCategoryGameCounts();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // 页面离开时保存当前的游戏顺序
            await SaveCurrentGameOrder();
        }

        #region Game Selection and Details

        private void GamesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                if (e.ClickedItem is CustomDataObject game)
                {
                    SelectedGame = game;
                    GamesListView.SelectedItem = game;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GamesListView_ItemClick error: {ex.Message}");
            }
        }

        private void GamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isDeleteMode)
                {
                    // Update delete button state based on selection in delete mode
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            var selectedCount = GamesListView.SelectedItems?.Count ?? 0;
                            DeleteSelectedButton.IsEnabled = selectedCount > 0;
                            System.Diagnostics.Debug.WriteLine($"选择变更: {selectedCount} 个项目被选中");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"更新删除按钮状态时异常: {ex.Message}");
                        }
                    });
                }
                else
                {
                    // Normal selection mode - update selected game
                    if (sender is ListView listView && listView.SelectedItem is CustomDataObject game)
                    {
                        SelectedGame = game;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GamesListView_SelectionChanged error: {ex.Message}");
            }
        }

        private void GamesListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("右键点击事件触发");
                
                var listView = sender as ListView;
                var tappedElement = e.OriginalSource as FrameworkElement;
                
                // Find the data context (game item) from the tapped element
                while (tappedElement != null && tappedElement.DataContext == null)
                {
                    tappedElement = tappedElement.Parent as FrameworkElement;
                }
                
                if (tappedElement?.DataContext is CustomDataObject game)
                {
                    _contextMenuGame = game;
                    System.Diagnostics.Debug.WriteLine($"设置上下文菜单游戏: {game.Title}");
                    
                    // 根据游戏类型动态显示菜单项
                    UpdateContextMenuForGame(game);
                    
                    // Context menu will show automatically due to ContextFlyout in XAML
                }
                else
                {
                    _contextMenuGame = null;
                    System.Diagnostics.Debug.WriteLine("未找到游戏数据上下文");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"右键点击处理异常: {ex.Message}");
                _contextMenuGame = null;
            }
        }

        private void UpdateContextMenuForGame(CustomDataObject game)
        {
            try
            {
                // 获取上下文菜单
                if (Resources["GameContextMenu"] is MenuFlyout contextMenu)
                {
                    // 查找 Steam 相关的菜单项
                    var steamSeparator = contextMenu.Items.OfType<MenuFlyoutSeparator>().FirstOrDefault(x => x.Name == "SteamSeparator");
                    var openInSteamItem = contextMenu.Items.OfType<MenuFlyoutItem>().FirstOrDefault(x => x.Name == "OpenInSteamMenuItem");

                    if (steamSeparator != null && openInSteamItem != null)
                    {
                        // 根据是否为 Steam 游戏显示/隐藏菜单项
                        steamSeparator.Visibility = game.IsSteamGame ? Visibility.Visible : Visibility.Collapsed;
                        openInSteamItem.Visibility = game.IsSteamGame ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新上下文菜单时异常: {ex.Message}");
            }
        }

        private async void UpdateGameDetails()
        {
            try
            {
                if (SelectedGame == null)
                {
                    // Show empty state
                    GameDetailsPanel.Visibility = Visibility.Collapsed;
                    EmptyStatePanel.Visibility = Visibility.Visible;
                    return;
                }

                // Hide empty state and show details
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                GameDetailsPanel.Visibility = Visibility.Visible;

                // Update game header
                GameIcon.Source = SelectedGame.IconImage;
                GameTitle.Text = SelectedGame.Title;
                GameCategory.Text = SelectedGame.Category;
                
                // Update category color indicator
                if (!string.IsNullOrEmpty(SelectedGame.CategoryColor))
                {
                    var converter = new ColorStringToColorConverter();
                    var color = (Windows.UI.Color)converter.Convert(SelectedGame.CategoryColor, typeof(Windows.UI.Color), null, "");
                    CategoryColorIndicator.Fill = new SolidColorBrush(color);
                }

                // Update game type panel
                UpdateGameTypePanel();

                // Update game stats
                UpdateGameStats();

                // Update file information
                await UpdateFileInformation();

                // Update actions panel
                UpdateActionsPanel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateGameDetails error: {ex.Message}");
            }
        }

        private void UpdateGameTypePanel()
        {
            try
            {
                GameTypePanel.Children.Clear();

                if (SelectedGame == null) return;

                if (SelectedGame.IsSteamGame)
                {
                    var steamIcon = new FontIcon { Glyph = "\uE968", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) };
                    var steamText = new TextBlock { Text = "Steam 游戏", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
                    var steamPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    steamPanel.Children.Add(steamIcon);
                    steamPanel.Children.Add(steamText);
                    GameTypePanel.Children.Add(steamPanel);

                    if (!string.IsNullOrEmpty(SelectedGame.SteamAppId))
                    {
                        var appIdText = new TextBlock 
                        { 
                            Text = $"App ID: {SelectedGame.SteamAppId}", 
                            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                        };
                        GameTypePanel.Children.Add(appIdText);
                    }
                }
                else if (SelectedGame.IsXboxGame)
                {
                    var xboxIcon = new FontIcon { Glyph = "\uE990", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) };
                    var xboxText = new TextBlock { Text = "Xbox 游戏", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
                    var xboxPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    xboxPanel.Children.Add(xboxIcon);
                    xboxPanel.Children.Add(xboxText);
                    GameTypePanel.Children.Add(xboxPanel);
                }
                else
                {
                    var localIcon = new FontIcon { Glyph = "\uE8B7", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) };
                    var localText = new TextBlock { Text = "本地游戏", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
                    var localPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    localPanel.Children.Add(localIcon);
                    localPanel.Children.Add(localText);
                    GameTypePanel.Children.Add(localPanel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateGameTypePanel error: {ex.Message}");
            }
        }

        private void UpdateGameStats()
        {
            try
            {
                if (SelectedGame == null) return;

                // Format playtime
                if (SelectedGame.Playtime > 0)
                {
                    var hours = SelectedGame.Playtime / 60;
                    var minutes = SelectedGame.Playtime % 60;
                    PlaytimeText.Text = $"游戏时间: {hours:N0} 小时 {minutes} 分钟";
                }
                else
                {
                    PlaytimeText.Text = "游戏时间: 暂无数据";
                }

                // Format last activity
                if (SelectedGame.LastActivity.HasValue)
                {
                    LastActivityText.Text = $"最后游玩: {SelectedGame.LastActivity.Value:yyyy年MM月dd日}";
                }
                else
                {
                    LastActivityText.Text = "最后游玩: 暂无数据";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateGameStats error: {ex.Message}");
            }
        }

        private async Task UpdateFileInformation()
        {
            try
            {
                if (SelectedGame == null) return;

                ExecutablePathText.Text = SelectedGame.ExecutablePath;

                if (!string.IsNullOrEmpty(SelectedGame.ExecutablePath) && File.Exists(SelectedGame.ExecutablePath))
                {
                    var fileInfo = new FileInfo(SelectedGame.ExecutablePath);
                    
                    // File size
                    var sizeInMB = fileInfo.Length / (1024.0 * 1024.0);
                    if (sizeInMB >= 1024)
                    {
                        FileSizeText.Text = $"{sizeInMB / 1024.0:F2} GB";
                    }
                    else
                    {
                        FileSizeText.Text = $"{sizeInMB:F2} MB";
                    }

                    // Last modified
                    LastModifiedText.Text = fileInfo.LastWriteTime.ToString("yyyy年MM月dd日 HH:mm");
                }
                else
                {
                    FileSizeText.Text = "文件不存在";
                    LastModifiedText.Text = "-";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateFileInformation error: {ex.Message}");
                FileSizeText.Text = "无法获取";
                LastModifiedText.Text = "无法获取";
            }
        }

        private void UpdateActionsPanel()
        {
            try
            {
                ActionsPanel.Children.Clear();

                if (SelectedGame == null) return;

                // Set Category button
                var setCategoryButton = new Button
                {
                    Content = "设置分类",
                    Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                setCategoryButton.Click += async (s, e) => await ShowSetCategoryDialog(SelectedGame);
                ActionsPanel.Children.Add(setCategoryButton);

                // Steam specific actions
                if (SelectedGame.IsSteamGame && !string.IsNullOrEmpty(SelectedGame.SteamAppId))
                {
                    var openInSteamButton = new Button
                    {
                        Content = "在 Steam 中打开",
                        Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    openInSteamButton.Click += async (s, e) => await OpenInSteam(SelectedGame);
                    ActionsPanel.Children.Add(openInSteamButton);
                }

                // Delete game button
                var deleteButton = new Button
                {
                    Content = "删除游戏",
                    Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
                };
                deleteButton.Click += async (s, e) => await DeleteSingleGame(SelectedGame);
                ActionsPanel.Children.Add(deleteButton);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateActionsPanel error: {ex.Message}");
            }
        }

        private async void LaunchGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedGame != null)
                {
                    await LaunchGame(SelectedGame);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"启动游戏时错误: {ex.Message}");
            }
        }

        private async void OpenDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedGame != null)
                {
                    await OpenGameDirectory(SelectedGame);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"打开游戏目录时错误: {ex.Message}");
            }
        }

        private async Task OpenInSteam(CustomDataObject game)
        {
            try
            {
                if (game.IsSteamGame && !string.IsNullOrEmpty(game.SteamAppId))
                {
                    var steamStoreUrl = $"https://store.steampowered.com/app/{game.SteamAppId}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = steamStoreUrl,
                        UseShellExecute = true
                    });
                }
                else
                {
                    await ShowErrorDialog("该游戏不是 Steam 游戏");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"在 Steam 中打开游戏时错误: {ex.Message}");
            }
        }

        #endregion

        #region Category and Filter Management

        private void ApplyCategoryFilter()
        {
            FilteredItems.Clear();
            
            if (_selectedCategory == null)
            {
                // 如果没有选择分类，显示所有游戏
                foreach (var item in Items)
                {
                    FilteredItems.Add(item);
                }
            }
            else if (_selectedCategory.Id == "all")
            {
                // 显示所有游戏
                foreach (var item in Items)
                {
                    FilteredItems.Add(item);
                }
            }
            else if (_selectedCategory.Id == "uncategorized")
            {
                // 显示未分类的游戏
                foreach (var item in Items.Where(g => string.IsNullOrEmpty(g.CategoryId) || g.CategoryId == "uncategorized"))
                {
                    FilteredItems.Add(item);
                }
            }
            else
            {
                // 显示特定分类的游戏
                foreach (var item in Items.Where(g => g.CategoryId == _selectedCategory.Id))
                {
                    FilteredItems.Add(item);
                }
            }
        }

        private void UpdateCategoryGameCounts()
        {
            CategoryService.Instance.UpdateCategoryGameCounts(Items);
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox && comboBox.SelectedItem is GameCategory selectedCategory)
                {
                    _selectedCategory = selectedCategory;
                    ApplyCategoryFilter();
                    
                    // Clear selection when changing categories
                    SelectedGame = null;
                    GamesListView.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"分类选择变更时错误: {ex.Message}");
            }
        }

        #endregion

        #region Context Menu Event Handlers

        private async void OpenGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    System.Diagnostics.Debug.WriteLine($"打开游戏: {_contextMenuGame.Title}");
                    await LaunchGame(_contextMenuGame);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("上下文菜单游戏为空，无法打开");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开游戏菜单项异常: {ex.Message}");
                await ShowErrorDialog($"打开游戏时错误: {ex.Message}");
            }
        }

        private async void DeleteGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    System.Diagnostics.Debug.WriteLine($"删除游戏菜单项点击: {_contextMenuGame.Title}");
                    await DeleteSingleGame(_contextMenuGame);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("上下文菜单游戏为空，无法删除");
                }
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除游戏菜单项异常: {ex.Message}");
                await ShowErrorDialog($"删除游戏时错误: {ex.Message}");
            }
        }

        private async void OpenGameDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    System.Diagnostics.Debug.WriteLine($"打开游戏目录: {_contextMenuGame.Title}");
                    await OpenGameDirectory(_contextMenuGame);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("上下文菜单游戏为空，无法打开目录");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开游戏目录菜单项异常: {ex.Message}");
                await ShowErrorDialog($"打开游戏目录时错误: {ex.Message}");
            }
        }

        private async void OpenInSteamMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null && _contextMenuGame.IsSteamGame)
                {
                    System.Diagnostics.Debug.WriteLine($"在 Steam 中打开游戏: {_contextMenuGame.Title}");
                    
                    if (!string.IsNullOrEmpty(_contextMenuGame.SteamAppId))
                    {
                        // 使用 Steam 商店页面 URL
                        var steamStoreUrl = $"https://store.steampowered.com/app/{_contextMenuGame.SteamAppId}";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = steamStoreUrl,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        await ShowErrorDialog("无法获取游戏的 Steam AppID");
                    }
                }
                else
                {
                    await ShowErrorDialog("该游戏不是 Steam 游戏");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"在 Steam 中打开游戏异常: {ex.Message}");
                await ShowErrorDialog($"在 Steam 中打开游戏时错误: {ex.Message}");
            }
        }

        private async void SetCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    await ShowSetCategoryDialog(_contextMenuGame);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"设置分类时错误: {ex.Message}");
            }
        }

        #endregion

        #region Game Management Methods

        private async Task DeleteSingleGame(CustomDataObject game)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"开始删除游戏: {game.Title}");
                
                var confirmDialog = new ContentDialog()
                {
                    Title = "确认删除",
                    Content = $"确定要删除游戏 \"{game.Title}\" 吗？此操作无法撤销。",
                    PrimaryButtonText = "删除",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                System.Diagnostics.Debug.WriteLine("显示确认对话框");
                var result = await confirmDialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    System.Diagnostics.Debug.WriteLine("用户确认删除，开始执行删除操作");
                    
                    // Clear selected game if it's being deleted
                    if (SelectedGame == game)
                    {
                        SelectedGame = null;
                    }
                    
                    // 确保游戏在集合中存在
                    if (Items.Contains(game))
                    {
                        Items.Remove(game);
                        System.Diagnostics.Debug.WriteLine("游戏已从集合中移除");
                        
                        // 保存更新后的数据
                        await SaveGamesData();
                        
                        // 确保UI立即更新
                        ApplyCategoryFilter();
                        UpdateCategoryGameCounts();
                        
                        System.Diagnostics.Debug.WriteLine("游戏数据保存完成");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("警告：游戏不在集合中");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("用户取消删除操作");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除游戏时发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                await ShowErrorDialog($"删除游戏时出错: {ex.Message}");
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

        private async Task LaunchGame(CustomDataObject game)
        {
            try
            {
                if (game == null)
                {
                    await ShowErrorDialog("游戏数据无效");
                    return;
                }

                // 如果是 Steam 游戏，优先使用 Steam 协议启动
                if (game.IsSteamGame && !string.IsNullOrEmpty(game.SteamAppId))
                {
                    System.Diagnostics.Debug.WriteLine($"通过 Steam 启动游戏: {game.Title} (AppID: {game.SteamAppId})");
                    
                    if (SteamService.LaunchSteamGame(game.SteamAppId))
                    {
                        return; // Steam 启动成功
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Steam 启动失败，尝试直接运行可执行文件");
                    }
                }

                // 如果是 Xbox 游戏，优先使用 Xbox 协议启动
                if (game.IsXboxGame && !string.IsNullOrEmpty(game.XboxPackageFamilyName))
                {
                    System.Diagnostics.Debug.WriteLine($"通过 Xbox 启动游戏: {game.Title} (Package: {game.XboxPackageFamilyName})");
                    
                    if (XboxService.LaunchXboxGame(game.XboxPackageFamilyName))
                    {
                        return; // Xbox 启动成功
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Xbox 启动失败，尝试直接运行可执行文件");
                        
                        // 尝试通过可执行文件启动 Xbox 游戏
                        if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                        {
                            if (XboxService.LaunchXboxGameByExecutable(game.ExecutablePath))
                            {
                                return; // 通过可执行文件启动成功
                            }
                        }
                    }
                }

                // 直接运行可执行文件（适用于 Steam 游戏或 Steam 启动失败时的备选方案）
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
                System.Diagnostics.Debug.WriteLine($"直接启动游戏: {game.Title}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"启动游戏失败: {ex.Message}");
            }
        }

        #endregion

        #region Button Event Handlers

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

        private async void CleanDuplicateGamesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await CleanDuplicateGames();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"清理重复游戏时错误: {ex.Message}");
            }
        }

        private void DeleteModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDeleteMode = true;
            UpdateDeleteModeUI();
        }

        private void CancelDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("取消删除模式");
                
                // 确保在 UI 线程上执行
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        _isDeleteMode = false;
                        UpdateDeleteModeUI();
                        System.Diagnostics.Debug.WriteLine("删除模式已取消");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"取消删除模式时UI异常: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取消删除按钮异常: {ex.Message}");
                
                // 强制退出删除模式
                _isDeleteMode = false;
            }
        }

        private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始批量删除游戏");
                
                // 安全地获取选中的项目
                var selectedItems = new List<CustomDataObject>();
                
                // 确保在 UI 线程中执行
                var tcs = new TaskCompletionSource<bool>();
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        foreach (var item in GamesListView.SelectedItems)
                        {
                            if (item is CustomDataObject game)
                            {
                                selectedItems.Add(game);
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"选中了 {selectedItems.Count} 个游戏");
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"获取选中项目时异常: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                
                await tcs.Task;
                
                if (selectedItems.Count == 0)
                {
                    await ShowErrorDialog("请选择要删除的游戏");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("显示批量删除确认对话框");
                
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
                    System.Diagnostics.Debug.WriteLine("用户确认批量删除，开始执行删除操作");
                    
                    // Clear selected game if it's being deleted
                    if (SelectedGame != null && selectedItems.Contains(SelectedGame))
                    {
                        SelectedGame = null;
                    }
                    
                    // 在 UI 线程上安全地删除项目
                    var deleteTcs = new TaskCompletionSource<bool>();
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            // Remove selected items from the collection
                            foreach (var item in selectedItems)
                            {
                                if (Items.Contains(item))
                                {
                                    Items.Remove(item);
                                    System.Diagnostics.Debug.WriteLine($"删除游戏: {item.Title}");
                                }
                            }

                            System.Diagnostics.Debug.WriteLine("批量删除完成，退出删除模式");
                            
                            // Exit delete mode
                            _isDeleteMode = false;
                            UpdateDeleteModeUI();
                            
                            deleteTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"UI线程删除操作异常: {ex.Message}");
                            deleteTcs.SetException(ex);
                        }
                    });
                    
                    await deleteTcs.Task;
                    
                    // Save the updated data
                    await SaveGamesData();
                    
                    // 确保UI立即更新
                    ApplyCategoryFilter();
                    UpdateCategoryGameCounts();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("用户取消批量删除操作");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"批量删除游戏时发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                
                // 确保退出删除模式
                try
                {
                    _isDeleteMode = false;
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            UpdateDeleteModeUI();
                        }
                        catch (Exception updateEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"更新UI异常: {updateEx.Message}");
                        }
                    });
                }
                catch
                {
                    // 忽略嵌套异常
                }
                
                await ShowErrorDialog($"删除游戏时出错: {ex.Message}");
            }
        }

        private async void ManageCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowManageCategoriesDialog();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"管理分类时错误: {ex.Message}");
            }
        }

        private async void ImportSteamGamesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ImportSteamGames();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"导入Steam游戏时错误: {ex.Message}");
            }
        }

        private async void ImportXboxGamesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ImportXboxGames();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"导入Xbox游戏时错误: {ex.Message}");
            }
        }

        #endregion

        #region Delete Mode UI

        private void UpdateDeleteModeUI()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"更新删除模式UI: _isDeleteMode = {_isDeleteMode}");
                
                if (_isDeleteMode)
                {
                    // Enter delete mode
                    GamesListView.SelectionMode = ListViewSelectionMode.Multiple;
                    GamesListView.IsItemClickEnabled = false;
                    
                    // Show delete mode buttons
                    DeleteSelectedButton.Visibility = Visibility.Visible;
                    CancelDeleteButton.Visibility = Visibility.Visible;
                    
                    // Hide normal mode buttons
                    DeleteModeButton.Visibility = Visibility.Collapsed;
                    CleanDuplicateGamesButton.Visibility = Visibility.Collapsed;
                    ImportGamesDropDownButton.Visibility = Visibility.Collapsed;
                    AddGameButton.Visibility = Visibility.Collapsed;
                    
                    // Initialize delete button state
                    DeleteSelectedButton.IsEnabled = false;
                    
                    System.Diagnostics.Debug.WriteLine("进入删除模式");
                }
                else
                {
                    // Exit delete mode - clear selections first
                    try
                    {
                        if (GamesListView.SelectedItems != null)
                        {
                            GamesListView.SelectedItems.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"清理选择时异常: {ex.Message}");
                    }
                    
                    GamesListView.SelectionMode = ListViewSelectionMode.Single;
                    GamesListView.IsItemClickEnabled = true;
                    
                    // Hide delete mode buttons
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    
                    // Show normal mode buttons
                    DeleteModeButton.Visibility = Visibility.Visible;
                    CleanDuplicateGamesButton.Visibility = Visibility.Visible;
                    ImportGamesDropDownButton.Visibility = Visibility.Visible;
                    AddGameButton.Visibility = Visibility.Visible;
                    
                    System.Diagnostics.Debug.WriteLine("退出删除模式");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateDeleteModeUI 异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                
                // 错误恢复，强制重置到安全状态
                try
                {
                    _isDeleteMode = false;
                    GamesListView.SelectionMode = ListViewSelectionMode.Single;
                    GamesListView.IsItemClickEnabled = true;
                    
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    DeleteModeButton.Visibility = Visibility.Visible;
                    CleanDuplicateGamesButton.Visibility = Visibility.Visible;
                    ImportGamesDropDownButton.Visibility = Visibility.Visible;
                    AddGameButton.Visibility = Visibility.Visible;
                }
                catch
                {
                    // 忽略嵌套异常
                }
            }
        }

        #endregion

        #region Data Management

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
                            
                            // 按显示顺序然后添加到集合
                            var sortedGames = gameDataList
                                .Where(g => g != null && !string.IsNullOrEmpty(g.Title))
                                .OrderBy(g => g.DisplayOrder)
                                .ToList();
                            
                            foreach (var gameJson in sortedGames)
                            {
                                BitmapImage? iconImage = null;
                                
                                // 为有可执行文件路径的游戏提取图标
                                if (!string.IsNullOrEmpty(gameJson.ExecutablePath) && File.Exists(gameJson.ExecutablePath))
                                {
                                    iconImage = await IconExtractor.ExtractIconAsync(gameJson.ExecutablePath);
                                }
                                
                                var game = new CustomDataObject
                                {
                                    Title = gameJson.Title,
                                    ExecutablePath = gameJson.ExecutablePath,
                                    IconImage = iconImage,
                                    IsSteamGame = gameJson.IsSteamGame,
                                    SteamAppId = gameJson.SteamAppId,
                                    IsXboxGame = gameJson.IsXboxGame,
                                    XboxPackageFamilyName = gameJson.XboxPackageFamilyName,
                                    DisplayOrder = gameJson.DisplayOrder,
                                    CategoryId = gameJson.CategoryId ?? string.Empty,
                                    Category = gameJson.Category ?? "未分类",
                                    Playtime = gameJson.Playtime,
                                    LastActivity = gameJson.LastActivity
                                };
                                Items.Add(game);
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"已加载 {Items.Count} 个游戏，包括顺序恢复");
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
                System.Diagnostics.Debug.WriteLine("开始保存游戏数据");
                
                var localFolder = ApplicationData.Current.LocalFolder;
                var file = await localFolder.CreateFileAsync(GamesDataFileName, CreationCollisionOption.ReplaceExisting);
                
                var gameDataList = Items.Where(item => item != null && !string.IsNullOrEmpty(item.Title))
                                       .Select(item => new GameDataJson
                                       {
                                           Title = item.Title,
                                           ExecutablePath = item.ExecutablePath,
                                           IsSteamGame = item.IsSteamGame,
                                           SteamAppId = item.SteamAppId,
                                           IsXboxGame = item.IsXboxGame,
                                           XboxPackageFamilyName = item.XboxPackageFamilyName,
                                           DisplayOrder = item.DisplayOrder,
                                           CategoryId = item.CategoryId,
                                           Category = item.Category,
                                           Playtime = item.Playtime,
                                           LastActivity = item.LastActivity
                                       }).ToList();
                
                System.Diagnostics.Debug.WriteLine($"准备保存 {gameDataList.Count} 个游戏");
                
                var json = JsonSerializer.Serialize(gameDataList, new JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(file, json);
                
                System.Diagnostics.Debug.WriteLine("游戏数据保存成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存游戏数据时发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                
                // 不要向上抛出异常，而是记录错误并尝试通知用户
                await ShowErrorDialog($"保存游戏数据失败: {ex.Message}");
            }
        }

        private async Task SaveCurrentGameOrder()
        {
            try
            {
                await SaveGamesData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存游戏顺序时出错: {ex.Message}");
            }
        }

        #endregion

        #region Dialog Methods

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

            var categoryComboBox = new ComboBox()
            {
                ItemsSource = Categories.Where(c => c.Id != "all").ToList(),
                DisplayMemberPath = "Name",
                SelectedValuePath = "Id",
                SelectedValue = "uncategorized",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 10)
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
            contentPanel.Children.Add(new TextBlock() { Text = "游戏分类:", Margin = new Thickness(0, 10, 0, 5) });
            contentPanel.Children.Add(categoryComboBox);

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
                var selectedCategory = categoryComboBox.SelectedItem as GameCategory;

                var gameData = new CustomDataObject
                {
                    Title = gameNameBox.Text.Trim(),
                    ExecutablePath = pathBox.Text.Trim(),
                    IconImage = iconImage,
                    CategoryId = selectedCategory?.Id ?? "uncategorized",
                    Category = selectedCategory?.Name ?? "未分类"
                };

                Items.Add(gameData);
                await SaveGamesData();
                
                // 确保UI立即更新显示新添加的游戏
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
            }
        }

        private async Task ShowSetCategoryDialog(CustomDataObject game)
        {
            try
            {
                var categoryComboBox = new ComboBox()
                {
                    ItemsSource = Categories.Where(c => c.Id != "all").ToList(),
                    DisplayMemberPath = "Name",
                    SelectedValuePath = "Id",
                    SelectedValue = game.CategoryId ?? "uncategorized",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock() 
                { 
                    Text = $"为游戏 \"{game.Title}\" 选择分类:", 
                    Margin = new Thickness(0, 0, 0, 10) 
                });
                contentPanel.Children.Add(categoryComboBox);

                var dialog = new ContentDialog()
                {
                    Title = "设置游戏分类",
                    Content = contentPanel,
                    PrimaryButtonText = "确定",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var selectedCategory = categoryComboBox.SelectedItem as GameCategory;
                    if (selectedCategory != null)
                    {
                        game.CategoryId = selectedCategory.Id;
                        game.Category = selectedCategory.Name;
                        
                        await SaveGamesData();
                        
                        // 确保UI立即更新以反映分类变更
                        ApplyCategoryFilter();
                        UpdateCategoryGameCounts();
                        
                        // Update details if this is the selected game
                        if (SelectedGame == game)
                        {
                            UpdateGameDetails();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示设置分类对话框时错误: {ex.Message}");
                throw;
            }
        }

        private async Task ShowManageCategoriesDialog()
        {
            try
            {
                var dialog = new ManageCategoriesDialog()
                {
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                
                // 对话框关闭后，刷新分类相关的UI
                if (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
                {
                    // 重新计算分类的游戏数量
                    UpdateCategoryGameCounts();
                    
                    // 如果当前选中的分类被删除了，切换到"全部游戏"
                    if (_selectedCategory != null && !Categories.Contains(_selectedCategory))
                    {
                        _selectedCategory = Categories.FirstOrDefault(c => c.Id == "all");
                        CategoryComboBox.SelectedItem = _selectedCategory;
                    }
                    
                    // 重新应用分类筛选
                    ApplyCategoryFilter();
                    
                    // 更新选中游戏的分类信息（如果分类被修改）
                    if (SelectedGame != null)
                    {
                        var updatedCategory = Categories.FirstOrDefault(c => c.Id == SelectedGame.CategoryId);
                        if (updatedCategory != null)
                        {
                            SelectedGame.Category = updatedCategory.Name;
                            // CategoryColor 会自动通过 CategoryId 更新，不需要手动设置
                        }
                        UpdateGameDetails();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示管理分类对话框时出错: {ex.Message}");
                throw;
            }
        }

        private static bool _isErrorDialogShowing = false;

        private async Task ShowErrorDialog(string message)
        {
            try
            {
                // 防止重复显示错误对话框
                if (_isErrorDialogShowing)
                {
                    System.Diagnostics.Debug.WriteLine($"错误对话框已在显示中，跳过: {message}");
                    return;
                }

                _isErrorDialogShowing = true;
                System.Diagnostics.Debug.WriteLine($"显示错误对话框: {message}");

                var dialog = new ContentDialog()
                {
                    Title = "错误",
                    Content = message ?? "发生未知错误",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
                System.Diagnostics.Debug.WriteLine("错误对话框关闭");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowErrorDialog error: {ex.Message}");
            }
            finally
            {
                _isErrorDialogShowing = false;
            }
        }

        private async Task ShowInfoDialog(string content)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "信息",
                    Content = content,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示信息对话框时出错: {ex.Message}");
            }
        }

        #endregion

        #region Steam and Xbox Import (Simplified placeholders)

        private async Task ImportSteamGames()
        {
            await ShowInfoDialog("Steam 游戏导入功能即将实现");
        }

        private async Task ImportXboxGames()
        {
            await ShowInfoDialog("Xbox 游戏导入功能即将实现");
        }

        private async Task CleanDuplicateGames()
        {
            try
            {
                var duplicateGroups = Items
                    .GroupBy(item => item.ExecutablePath.ToLowerInvariant())
                    .Where(group => group.Count() > 1 && !string.IsNullOrWhiteSpace(group.Key))
                    .ToList();

                var removedCount = 0;
                if (duplicateGroups.Count > 0)
                {
                    foreach (var group in duplicateGroups)
                    {
                        var items = group.OrderBy(item => item.Title).Skip(1).ToList();
                        foreach (var item in items)
                        {
                            Items.Remove(item);
                            removedCount++;
                        }
                    }

                    if (removedCount > 0)
                    {
                        await SaveGamesData();
                        System.Diagnostics.Debug.WriteLine($"清理了 {removedCount} 个重复游戏");
                        await ShowInfoDialog($"清理了 {removedCount} 个重复游戏");
                    }
                    else
                    {
                        await ShowInfoDialog("没有发现重复游戏");
                    }
                }
                else
                {
                    await ShowInfoDialog("没有发现重复游戏");
                }

                // 始终刷新UI，确保显示最新状态
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理重复游戏时出错: {ex.Message}");
            }
        }

        #endregion

        #region Helper Classes

        public class GameDataJson
        {
            public string Title { get; set; } = string.Empty;
            public string ExecutablePath { get; set; } = string.Empty;
            public string SteamAppId { get; set; } = string.Empty;
            public bool IsSteamGame { get; set; } = false;
            public string XboxPackageFamilyName { get; set; } = string.Empty;
            public bool IsXboxGame { get; set; } = false;
            public int DisplayOrder { get; set; } = 0;
            public string CategoryId { get; set; } = string.Empty;
            public string Category { get; set; } = "未分类";
            public ulong Playtime { get; set; } = 0;
            public DateTime? LastActivity { get; set; }
        }

        #endregion
    }
}

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
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;

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

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // 页面离开时保存当前的游戏顺序
            await SaveCurrentGameOrder();
        }

        #region Context Menu Event Handlers

        private void ContentGridView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("右键点击事件触发");
                
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

        #endregion

        #region New Methods for Context Menu Actions

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
                    
                    // 确保游戏在集合中存在
                    if (Items.Contains(game))
                    {
                        Items.Remove(game);
                        System.Diagnostics.Debug.WriteLine("游戏已从集合中移除");
                        
                        // 保存更新后的数据
                        await SaveGamesData();
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

        private async void ImportSteamGamesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ImportSteamGames();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"导入 Steam 游戏时错误: {ex.Message}");
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
                
                // 确保在 UI 线程上执行
                var tcs = new TaskCompletionSource<bool>();
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        foreach (var item in ContentGridView.SelectedItems)
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
                    // 忽略 UI 更新异常
                }
                
                await ShowErrorDialog($"删除游戏时错误: {ex.Message}");
            }
        }

        #endregion

        #region Steam Import Methods

        private async Task ImportSteamGames()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始导入 Steam 游戏");

                // 检查 Steam 是否安装
                if (!SteamService.IsSteamInstalled())
                {
                    await ShowErrorDialog("未检测到 Steam 安装。请确保 Steam 已正确安装。");
                    return;
                }

                // 显示进度对话框
                var progressDialog = new ContentDialog()
                {
                    Title = "导入 Steam 游戏",
                    Content = "正在扫描 Steam 游戏库，请稍候...",
                    XamlRoot = this.XamlRoot
                };

                // 异步显示对话框并开始扫描
                _ = progressDialog.ShowAsync();

                try
                {
                    // 扫描 Steam 游戏
                    var steamGames = await SteamService.ScanSteamGamesAsync();
                    
                    // 关闭进度对话框
                    progressDialog.Hide();

                    if (steamGames.Count == 0)
                    {
                        await ShowErrorDialog("未找到已安装的 Steam 游戏。");
                        return;
                    }

                    // 过滤掉已经存在的游戏 - 使用多重检查避免重复
                    var existingAppIds = Items.Where(item => item.IsSteamGame)
                                            .Select(item => item.SteamAppId)
                                            .Where(appId => !string.IsNullOrEmpty(appId))
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // 同时检查可执行文件路径，避免路径大小写导致的重复
                    var existingPaths = Items.Where(item => !string.IsNullOrEmpty(item.ExecutablePath))
                                           .Select(item => Path.GetFullPath(item.ExecutablePath).ToLowerInvariant())
                                           .ToHashSet();

                    var newGames = steamGames.Where(game => 
                    {
                        // 检查 AppID 是否已存在
                        if (existingAppIds.Contains(game.AppId))
                        {
                            System.Diagnostics.Debug.WriteLine($"跳过重复的 Steam AppID: {game.AppId} - {game.Name}");
                            return false;
                        }

                        // 检查可执行文件路径是否已存在（标准化路径比较）
                        if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                        {
                            var normalizedPath = Path.GetFullPath(game.ExecutablePath).ToLowerInvariant();
                            if (existingPaths.Contains(normalizedPath))
                            {
                                System.Diagnostics.Debug.WriteLine($"跳过重复的可执行文件路径: {game.ExecutablePath} - {game.Name}");
                                return false;
                            }
                        }

                        return true;
                    }).ToList();

                    if (newGames.Count == 0)
                    {
                        await ShowErrorDialog("所有 Steam 游戏都已导入。");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"找到 {newGames.Count} 个新的 Steam 游戏待导入");

                    // 显示选择对话框
                    var selectedGames = await ShowSteamGameSelectionDialog(newGames);
                    if (selectedGames.Count > 0)
                    {
                        await ImportSelectedSteamGames(selectedGames);
                        await ShowInfoDialog($"成功导入 {selectedGames.Count} 个 Steam 游戏！");
                    }
                }
                catch
                {
                    progressDialog.Hide();
                    throw;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导入 Steam 游戏时发生异常: {ex.Message}");
                await ShowErrorDialog($"导入 Steam 游戏时错误: {ex.Message}");
            }
        }

        private async Task<List<SteamGame>> ShowSteamGameSelectionDialog(List<SteamGame> steamGames)
        {
            var selectedGames = new List<SteamGame>();

            try
            {
                // 创建一个包装类用于绑定
                var gameSelectionItems = steamGames.Select(game => new GameSelectionItem
                {
                    Game = game,
                    IsSelected = true, // 默认全选
                    DisplayName = game.Name
                }).ToList();

                // 创建游戏选择列表
                var stackPanel = new StackPanel()
                {
                    Spacing = 6
                };

                foreach (var item in gameSelectionItems)
                {
                    var checkBox = new CheckBox()
                    {
                        Content = item.DisplayName,
                        IsChecked = item.IsSelected,
                        Tag = item,
                        FontSize = 14,
                        Margin = new Thickness(4, 2, 4, 2)
                    };

                    // 绑定选中状态
                    checkBox.Checked += (s, e) => 
                    {
                        if (checkBox.Tag is GameSelectionItem selectionItem)
                            selectionItem.IsSelected = true;
                    };
                    checkBox.Unchecked += (s, e) => 
                    {
                        if (checkBox.Tag is GameSelectionItem selectionItem)
                            selectionItem.IsSelected = false;
                    };

                    stackPanel.Children.Add(checkBox);
                }

                var scrollViewer = new ScrollViewer()
                {
                    Content = stackPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Height = 400,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock() 
                { 
                    Text = $"找到 {steamGames.Count} 个新的 Steam 游戏，请选择要导入的游戏:",
                    Margin = new Thickness(0, 0, 0, 10),
                    TextWrapping = TextWrapping.Wrap
                });
                contentPanel.Children.Add(scrollViewer);

                // 创建操作按钮
                var buttonPanel = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var selectAllButton = new Button()
                {
                    Content = "全选"
                };

                var deselectAllButton = new Button()
                {
                    Content = "全不选"
                };

                selectAllButton.Click += (s, e) =>
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is CheckBox checkBox)
                        {
                            checkBox.IsChecked = true;
                            if (checkBox.Tag is GameSelectionItem selectionItem)
                            {
                                selectionItem.IsSelected = true;
                            }
                        }
                    }
                };

                deselectAllButton.Click += (s, e) =>
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is CheckBox checkBox)
                        {
                            checkBox.IsChecked = false;
                            if (checkBox.Tag is GameSelectionItem selectionItem)
                            {
                                selectionItem.IsSelected = false;
                            }
                        }
                    }
                };

                buttonPanel.Children.Add(selectAllButton);
                buttonPanel.Children.Add(deselectAllButton);
                contentPanel.Children.Add(buttonPanel);

                var dialog = new ContentDialog()
                {
                    Title = "选择 Steam 游戏",
                    Content = contentPanel,
                    PrimaryButtonText = "导入选中的游戏",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    selectedGames.AddRange(gameSelectionItems
                        .Where(item => item.IsSelected)
                        .Select(item => item.Game));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示 Steam 游戏选择对话框时出错: {ex.Message}");
            }

            return selectedGames;
        }

        private async Task ImportSelectedSteamGames(List<SteamGame> steamGames)
        {
            try
            {
                foreach (var steamGame in steamGames)
                {
                    System.Diagnostics.Debug.WriteLine($"导入 Steam 游戏: {steamGame.Name}");

                    // 尝试提取图标
                    BitmapImage? iconImage = null;
                    if (!string.IsNullOrEmpty(steamGame.ExecutablePath) && File.Exists(steamGame.ExecutablePath))
                    {
                        iconImage = await IconExtractor.ExtractIconAsync(steamGame.ExecutablePath);
                    }

                    var gameData = new CustomDataObject
                    {
                        Title = steamGame.Name,
                        ExecutablePath = steamGame.ExecutablePath,
                        IconImage = iconImage,
                        IsSteamGame = true,
                        SteamAppId = steamGame.AppId
                    };

                    Items.Add(gameData);
                }

                // 保存数据
                await SaveGamesData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导入选中的 Steam 游戏时出错: {ex.Message}");
                throw;
            }
        }

        private async Task ShowInfoDialog(string message)
        {
            try
            {
                var dialog = new ContentDialog()
                {
                    Title = "信息",
                    Content = message,
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

        private async Task CleanDuplicateGames()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始清理重复游戏");

                var duplicates = new List<CustomDataObject>();
                var uniqueGames = new Dictionary<string, CustomDataObject>();

                foreach (var game in Items.ToList())
                {
                    string uniqueKey = "";

                    // 对于 Steam 游戏，使用 AppID 作为唯一标识
                    if (game.IsSteamGame && !string.IsNullOrEmpty(game.SteamAppId))
                    {
                        uniqueKey = $"steam_{game.SteamAppId}";
                    }
                    // 对于非 Steam 游戏或没有 AppID 的游戏，使用标准化的可执行文件路径
                    else if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                    {
                        try
                        {
                            uniqueKey = $"path_{Path.GetFullPath(game.ExecutablePath).ToLowerInvariant()}";
                        }
                        catch
                        {
                            uniqueKey = $"path_{game.ExecutablePath.ToLowerInvariant()}";
                        }
                    }
                    // 使用游戏名称作为最后的唯一标识（不推荐，但作为备选）
                    else
                    {
                        uniqueKey = $"name_{game.Title.ToLowerInvariant()}";
                    }

                    if (uniqueGames.ContainsKey(uniqueKey))
                    {
                        // 发现重复游戏
                        var existingGame = uniqueGames[uniqueKey];
                        System.Diagnostics.Debug.WriteLine($"发现重复游戏: {game.Title} (现有: {existingGame.Title})");
                        
                        // 选择保留更完整的游戏信息
                        if (ShouldKeepGame(game, existingGame))
                        {
                            duplicates.Add(existingGame);
                            uniqueGames[uniqueKey] = game;
                            System.Diagnostics.Debug.WriteLine($"保留新游戏: {game.Title}");
                        }
                        else
                        {
                            duplicates.Add(game);
                            System.Diagnostics.Debug.WriteLine($"保留现有游戏: {existingGame.Title}");
                        }
                    }
                    else
                    {
                        uniqueGames[uniqueKey] = game;
                    }
                }

                if (duplicates.Count == 0)
                {
                    await ShowInfoDialog("未发现重复的游戏。");
                    return;
                }

                // 显示确认对话框
                var gameListText = string.Join("\n", duplicates.Take(5).Select(g => $"? {g.Title}"));
                if (duplicates.Count > 5)
                {
                    gameListText += $"\n... 还有 {duplicates.Count - 5} 个";
                }

                var confirmDialog = new ContentDialog()
                {
                    Title = "清理重复游戏",
                    Content = $"发现 {duplicates.Count} 个重复的游戏。是否要删除这些重复项？\n\n将要删除的游戏:\n{gameListText}",
                    PrimaryButtonText = "删除重复项",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // 删除重复的游戏
                    foreach (var duplicate in duplicates)
                    {
                        Items.Remove(duplicate);
                        System.Diagnostics.Debug.WriteLine($"删除重复游戏: {duplicate.Title}");
                    }

                    // 保存更新后的数据
                    await SaveGamesData();

                    await ShowInfoDialog($"成功清理了 {duplicates.Count} 个重复的游戏！");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理重复游戏时发生异常: {ex.Message}");
                throw;
            }
        }

        private bool ShouldKeepGame(CustomDataObject newGame, CustomDataObject existingGame)
        {
            // 优先保留有图标的游戏
            if (newGame.IconImage != null && existingGame.IconImage == null)
                return true;
            if (newGame.IconImage == null && existingGame.IconImage != null)
                return false;

            // 优先保留 Steam 游戏（有 AppID 的）
            if (newGame.IsSteamGame && !string.IsNullOrEmpty(newGame.SteamAppId) && 
                (!existingGame.IsSteamGame || string.IsNullOrEmpty(existingGame.SteamAppId)))
                return true;
            if (existingGame.IsSteamGame && !string.IsNullOrEmpty(existingGame.SteamAppId) && 
                (!newGame.IsSteamGame || string.IsNullOrEmpty(newGame.SteamAppId)))
                return false;

            // 优先保留有可执行文件路径且文件存在的游戏
            bool newGameHasValidPath = !string.IsNullOrEmpty(newGame.ExecutablePath) && File.Exists(newGame.ExecutablePath);
            bool existingGameHasValidPath = !string.IsNullOrEmpty(existingGame.ExecutablePath) && File.Exists(existingGame.ExecutablePath);

            if (newGameHasValidPath && !existingGameHasValidPath)
                return true;
            if (!newGameHasValidPath && existingGameHasValidPath)
                return false;

            // 默认保留现有游戏
            return false;
        }

        #endregion
        
        private void UpdateDeleteModeUI()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"更新删除模式UI: _isDeleteMode = {_isDeleteMode}");
                
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
                    CleanDuplicateGamesButton.Visibility = Visibility.Collapsed;
                    ImportSteamGamesButton.Visibility = Visibility.Collapsed;
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
                        if (ContentGridView.SelectedItems != null)
                        {
                            ContentGridView.SelectedItems.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"清除选择时异常: {ex.Message}");
                    }
                    
                    ContentGridView.SelectionMode = ListViewSelectionMode.None;
                    ContentGridView.IsItemClickEnabled = true;
                    
                    // Hide delete mode buttons
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    
                    // Show normal mode buttons
                    DeleteModeButton.Visibility = Visibility.Visible;
                    CleanDuplicateGamesButton.Visibility = Visibility.Visible;
                    ImportSteamGamesButton.Visibility = Visibility.Visible;
                    AddGameButton.Visibility = Visibility.Visible;
                    
                    System.Diagnostics.Debug.WriteLine("退出删除模式");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateDeleteModeUI 异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                
                // 尝试强制重置到安全状态
                try
                {
                    _isDeleteMode = false;
                    ContentGridView.SelectionMode = ListViewSelectionMode.None;
                    ContentGridView.IsItemClickEnabled = true;
                    
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    DeleteModeButton.Visibility = Visibility.Visible;
                    CleanDuplicateGamesButton.Visibility = Visibility.Visible;
                    ImportSteamGamesButton.Visibility = Visibility.Visible;
                    AddGameButton.Visibility = Visibility.Visible;
                }
                catch
                {
                    // 忽略重置异常
                }
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
            try
            {
                // Update delete button state based on selection
                if (_isDeleteMode)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            var selectedCount = ContentGridView.SelectedItems?.Count ?? 0;
                            DeleteSelectedButton.IsEnabled = selectedCount > 0;
                            System.Diagnostics.Debug.WriteLine($"选择变更: {selectedCount} 个项目被选中");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"更新删除按钮状态时异常: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContentGridView_SelectionChanged 异常: {ex.Message}");
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
                        System.Diagnostics.Debug.WriteLine("Steam 启动失败，尝试直接启动可执行文件");
                    }
                }

                // 直接启动可执行文件（非 Steam 游戏或 Steam 启动失败时的备选方案）
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

        private async Task LoadGamesData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始加载游戏数据");
                
                var json = await DataStorageService.ReadTextFileAsync(GamesDataFileName);
                
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
                        
                        // 按显示顺序排序然后添加到集合中
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
                                DisplayOrder = gameJson.DisplayOrder
                            };
                            Items.Add(game);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"已加载 {Items.Count} 个游戏，按显示顺序排序");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("游戏数据文件为空或不存在");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadGamesData error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoadGamesData stack trace: {ex.StackTrace}");
            }
        }

        private async Task SaveGamesData()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始保存游戏数据");
                
                var gameDataList = Items.Where(item => item != null && !string.IsNullOrEmpty(item.Title))
                                       .Select(item => new GameDataJson
                                       {
                                           Title = item.Title,
                                           ExecutablePath = item.ExecutablePath,
                                           IsSteamGame = item.IsSteamGame,
                                           SteamAppId = item.SteamAppId,
                                           DisplayOrder = item.DisplayOrder
                                       }).ToList();
                
                System.Diagnostics.Debug.WriteLine($"准备保存 {gameDataList.Count} 个游戏");
                
                var json = JsonSerializer.Serialize(gameDataList, new JsonSerializerOptions { WriteIndented = true });
                await DataStorageService.WriteTextFileAsync(GamesDataFileName, json);
                
                System.Diagnostics.Debug.WriteLine("游戏数据保存成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存游戏数据时发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                
                // 需要重新抛出异常，但是记录错误并尝试通知用户
                await ShowErrorDialog($"保存游戏数据失败: {ex.Message}");
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

        // 添加辅助类用于游戏选择
        private class GameSelectionItem
        {
            public SteamGame Game { get; set; } = new SteamGame();
            public bool IsSelected { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }

        private async Task SaveCurrentGameOrder()
        {
            try
            {
                // 更新每个游戏的显示顺序
                for (int i = 0; i < Items.Count; i++)
                {
                    Items[i].DisplayOrder = i;
                }
                
                // 保存到文件
                await SaveGamesData();
                System.Diagnostics.Debug.WriteLine("游戏顺序已保存");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存游戏顺序时出错: {ex.Message}");
            }
        }

        private async void ContentGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            try
            {
                // 拖拽操作完成后，保存新的游戏顺序
                await SaveCurrentGameOrder();
                System.Diagnostics.Debug.WriteLine("拖拽重排完成，游戏顺序已保存");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理拖拽完成事件时出错: {ex.Message}");
            }
        }
    }

    public class GameDataJson
    {
        public string Title { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string SteamAppId { get; set; } = string.Empty;
        public bool IsSteamGame { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;
    }
}
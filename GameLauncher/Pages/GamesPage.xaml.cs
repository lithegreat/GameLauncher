using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using GameLauncher.Services;
using GameLauncher.Models;
using GameLauncher.Managers;

namespace GameLauncher.Pages
{
    /// <summary>
    /// 游戏页面 - 重构后的简化版本
    /// </summary>
    public sealed partial class GamesPage : Page, INotifyPropertyChanged
    {
        #region Fields and Properties

        // 管理器
        private readonly GameDataManager _gameDataManager;
        private readonly GameSelectionManager _gameSelectionManager;
        private readonly GameCategoryManager _gameCategoryManager;
        private readonly GameDialogManager _gameDialogManager;
        private readonly GameImportManager _gameImportManager;
        private readonly GameOperationManager _gameOperationManager;
        private readonly GameDragDropManager _gameDragDropManager;

        // UI状态
        private bool _isDeleteMode = false;
        private CustomDataObject? _contextMenuGame = null;

        // 属性绑定
        public ObservableCollection<CustomDataObject> Items => _gameDataManager.Items;
        public ObservableCollection<CustomDataObject> FilteredItems => _gameDataManager.FilteredItems;
        public ObservableCollection<GameCategory> Categories => CategoryService.Instance.Categories;

        public CustomDataObject? SelectedGame
        {
            get => _gameSelectionManager.SelectedGame;
            set => _gameSelectionManager.SelectedGame = value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Initialization

        public GamesPage()
        {
            this.InitializeComponent();
            
            // 初始化管理器
            _gameDataManager = new GameDataManager();
            _gameSelectionManager = new GameSelectionManager(this);
            _gameCategoryManager = new GameCategoryManager(_gameDataManager);
            _gameDialogManager = new GameDialogManager(this, _gameDataManager, _gameCategoryManager);
            _gameImportManager = new GameImportManager(this, _gameDataManager, _gameCategoryManager, _gameDialogManager);
            _gameOperationManager = new GameOperationManager(_gameDataManager, _gameCategoryManager, _gameDialogManager);
            _gameDragDropManager = new GameDragDropManager(GamesListView, _gameDataManager, _gameCategoryManager, _gameDialogManager);

            // 设置事件订阅
            SetupEventSubscriptions();

            this.Loaded += GamesPage_Loaded;
        }

        private void SetupEventSubscriptions()
        {
            // 分类删除事件
            CategoryService.Instance.CategoryDeleted += OnCategoryDeleted;

            // 游戏选择事件
            _gameSelectionManager.SelectedGameChanged += OnSelectedGameChanged;
            _gameSelectionManager.SetCategoryRequested += OnSetCategoryRequested;
            _gameSelectionManager.OpenInSteamRequested += OnOpenInSteamRequested;
            _gameSelectionManager.DeleteGameRequested += OnDeleteGameRequested;

            // 分类管理事件
            _gameCategoryManager.FilterChanged += OnFilterChanged;
            _gameCategoryManager.CategoriesChanged += OnCategoriesChanged;
        }

        private async void GamesPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("GamesPage_Loaded 开始");

                // 确保分类服务已加载
                await CategoryService.Instance.LoadCategoriesAsync();

                // 只在数据未加载时加载数据，避免覆盖已排序的数据
                if (!_gameDataManager.IsDataLoaded)
                {
                    Debug.WriteLine("首次加载，加载游戏数据");
                    await _gameDataManager.LoadGamesDataAsync();
                }

                // 初始化分类选择
                _gameCategoryManager.InitializeDefaultCategory();
                _gameCategoryManager.ApplyCategoryFilter();
                _gameCategoryManager.UpdateCategoryGameCounts();

                Debug.WriteLine("GamesPage_Loaded 完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GamesPage_Loaded 异常: {ex.Message}");
                await _gameDialogManager.ShowErrorDialogAsync($"页面加载时出错: {ex.Message}");
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 异步执行初始化操作
            _ = Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("GamesPage OnNavigatedTo 开始");

                    // 只在数据未加载时重新加载数据
                    if (!_gameDataManager.IsDataLoaded)
                    {
                        Debug.WriteLine("数据未加载，重新加载游戏数据");
                        await CategoryService.Instance.LoadCategoriesAsync();
                        await _gameDataManager.LoadGamesDataAsync();

                        // 在UI线程上执行UI更新
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            _gameCategoryManager.InitializeDefaultCategory();
                            _gameCategoryManager.ApplyCategoryFilter();
                            _gameCategoryManager.UpdateCategoryGameCounts();
                        });
                    }
                    else
                    {
                        Debug.WriteLine("数据已加载，保持当前状态");

                        // 确保分类服务已加载（在页面导航回来时可能需要）
                        await CategoryService.Instance.LoadCategoriesAsync();

                        // 在UI线程上执行UI更新
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            _gameCategoryManager.ApplyCategoryFilter();
                            _gameCategoryManager.UpdateCategoryGameCounts();
                        });
                    }

                    Debug.WriteLine("GamesPage OnNavigatedTo 完成");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnNavigatedTo 异常: {ex.Message}");
                }
            });
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // 页面离开时保存当前的游戏顺序
            await _gameDataManager.SaveGamesDataAsync();
        }

        #endregion

        #region Event Handlers - Manager Events

        private async void OnCategoryDeleted(string deletedCategoryId)
        {
            await _gameCategoryManager.HandleCategoryDeleted(deletedCategoryId);

            // 如果当前选中的游戏受到影响，更新详情显示
            if (SelectedGame != null && SelectedGame.CategoryId == deletedCategoryId)
            {
                await _gameSelectionManager.UpdateGameDetailsAsync();
            }
        }

        private void OnSelectedGameChanged(CustomDataObject? selectedGame)
        {
            OnPropertyChanged(nameof(SelectedGame));
        }

        private async void OnSetCategoryRequested(CustomDataObject game)
        {
            await _gameDialogManager.ShowSetCategoryDialogAsync(game);
            
            // Update details if this is the selected game
            if (SelectedGame == game)
            {
                await _gameSelectionManager.UpdateGameDetailsAsync();
            }
        }

        private async void OnOpenInSteamRequested(CustomDataObject game)
        {
            await _gameOperationManager.OpenInSteamAsync(game);
        }

        private async void OnDeleteGameRequested(CustomDataObject game)
        {
            await _gameOperationManager.DeleteSingleGameAsync(game);
            
            // Clear selected game if it was deleted
            if (SelectedGame == game)
            {
                SelectedGame = null;
            }
        }

        private void OnFilterChanged()
        {
            // UI已通过绑定自动更新
        }

        private void OnCategoriesChanged()
        {
            // 分类数量已更新，UI会自动反映
        }

        #endregion

        #region Event Handlers - UI Events

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
                Debug.WriteLine($"GamesListView_ItemClick error: {ex.Message}");
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
                            Debug.WriteLine($"选择变更: {selectedCount} 个项目被选中");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"更新删除按钮状态时异常: {ex.Message}");
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
                Debug.WriteLine($"GamesListView_SelectionChanged error: {ex.Message}");
            }
        }

        private void GamesListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("右键点击事件触发");

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
                    Debug.WriteLine($"设置上下文菜单游戏: {game.Title}");

                    // 根据游戏类型动态显示菜单项
                    UpdateContextMenuForGame(game);

                    // Context menu will show automatically due to ContextFlyout in XAML
                }
                else
                {
                    _contextMenuGame = null;
                    Debug.WriteLine("未找到游戏数据上下文");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"右键点击处理异常: {ex.Message}");
                _contextMenuGame = null;
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox && comboBox.SelectedItem is GameCategory selectedCategory)
                {
                    _gameCategoryManager.SelectedCategory = selectedCategory;

                    // Clear selection when changing categories
                    SelectedGame = null;
                    GamesListView.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"分类选择变更时错误: {ex.Message}");
            }
        }

        #endregion

        #region Button Event Handlers

        private async void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _gameDialogManager.ShowAddGameDialogAsync();
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"添加游戏时错误: {ex.Message}");
            }
        }

        private async void LaunchGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedGame != null)
                {
                    await _gameOperationManager.LaunchGameAsync(SelectedGame);
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"启动游戏时错误: {ex.Message}");
            }
        }

        private async void OpenDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedGame != null)
                {
                    await _gameOperationManager.OpenGameDirectoryAsync(SelectedGame);
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"打开游戏目录时错误: {ex.Message}");
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
                Debug.WriteLine("取消删除模式");

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        _isDeleteMode = false;
                        UpdateDeleteModeUI();
                        Debug.WriteLine("删除模式已取消");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"取消删除模式时UI异常: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"取消删除按钮异常: {ex.Message}");
                _isDeleteMode = false;
            }
        }

        private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("开始批量删除游戏");

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
                        Debug.WriteLine($"选中了 {selectedItems.Count} 个游戏");
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"获取选中项目时异常: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });

                await tcs.Task;

                if (selectedItems.Count == 0)
                {
                    await _gameDialogManager.ShowErrorDialogAsync("请选择要删除的游戏");
                    return;
                }

                bool confirmed = await _gameDialogManager.ShowDeleteConfirmationDialogAsync(
                    "确认删除",
                    $"确定要删除 {selectedItems.Count} 个游戏吗？此操作无法撤销。");

                if (confirmed)
                {
                    Debug.WriteLine("用户确认批量删除，开始执行删除操作");

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
                            _gameDataManager.RemoveGames(selectedItems);

                            Debug.WriteLine("批量删除完成，退出删除模式");

                            // Exit delete mode
                            _isDeleteMode = false;
                            UpdateDeleteModeUI();

                            deleteTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"UI线程删除操作异常: {ex.Message}");
                            deleteTcs.SetException(ex);
                        }
                    });

                    await deleteTcs.Task;

                    // Save the updated data
                    await _gameDataManager.SaveGamesDataAsync();

                    // 确保UI立即更新
                    _gameCategoryManager.ApplyCategoryFilter();
                    _gameCategoryManager.UpdateCategoryGameCounts();
                }
                else
                {
                    Debug.WriteLine("用户取消批量删除操作");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"批量删除游戏时发生异常: {ex.Message}");

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
                            Debug.WriteLine($"更新UI异常: {updateEx.Message}");
                        }
                    });
                }
                catch
                {
                    // 忽略嵌套异常
                }

                await _gameDialogManager.ShowErrorDialogAsync($"删除游戏时出错: {ex.Message}");
            }
        }

        private async void ManageCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _gameDialogManager.ShowManageCategoriesDialogAsync();
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"管理分类时错误: {ex.Message}");
            }
        }

        private async void ImportSteamGamesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _gameImportManager.ImportSteamGamesAsync();
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"导入Steam游戏时错误: {ex.Message}");
            }
        }

        private async void ImportXboxGamesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _gameImportManager.ImportXboxGamesAsync();
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"导入Xbox游戏时错误: {ex.Message}");
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
                    Debug.WriteLine($"打开游戏: {_contextMenuGame.Title}");
                    await _gameOperationManager.LaunchGameAsync(_contextMenuGame);
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"打开游戏时错误: {ex.Message}");
            }
        }

        private async void DeleteGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    Debug.WriteLine($"删除游戏菜单项点击: {_contextMenuGame.Title}");
                    await _gameOperationManager.DeleteSingleGameAsync(_contextMenuGame);
                    
                    // Clear selected game if it was deleted
                    if (SelectedGame == _contextMenuGame)
                    {
                        SelectedGame = null;
                    }
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"删除游戏时错误: {ex.Message}");
            }
        }

        private async void OpenGameDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    Debug.WriteLine($"打开游戏目录: {_contextMenuGame.Title}");
                    await _gameOperationManager.OpenGameDirectoryAsync(_contextMenuGame);
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"打开游戏目录时错误: {ex.Message}");
            }
        }

        private async void OpenInSteamMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null && _contextMenuGame.IsSteamGame)
                {
                    Debug.WriteLine($"在 Steam 中打开游戏: {_contextMenuGame.Title}");
                    await _gameOperationManager.OpenInSteamAsync(_contextMenuGame);
                }
                else
                {
                    await _gameDialogManager.ShowErrorDialogAsync("该游戏不是 Steam 游戏");
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"在 Steam 中打开游戏时错误: {ex.Message}");
            }
        }

        private async void SetCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    await _gameDialogManager.ShowSetCategoryDialogAsync(_contextMenuGame);
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"设置分类时错误: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

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
                Debug.WriteLine($"更新上下文菜单时异常: {ex.Message}");
            }
        }

        private void UpdateDeleteModeUI()
        {
            try
            {
                Debug.WriteLine($"更新删除模式UI: _isDeleteMode = {_isDeleteMode}");
                
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
                    AddGameDropDownButton.Visibility = Visibility.Collapsed;
                    
                    // Initialize delete button status
                    DeleteSelectedButton.IsEnabled = false;
                    
                    Debug.WriteLine("进入删除模式");
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
                        Debug.WriteLine($"清空选择时异常: {ex.Message}");
                    }
                    
                    GamesListView.SelectionMode = ListViewSelectionMode.Single;
                    GamesListView.IsItemClickEnabled = true;
                    
                    // Hide delete mode buttons
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    
                    // Show normal mode buttons
                    DeleteModeButton.Visibility = Visibility.Visible;
                    AddGameDropDownButton.Visibility = Visibility.Visible;
                    
                    Debug.WriteLine("退出删除模式");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateDeleteModeUI 异常: {ex.Message}");
                
                // 紧急修复，强制重置到安全状态
                try
                {
                    _isDeleteMode = false;
                    GamesListView.SelectionMode = ListViewSelectionMode.Single;
                    GamesListView.IsItemClickEnabled = true;
                    
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    DeleteModeButton.Visibility = Visibility.Visible;
                    AddGameDropDownButton.Visibility = Visibility.Visible;
                }
                catch
                {
                    // 忽略嵌套异常
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}

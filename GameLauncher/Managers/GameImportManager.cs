using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameLauncher.Models;
using GameLauncher.Services;
using System.Diagnostics;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;

namespace GameLauncher.Managers
{
    /// <summary>
    /// 游戏导入管理器，处理Steam和Xbox游戏导入
    /// </summary>
    public class GameImportManager
    {
        private readonly Page _page;
        private readonly GameDataManager _gameDataManager;
        private readonly GameCategoryManager _categoryManager;
        private readonly GameDialogManager _dialogManager;

        public GameImportManager(Page page, GameDataManager gameDataManager, 
            GameCategoryManager categoryManager, GameDialogManager dialogManager)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _gameDataManager = gameDataManager ?? throw new ArgumentNullException(nameof(gameDataManager));
            _categoryManager = categoryManager ?? throw new ArgumentNullException(nameof(categoryManager));
            _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        }

        public async Task ImportSteamGamesAsync()
        {
            try
            {
                Debug.WriteLine("开始导入 Steam 游戏");

                // 检查 Steam 是否安装
                if (!SteamService.IsSteamInstalled())
                {
                    await _dialogManager.ShowErrorDialogAsync("未检测到 Steam 安装。请确保 Steam 已正确安装。");
                    return;
                }

                // 显示进度对话框
                var progressDialog = new ContentDialog()
                {
                    Title = "导入 Steam 游戏",
                    Content = "正在扫描 Steam 游戏库，请稍候...",
                    XamlRoot = _page.XamlRoot
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
                        await _dialogManager.ShowErrorDialogAsync("未找到已安装的 Steam 游戏。");
                        return;
                    }

                    // 过滤掉已经存在的游戏
                    var newGames = FilterNewSteamGames(steamGames);

                    if (newGames.Count == 0)
                    {
                        await _dialogManager.ShowErrorDialogAsync("所有 Steam 游戏都已导入。");
                        return;
                    }

                    Debug.WriteLine($"找到 {newGames.Count} 个新的 Steam 游戏待导入");

                    // 显示选择对话框
                    var selectedGames = await ShowSteamGameSelectionDialogAsync(newGames);
                    if (selectedGames.Count > 0)
                    {
                        await ImportSelectedSteamGamesAsync(selectedGames);
                        await _dialogManager.ShowInfoDialogAsync($"成功导入 {selectedGames.Count} 个 Steam 游戏！");
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
                Debug.WriteLine($"导入 Steam 游戏时发生异常: {ex.Message}");
                await _dialogManager.ShowErrorDialogAsync($"导入 Steam 游戏时错误: {ex.Message}");
            }
        }

        public async Task ImportXboxGamesAsync()
        {
            try
            {
                Debug.WriteLine("开始导入 Xbox 游戏");

                // 显示进度对话框
                var progressDialog = new ContentDialog()
                {
                    Title = "导入 Xbox 游戏",
                    Content = "正在扫描 Xbox 游戏库，请稍后...",
                    XamlRoot = _page.XamlRoot
                };

                // 异步显示对话框并开始扫描
                _ = progressDialog.ShowAsync();

                try
                {
                    // 扫描 Xbox 游戏
                    var xboxGames = await XboxService.ScanXboxGamesAsync();

                    // 关闭进度对话框
                    progressDialog.Hide();

                    if (xboxGames.Count == 0)
                    {
                        await _dialogManager.ShowErrorDialogAsync("未找到已安装的 Xbox 游戏。");
                        return;
                    }

                    // 过滤掉已经存在的游戏
                    var newGames = FilterNewXboxGames(xboxGames);

                    if (newGames.Count == 0)
                    {
                        await _dialogManager.ShowErrorDialogAsync("所有 Xbox 游戏都已导入。");
                        return;
                    }

                    Debug.WriteLine($"找到 {newGames.Count} 个新的 Xbox 游戏待导入");

                    // 显示选择对话框
                    var selectedGames = await ShowXboxGameSelectionDialogAsync(newGames);
                    if (selectedGames.Count > 0)
                    {
                        await ImportSelectedXboxGamesAsync(selectedGames);
                        await _dialogManager.ShowInfoDialogAsync($"成功导入 {selectedGames.Count} 个 Xbox 游戏！");
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
                Debug.WriteLine($"导入 Xbox 游戏时发生异常: {ex.Message}");
                await _dialogManager.ShowErrorDialogAsync($"导入 Xbox 游戏时错误: {ex.Message}");
            }
        }

        private List<SteamGame> FilterNewSteamGames(List<SteamGame> steamGames)
        {
            // 过滤掉已经存在的游戏 - 使用多重检查避免重复
            var existingAppIds = _gameDataManager.Items.Where(item => item.IsSteamGame)
                                    .Select(item => item.SteamAppId)
                                    .Where(appId => !string.IsNullOrEmpty(appId))
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 同时检查可执行文件路径，避免路径大小写导致的重复
            var existingPaths = _gameDataManager.Items.Where(item => !string.IsNullOrEmpty(item.ExecutablePath))
                                   .Select(item => Path.GetFullPath(item.ExecutablePath).ToLowerInvariant())
                                   .ToHashSet();

            return steamGames.Where(game =>
            {
                // 检查 AppID 是否已存在
                if (existingAppIds.Contains(game.AppId))
                {
                    Debug.WriteLine($"跳过重复的 Steam AppID: {game.AppId} - {game.Name}");
                    return false;
                }

                // 检查可执行文件路径是否已存在（标准化路径比较）
                if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                {
                    var normalizedPath = Path.GetFullPath(game.ExecutablePath).ToLowerInvariant();
                    if (existingPaths.Contains(normalizedPath))
                    {
                        Debug.WriteLine($"跳过重复的可执行文件路径: {game.ExecutablePath} - {game.Name}");
                        return false;
                    }
                }

                return true;
            }).ToList();
        }

        private List<XboxGame> FilterNewXboxGames(List<XboxGame> xboxGames)
        {
            // 过滤掉已经存在的游戏 - 使用多重检查避免重复
            var existingPackageNames = _gameDataManager.Items.Where(item => item.IsXboxGame)
                                          .Select(item => item.XboxPackageFamilyName)
                                          .Where(name => !string.IsNullOrEmpty(name))
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 同时检查可执行文件路径避免不同路径的重复
            var existingPaths = _gameDataManager.Items.Where(item => !string.IsNullOrEmpty(item.ExecutablePath))
                                   .Select(item => Path.GetFullPath(item.ExecutablePath).ToLowerInvariant())
                                   .ToHashSet();

            return xboxGames.Where(game =>
            {
                // 检查 Package Family Name 是否已存在
                if (existingPackageNames.Contains(game.PackageFamilyName))
                {
                    Debug.WriteLine($"跳过重复的 Xbox Package: {game.PackageFamilyName} - {game.Name}");
                    return false;
                }

                // 检查可执行文件路径是否已存在（标准化路径比较）
                if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                {
                    var normalizedPath = Path.GetFullPath(game.ExecutablePath).ToLowerInvariant();
                    if (existingPaths.Contains(normalizedPath))
                    {
                        Debug.WriteLine($"跳过重复的可执行文件路径: {game.ExecutablePath} - {game.Name}");
                        return false;
                    }
                }

                return true;
            }).ToList();
        }

        private async Task<List<SteamGame>> ShowSteamGameSelectionDialogAsync(List<SteamGame> steamGames)
        {
            var selectedGames = new List<SteamGame>();

            try
            {
                // 创建一个包装类用于绑定
                var gameSelectionItems = steamGames.Select(game => new GameSelectionItem<SteamGame>
                {
                    Game = game,
                    IsSelected = true, // 默认全选
                    DisplayName = game.Name
                }).ToList();

                var selectedItems = await ShowGameSelectionDialogAsync(
                    "选择 Steam 游戏",
                    $"找到 {steamGames.Count} 个新的 Steam 游戏，请选择要导入的游戏:",
                    "导入选中的游戏",
                    gameSelectionItems);

                selectedGames.AddRange(selectedItems.Select(item => item.Game));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示 Steam 游戏选择对话框时出错: {ex.Message}");
            }

            return selectedGames;
        }

        private async Task<List<XboxGame>> ShowXboxGameSelectionDialogAsync(List<XboxGame> xboxGames)
        {
            var selectedGames = new List<XboxGame>();

            try
            {
                // 创建一个包装类用于绑定
                var gameSelectionItems = xboxGames.Select(game => new GameSelectionItem<XboxGame>
                {
                    Game = game,
                    IsSelected = true, // 默认全选
                    DisplayName = game.Name
                }).ToList();

                var selectedItems = await ShowGameSelectionDialogAsync(
                    "选择 Xbox 游戏",
                    $"找到 {xboxGames.Count} 个新的 Xbox 游戏，请选择要导入的游戏:",
                    "导入选中的游戏",
                    gameSelectionItems);

                selectedGames.AddRange(selectedItems.Select(item => item.Game));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示 Xbox 游戏选择对话框时出错: {ex.Message}");
            }

            return selectedGames;
        }

        private async Task<List<GameSelectionItem<T>>> ShowGameSelectionDialogAsync<T>(
            string title, string description, string confirmText, 
            List<GameSelectionItem<T>> gameSelectionItems)
        {
            var selectedItems = new List<GameSelectionItem<T>>();

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
                    if (checkBox.Tag is GameSelectionItem<T> selectionItem)
                        selectionItem.IsSelected = true;
                };
                checkBox.Unchecked += (s, e) =>
                {
                    if (checkBox.Tag is GameSelectionItem<T> selectionItem)
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
                Text = description,
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
                        if (checkBox.Tag is GameSelectionItem<T> selectionItem)
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
                        if (checkBox.Tag is GameSelectionItem<T> selectionItem)
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
                Title = title,
                Content = contentPanel,
                PrimaryButtonText = confirmText,
                SecondaryButtonText = "取消",
                XamlRoot = _page.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                selectedItems.AddRange(gameSelectionItems.Where(item => item.IsSelected));
            }

            return selectedItems;
        }

        private async Task ImportSelectedSteamGamesAsync(List<SteamGame> steamGames)
        {
            try
            {
                foreach (var steamGame in steamGames)
                {
                    Debug.WriteLine($"导入 Steam 游戏: {steamGame.Name}");

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
                        SteamAppId = steamGame.AppId,
                        CategoryId = "uncategorized", // Steam 游戏默认为未分类
                        Category = "未分类"
                    };

                    _gameDataManager.AddGame(gameData);
                }

                // 保存数据
                await _gameDataManager.SaveGamesDataAsync();
                _categoryManager.ApplyCategoryFilter();
                _categoryManager.UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"导入选定的 Steam 游戏时出错: {ex.Message}");
                throw;
            }
        }

        private async Task ImportSelectedXboxGamesAsync(List<XboxGame> xboxGames)
        {
            try
            {
                foreach (var xboxGame in xboxGames)
                {
                    Debug.WriteLine($"导入 Xbox 游戏: {xboxGame.Name}");

                    // 尝试提取图标
                    BitmapImage? iconImage = null;
                    if (!string.IsNullOrEmpty(xboxGame.ExecutablePath) && File.Exists(xboxGame.ExecutablePath))
                    {
                        iconImage = await IconExtractor.ExtractIconAsync(xboxGame.ExecutablePath);
                    }

                    var gameData = new CustomDataObject
                    {
                        Title = xboxGame.Name,
                        ExecutablePath = xboxGame.ExecutablePath,
                        IconImage = iconImage,
                        IsXboxGame = true,
                        XboxPackageFamilyName = xboxGame.PackageFamilyName,
                        CategoryId = "uncategorized", // Xbox 游戏默认为未分类
                        Category = "未分类"
                    };

                    _gameDataManager.AddGame(gameData);
                }

                // 保存数据
                await _gameDataManager.SaveGamesDataAsync();
                _categoryManager.ApplyCategoryFilter();
                _categoryManager.UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"导入选中的 Xbox 游戏时出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 游戏选择项辅助类
        /// </summary>
        private class GameSelectionItem<T>
        {
            public T Game { get; set; } = default(T)!;
            public bool IsSelected { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using System.IO;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using GameLauncher.Pages;

namespace GameLauncher.Managers
{
    /// <summary>
    /// 游戏对话框管理器
    /// </summary>
    public class GameDialogManager
    {
        private readonly Page _page;
        private readonly GameDataManager _gameDataManager;
        private readonly GameCategoryManager _categoryManager;
        private static bool _isErrorDialogShowing = false;

        public GameDialogManager(Page page, GameDataManager gameDataManager, GameCategoryManager categoryManager)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _gameDataManager = gameDataManager ?? throw new ArgumentNullException(nameof(gameDataManager));
            _categoryManager = categoryManager ?? throw new ArgumentNullException(nameof(categoryManager));
        }

        public async Task ShowAddGameDialogAsync()
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
                ItemsSource = CategoryService.Instance.Categories.Where(c => c.Id != "all").ToList(),
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
                    System.Diagnostics.Debug.WriteLine($"File picker error: {ex.Message}");
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
                XamlRoot = _page.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(gameNameBox.Text))
                {
                    await ShowErrorDialogAsync("请输入游戏名称");
                    return;
                }

                if (string.IsNullOrWhiteSpace(pathBox.Text))
                {
                    await ShowErrorDialogAsync("请选择游戏可执行文件");
                    return;
                }

                if (!File.Exists(pathBox.Text))
                {
                    await ShowErrorDialogAsync("指定的文件不存在");
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
                    Category = selectedCategory?.Name ?? "未分类",
                    DisplayOrder = _gameDataManager.GetNextDisplayOrder()
                };

                _gameDataManager.AddGame(gameData);
                await _gameDataManager.SaveGamesDataAsync();

                // 确保UI立即更新显示新添加的游戏
                _categoryManager.ApplyCategoryFilter();
                _categoryManager.UpdateCategoryGameCounts();
            }
        }

        public async Task ShowSetCategoryDialogAsync(CustomDataObject game)
        {
            try
            {
                var categoryComboBox = new ComboBox()
                {
                    ItemsSource = CategoryService.Instance.Categories.Where(c => c.Id != "all").ToList(),
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
                    XamlRoot = _page.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var selectedCategory = categoryComboBox.SelectedItem as GameCategory;
                    if (selectedCategory != null)
                    {
                        await _categoryManager.SetGameCategory(game, selectedCategory.Id, selectedCategory.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示设置分类对话框时错误: {ex.Message}");
                throw;
            }
        }

        public async Task ShowManageCategoriesDialogAsync()
        {
            try
            {
                var dialog = new ManageCategoriesDialog()
                {
                    XamlRoot = _page.XamlRoot
                };

                var result = await dialog.ShowAsync();

                // 对话框关闭后，刷新分类相关的UI
                if (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
                {
                    // 重新计算分类的游戏数量
                    _categoryManager.UpdateCategoryGameCounts();

                    // 如果当前选中的分类被删除了，切换到"全部游戏"
                    if (_categoryManager.SelectedCategory != null && 
                        !CategoryService.Instance.Categories.Contains(_categoryManager.SelectedCategory))
                    {
                        _categoryManager.SelectedCategory = CategoryService.Instance.Categories.FirstOrDefault(c => c.Id == "all");
                    }

                    // 重新应用分类筛选
                    _categoryManager.ApplyCategoryFilter();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示管理分类对话框时出错: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ShowDeleteConfirmationDialogAsync(string title, string content)
        {
            var confirmDialog = new ContentDialog()
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "删除",
                SecondaryButtonText = "取消",
                XamlRoot = _page.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        public async Task ShowInfoDialogAsync(string content)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "信息",
                    Content = content,
                    CloseButtonText = "确定",
                    XamlRoot = _page.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示信息对话框时出错: {ex.Message}");
            }
        }

        public async Task ShowErrorDialogAsync(string message)
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
                    XamlRoot = _page.XamlRoot
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
    }
}
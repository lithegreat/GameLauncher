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
    /// ��Ϸ�Ի��������
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
                PlaceholderText = "������Ϸ����",
                Margin = new Thickness(0, 0, 0, 10)
            };

            var pathBox = new TextBox()
            {
                PlaceholderText = "��Ϸ��ִ���ļ�·��",
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 5, 0)
            };

            var browseButton = new Button()
            {
                Content = "���..."
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
            contentPanel.Children.Add(new TextBlock() { Text = "��Ϸ����:", Margin = new Thickness(0, 0, 0, 5) });
            contentPanel.Children.Add(gameNameBox);
            contentPanel.Children.Add(new TextBlock() { Text = "��ִ���ļ�·��:", Margin = new Thickness(0, 10, 0, 5) });
            contentPanel.Children.Add(pathPanel);
            contentPanel.Children.Add(new TextBlock() { Text = "��Ϸ����:", Margin = new Thickness(0, 10, 0, 5) });
            contentPanel.Children.Add(categoryComboBox);

            var dialog = new ContentDialog()
            {
                Title = "�����Ϸ",
                Content = contentPanel,
                PrimaryButtonText = "ȷ��",
                SecondaryButtonText = "ȡ��",
                XamlRoot = _page.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(gameNameBox.Text))
                {
                    await ShowErrorDialogAsync("��������Ϸ����");
                    return;
                }

                if (string.IsNullOrWhiteSpace(pathBox.Text))
                {
                    await ShowErrorDialogAsync("��ѡ����Ϸ��ִ���ļ�");
                    return;
                }

                if (!File.Exists(pathBox.Text))
                {
                    await ShowErrorDialogAsync("ָ�����ļ�������");
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
                    Category = selectedCategory?.Name ?? "δ����",
                    DisplayOrder = _gameDataManager.GetNextDisplayOrder()
                };

                _gameDataManager.AddGame(gameData);
                await _gameDataManager.SaveGamesDataAsync();

                // ȷ��UI����������ʾ����ӵ���Ϸ
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
                    Text = $"Ϊ��Ϸ \"{game.Title}\" ѡ�����:",
                    Margin = new Thickness(0, 0, 0, 10)
                });
                contentPanel.Children.Add(categoryComboBox);

                var dialog = new ContentDialog()
                {
                    Title = "������Ϸ����",
                    Content = contentPanel,
                    PrimaryButtonText = "ȷ��",
                    SecondaryButtonText = "ȡ��",
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
                System.Diagnostics.Debug.WriteLine($"��ʾ���÷���Ի���ʱ����: {ex.Message}");
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

                // �Ի���رպ�ˢ�·�����ص�UI
                if (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
                {
                    // ���¼���������Ϸ����
                    _categoryManager.UpdateCategoryGameCounts();

                    // �����ǰѡ�еķ��౻ɾ���ˣ��л���"ȫ����Ϸ"
                    if (_categoryManager.SelectedCategory != null && 
                        !CategoryService.Instance.Categories.Contains(_categoryManager.SelectedCategory))
                    {
                        _categoryManager.SelectedCategory = CategoryService.Instance.Categories.FirstOrDefault(c => c.Id == "all");
                    }

                    // ����Ӧ�÷���ɸѡ
                    _categoryManager.ApplyCategoryFilter();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ʾ�������Ի���ʱ����: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ShowDeleteConfirmationDialogAsync(string title, string content)
        {
            var confirmDialog = new ContentDialog()
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "ɾ��",
                SecondaryButtonText = "ȡ��",
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
                    Title = "��Ϣ",
                    Content = content,
                    CloseButtonText = "ȷ��",
                    XamlRoot = _page.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ʾ��Ϣ�Ի���ʱ����: {ex.Message}");
            }
        }

        public async Task ShowErrorDialogAsync(string message)
        {
            try
            {
                // ��ֹ�ظ���ʾ����Ի���
                if (_isErrorDialogShowing)
                {
                    System.Diagnostics.Debug.WriteLine($"����Ի���������ʾ�У�����: {message}");
                    return;
                }

                _isErrorDialogShowing = true;
                System.Diagnostics.Debug.WriteLine($"��ʾ����Ի���: {message}");

                var dialog = new ContentDialog()
                {
                    Title = "����",
                    Content = message ?? "����δ֪����",
                    CloseButtonText = "ȷ��",
                    XamlRoot = _page.XamlRoot
                };

                await dialog.ShowAsync();
                System.Diagnostics.Debug.WriteLine("����Ի���ر�");
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
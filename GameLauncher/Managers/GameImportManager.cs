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
    /// ��Ϸ���������������Steam��Xbox��Ϸ����
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
                Debug.WriteLine("��ʼ���� Steam ��Ϸ");

                // ��� Steam �Ƿ�װ
                if (!SteamService.IsSteamInstalled())
                {
                    await _dialogManager.ShowErrorDialogAsync("δ��⵽ Steam ��װ����ȷ�� Steam ����ȷ��װ��");
                    return;
                }

                // ��ʾ���ȶԻ���
                var progressDialog = new ContentDialog()
                {
                    Title = "���� Steam ��Ϸ",
                    Content = "����ɨ�� Steam ��Ϸ�⣬���Ժ�...",
                    XamlRoot = _page.XamlRoot
                };

                // �첽��ʾ�Ի��򲢿�ʼɨ��
                _ = progressDialog.ShowAsync();

                try
                {
                    // ɨ�� Steam ��Ϸ
                    var steamGames = await SteamService.ScanSteamGamesAsync();

                    // �رս��ȶԻ���
                    progressDialog.Hide();

                    if (steamGames.Count == 0)
                    {
                        await _dialogManager.ShowErrorDialogAsync("δ�ҵ��Ѱ�װ�� Steam ��Ϸ��");
                        return;
                    }

                    // ���˵��Ѿ����ڵ���Ϸ
                    var newGames = FilterNewSteamGames(steamGames);

                    if (newGames.Count == 0)
                    {
                        await _dialogManager.ShowErrorDialogAsync("���� Steam ��Ϸ���ѵ��롣");
                        return;
                    }

                    Debug.WriteLine($"�ҵ� {newGames.Count} ���µ� Steam ��Ϸ������");

                    // ��ʾѡ��Ի���
                    var selectedGames = await ShowSteamGameSelectionDialogAsync(newGames);
                    if (selectedGames.Count > 0)
                    {
                        await ImportSelectedSteamGamesAsync(selectedGames);
                        await _dialogManager.ShowInfoDialogAsync($"�ɹ����� {selectedGames.Count} �� Steam ��Ϸ��");
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
                Debug.WriteLine($"���� Steam ��Ϸʱ�����쳣: {ex.Message}");
                await _dialogManager.ShowErrorDialogAsync($"���� Steam ��Ϸʱ����: {ex.Message}");
            }
        }

        public async Task ImportXboxGamesAsync()
        {
            try
            {
                Debug.WriteLine("��ʼ���� Xbox ��Ϸ");

                // ��ʾ���ȶԻ���
                var progressDialog = new ContentDialog()
                {
                    Title = "���� Xbox ��Ϸ",
                    Content = "����ɨ�� Xbox ��Ϸ�⣬���Ժ�...",
                    XamlRoot = _page.XamlRoot
                };

                // �첽��ʾ�Ի��򲢿�ʼɨ��
                _ = progressDialog.ShowAsync();

                try
                {
                    // ɨ�� Xbox ��Ϸ
                    var xboxGames = await XboxService.ScanXboxGamesAsync();

                    // �رս��ȶԻ���
                    progressDialog.Hide();

                    if (xboxGames.Count == 0)
                    {
                        await _dialogManager.ShowErrorDialogAsync("δ�ҵ��Ѱ�װ�� Xbox ��Ϸ��");
                        return;
                    }

                    // ���˵��Ѿ����ڵ���Ϸ
                    var newGames = FilterNewXboxGames(xboxGames);

                    if (newGames.Count == 0)
                    {
                        await _dialogManager.ShowErrorDialogAsync("���� Xbox ��Ϸ���ѵ��롣");
                        return;
                    }

                    Debug.WriteLine($"�ҵ� {newGames.Count} ���µ� Xbox ��Ϸ������");

                    // ��ʾѡ��Ի���
                    var selectedGames = await ShowXboxGameSelectionDialogAsync(newGames);
                    if (selectedGames.Count > 0)
                    {
                        await ImportSelectedXboxGamesAsync(selectedGames);
                        await _dialogManager.ShowInfoDialogAsync($"�ɹ����� {selectedGames.Count} �� Xbox ��Ϸ��");
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
                Debug.WriteLine($"���� Xbox ��Ϸʱ�����쳣: {ex.Message}");
                await _dialogManager.ShowErrorDialogAsync($"���� Xbox ��Ϸʱ����: {ex.Message}");
            }
        }

        private List<SteamGame> FilterNewSteamGames(List<SteamGame> steamGames)
        {
            // ���˵��Ѿ����ڵ���Ϸ - ʹ�ö��ؼ������ظ�
            var existingAppIds = _gameDataManager.Items.Where(item => item.IsSteamGame)
                                    .Select(item => item.SteamAppId)
                                    .Where(appId => !string.IsNullOrEmpty(appId))
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // ͬʱ����ִ���ļ�·��������·����Сд���µ��ظ�
            var existingPaths = _gameDataManager.Items.Where(item => !string.IsNullOrEmpty(item.ExecutablePath))
                                   .Select(item => Path.GetFullPath(item.ExecutablePath).ToLowerInvariant())
                                   .ToHashSet();

            return steamGames.Where(game =>
            {
                // ��� AppID �Ƿ��Ѵ���
                if (existingAppIds.Contains(game.AppId))
                {
                    Debug.WriteLine($"�����ظ��� Steam AppID: {game.AppId} - {game.Name}");
                    return false;
                }

                // ����ִ���ļ�·���Ƿ��Ѵ��ڣ���׼��·���Ƚϣ�
                if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                {
                    var normalizedPath = Path.GetFullPath(game.ExecutablePath).ToLowerInvariant();
                    if (existingPaths.Contains(normalizedPath))
                    {
                        Debug.WriteLine($"�����ظ��Ŀ�ִ���ļ�·��: {game.ExecutablePath} - {game.Name}");
                        return false;
                    }
                }

                return true;
            }).ToList();
        }

        private List<XboxGame> FilterNewXboxGames(List<XboxGame> xboxGames)
        {
            // ���˵��Ѿ����ڵ���Ϸ - ʹ�ö��ؼ������ظ�
            var existingPackageNames = _gameDataManager.Items.Where(item => item.IsXboxGame)
                                          .Select(item => item.XboxPackageFamilyName)
                                          .Where(name => !string.IsNullOrEmpty(name))
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // ͬʱ����ִ���ļ�·�����ⲻͬ·�����ظ�
            var existingPaths = _gameDataManager.Items.Where(item => !string.IsNullOrEmpty(item.ExecutablePath))
                                   .Select(item => Path.GetFullPath(item.ExecutablePath).ToLowerInvariant())
                                   .ToHashSet();

            return xboxGames.Where(game =>
            {
                // ��� Package Family Name �Ƿ��Ѵ���
                if (existingPackageNames.Contains(game.PackageFamilyName))
                {
                    Debug.WriteLine($"�����ظ��� Xbox Package: {game.PackageFamilyName} - {game.Name}");
                    return false;
                }

                // ����ִ���ļ�·���Ƿ��Ѵ��ڣ���׼��·���Ƚϣ�
                if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                {
                    var normalizedPath = Path.GetFullPath(game.ExecutablePath).ToLowerInvariant();
                    if (existingPaths.Contains(normalizedPath))
                    {
                        Debug.WriteLine($"�����ظ��Ŀ�ִ���ļ�·��: {game.ExecutablePath} - {game.Name}");
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
                // ����һ����װ�����ڰ�
                var gameSelectionItems = steamGames.Select(game => new GameSelectionItem<SteamGame>
                {
                    Game = game,
                    IsSelected = true, // Ĭ��ȫѡ
                    DisplayName = game.Name
                }).ToList();

                var selectedItems = await ShowGameSelectionDialogAsync(
                    "ѡ�� Steam ��Ϸ",
                    $"�ҵ� {steamGames.Count} ���µ� Steam ��Ϸ����ѡ��Ҫ�������Ϸ:",
                    "����ѡ�е���Ϸ",
                    gameSelectionItems);

                selectedGames.AddRange(selectedItems.Select(item => item.Game));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"��ʾ Steam ��Ϸѡ��Ի���ʱ����: {ex.Message}");
            }

            return selectedGames;
        }

        private async Task<List<XboxGame>> ShowXboxGameSelectionDialogAsync(List<XboxGame> xboxGames)
        {
            var selectedGames = new List<XboxGame>();

            try
            {
                // ����һ����װ�����ڰ�
                var gameSelectionItems = xboxGames.Select(game => new GameSelectionItem<XboxGame>
                {
                    Game = game,
                    IsSelected = true, // Ĭ��ȫѡ
                    DisplayName = game.Name
                }).ToList();

                var selectedItems = await ShowGameSelectionDialogAsync(
                    "ѡ�� Xbox ��Ϸ",
                    $"�ҵ� {xboxGames.Count} ���µ� Xbox ��Ϸ����ѡ��Ҫ�������Ϸ:",
                    "����ѡ�е���Ϸ",
                    gameSelectionItems);

                selectedGames.AddRange(selectedItems.Select(item => item.Game));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"��ʾ Xbox ��Ϸѡ��Ի���ʱ����: {ex.Message}");
            }

            return selectedGames;
        }

        private async Task<List<GameSelectionItem<T>>> ShowGameSelectionDialogAsync<T>(
            string title, string description, string confirmText, 
            List<GameSelectionItem<T>> gameSelectionItems)
        {
            var selectedItems = new List<GameSelectionItem<T>>();

            // ������Ϸѡ���б�
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

                // ��ѡ��״̬
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

            // ����������ť
            var buttonPanel = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var selectAllButton = new Button()
            {
                Content = "ȫѡ"
            };

            var deselectAllButton = new Button()
            {
                Content = "ȫ��ѡ"
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
                SecondaryButtonText = "ȡ��",
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
                    Debug.WriteLine($"���� Steam ��Ϸ: {steamGame.Name}");

                    // ������ȡͼ��
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
                        CategoryId = "uncategorized", // Steam ��ϷĬ��Ϊδ����
                        Category = "δ����"
                    };

                    _gameDataManager.AddGame(gameData);
                }

                // ��������
                await _gameDataManager.SaveGamesDataAsync();
                _categoryManager.ApplyCategoryFilter();
                _categoryManager.UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"����ѡ���� Steam ��Ϸʱ����: {ex.Message}");
                throw;
            }
        }

        private async Task ImportSelectedXboxGamesAsync(List<XboxGame> xboxGames)
        {
            try
            {
                foreach (var xboxGame in xboxGames)
                {
                    Debug.WriteLine($"���� Xbox ��Ϸ: {xboxGame.Name}");

                    // ������ȡͼ��
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
                        CategoryId = "uncategorized", // Xbox ��ϷĬ��Ϊδ����
                        Category = "δ����"
                    };

                    _gameDataManager.AddGame(gameData);
                }

                // ��������
                await _gameDataManager.SaveGamesDataAsync();
                _categoryManager.ApplyCategoryFilter();
                _categoryManager.UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"����ѡ�е� Xbox ��Ϸʱ����: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ��Ϸѡ�������
        /// </summary>
        private class GameSelectionItem<T>
        {
            public T Game { get; set; } = default(T)!;
            public bool IsSelected { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}
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
using Ellipse = Microsoft.UI.Xaml.Shapes.Ellipse;

namespace GameLauncher.Pages
{
    public sealed partial class GamesPage : Page
    {
        public ObservableCollection<CustomDataObject> Items { get; } = new ObservableCollection<CustomDataObject>();
        public ObservableCollection<CustomDataObject> FilteredItems { get; } = new ObservableCollection<CustomDataObject>();
        public ObservableCollection<GameCategory> Categories => CategoryService.Instance.Categories;
        
        private const string GamesDataFileName = "games.json";
        private bool _isDeleteMode = false;
        private CustomDataObject? _contextMenuGame = null;
        private GameCategory? _selectedCategory = null;

        public GamesPage()
        {
            this.InitializeComponent();
            this.Loaded += GamesPage_Loaded;
        }

        private async void GamesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CategoryService.Instance.LoadCategoriesAsync();
            await LoadGamesData();
            
            // Ĭ��ѡ��"ȫ����Ϸ"
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
            // ҳ���뿪ʱ���浱ǰ����Ϸ˳��
            await SaveCurrentGameOrder();
        }

        private void ApplyCategoryFilter()
        {
            FilteredItems.Clear();
            
            if (_selectedCategory == null)
            {
                // ���û��ѡ����࣬��ʾ������Ϸ
                foreach (var item in Items)
                {
                    FilteredItems.Add(item);
                }
            }
            else if (_selectedCategory.Id == "all")
            {
                // ��ʾ������Ϸ
                foreach (var item in Items)
                {
                    FilteredItems.Add(item);
                }
            }
            else if (_selectedCategory.Id == "uncategorized")
            {
                // ��ʾδ�������Ϸ
                foreach (var item in Items.Where(g => string.IsNullOrEmpty(g.CategoryId) || g.CategoryId == "uncategorized"))
                {
                    FilteredItems.Add(item);
                }
            }
            else
            {
                // ��ʾ�ض��������Ϸ
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

        #region Context Menu Event Handlers

        private void ContentGridView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("�Ҽ�����¼�����");
                
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
                    System.Diagnostics.Debug.WriteLine($"���������Ĳ˵���Ϸ: {game.Title}");
                    
                    // ������Ϸ���Ͷ�̬��ʾ�˵���
                    UpdateContextMenuForGame(game);
                    
                    // Context menu will show automatically due to ContextFlyout in XAML
                }
                else
                {
                    _contextMenuGame = null;
                    System.Diagnostics.Debug.WriteLine("δ�ҵ���Ϸ����������");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�Ҽ���������쳣: {ex.Message}");
                _contextMenuGame = null;
            }
        }

        private void UpdateContextMenuForGame(CustomDataObject game)
        {
            try
            {
                // ��ȡ�����Ĳ˵�
                if (Resources["GameContextMenu"] is MenuFlyout contextMenu)
                {
                    // ���� Steam ��صĲ˵���
                    var steamSeparator = contextMenu.Items.OfType<MenuFlyoutSeparator>().FirstOrDefault(x => x.Name == "SteamSeparator");
                    var openInSteamItem = contextMenu.Items.OfType<MenuFlyoutItem>().FirstOrDefault(x => x.Name == "OpenInSteamMenuItem");

                    if (steamSeparator != null && openInSteamItem != null)
                    {
                        // �����Ƿ�Ϊ Steam ��Ϸ��ʾ/���ز˵���
                        steamSeparator.Visibility = game.IsSteamGame ? Visibility.Visible : Visibility.Collapsed;
                        openInSteamItem.Visibility = game.IsSteamGame ? Visibility.Visible : Visibility.Collapsed;
                    }

                    // Xbox ��Ϸ����Ҫ����������Ĳ˵����Ϊ���ǿ���ͨ�������"����Ϸ"��������
                    // ���������Ҫ��� Xbox �ض��Ĺ��ܣ����� Xbox App �д򿪣����������������
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"���������Ĳ˵�ʱ�쳣: {ex.Message}");
            }
        }

        private async void OpenGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    System.Diagnostics.Debug.WriteLine($"����Ϸ: {_contextMenuGame.Title}");
                    await LaunchGame(_contextMenuGame);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("�����Ĳ˵���ϷΪ�գ��޷���");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"����Ϸ�˵����쳣: {ex.Message}");
                await ShowErrorDialog($"����Ϸʱ����: {ex.Message}");
            }
        }

        private async void DeleteGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    System.Diagnostics.Debug.WriteLine($"ɾ����Ϸ�˵�����: {_contextMenuGame.Title}");
                    await DeleteSingleGame(_contextMenuGame);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("�����Ĳ˵���ϷΪ�գ��޷�ɾ��");
                }
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɾ����Ϸ�˵����쳣: {ex.Message}");
                await ShowErrorDialog($"ɾ����Ϸʱ����: {ex.Message}");
            }
        }

        private async void OpenGameDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    System.Diagnostics.Debug.WriteLine($"����ϷĿ¼: {_contextMenuGame.Title}");
                    await OpenGameDirectory(_contextMenuGame);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("�����Ĳ˵���ϷΪ�գ��޷���Ŀ¼");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"����ϷĿ¼�˵����쳣: {ex.Message}");
                await ShowErrorDialog($"����ϷĿ¼ʱ����: {ex.Message}");
            }
        }

        private async void OpenInSteamMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null && _contextMenuGame.IsSteamGame)
                {
                    System.Diagnostics.Debug.WriteLine($"�� Steam �д���Ϸ: {_contextMenuGame.Title}");
                    
                    if (!string.IsNullOrEmpty(_contextMenuGame.SteamAppId))
                    {
                        // ʹ�� Steam �̵�ҳ�� URL
                        var steamStoreUrl = $"https://store.steampowered.com/app/{_contextMenuGame.SteamAppId}";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = steamStoreUrl,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        await ShowErrorDialog("�޷���ȡ��Ϸ�� Steam AppID");
                    }
                }
                else
                {
                    await ShowErrorDialog("����Ϸ���� Steam ��Ϸ");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�� Steam �д���Ϸ�쳣: {ex.Message}");
                await ShowErrorDialog($"�� Steam �д���Ϸʱ����: {ex.Message}");
            }
        }

        #endregion

        #region New Methods for Context Menu Actions

        private async Task DeleteSingleGame(CustomDataObject game)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"��ʼɾ����Ϸ: {game.Title}");
                
                var confirmDialog = new ContentDialog()
                {
                    Title = "ȷ��ɾ��",
                    Content = $"ȷ��Ҫɾ����Ϸ \"{game.Title}\" �𣿴˲����޷�������",
                    PrimaryButtonText = "ɾ��",
                    SecondaryButtonText = "ȡ��",
                    XamlRoot = this.XamlRoot
                };

                System.Diagnostics.Debug.WriteLine("��ʾȷ�϶Ի���");
                var result = await confirmDialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    System.Diagnostics.Debug.WriteLine("�û�ȷ��ɾ������ʼִ��ɾ������");
                    
                    // ȷ����Ϸ�ڼ����д���
                    if (Items.Contains(game))
                    {
                        Items.Remove(game);
                        System.Diagnostics.Debug.WriteLine("��Ϸ�ѴӼ������Ƴ�");
                        
                        // ������º������
                        await SaveGamesData();
                        ApplyCategoryFilter();
                        UpdateCategoryGameCounts();
                        System.Diagnostics.Debug.WriteLine("��Ϸ���ݱ������");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("���棺��Ϸ���ڼ�����");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("�û�ȡ��ɾ������");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ɾ����Ϸʱ�����쳣: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"�쳣��ջ: {ex.StackTrace}");
                await ShowErrorDialog($"ɾ����Ϸʱ����: {ex.Message}");
            }
        }

        private async Task OpenGameDirectory(CustomDataObject game)
        {
            try
            {
                if (string.IsNullOrEmpty(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
                {
                    await ShowErrorDialog("��Ϸ�ļ������ڣ��޷���Ŀ¼");
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
                    await ShowErrorDialog("��ϷĿ¼������");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"����ϷĿ¼ʱ����: {ex.Message}");
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
                await ShowErrorDialog($"�����Ϸʱ����: {ex.Message}");
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
                await ShowErrorDialog($"�����ظ���Ϸʱ����: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine("ȡ��ɾ��ģʽ");
                
                // ȷ���� UI �߳���ִ��
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        _isDeleteMode = false;
                        UpdateDeleteModeUI();
                        System.Diagnostics.Debug.WriteLine("ɾ��ģʽ��ȡ��");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ȡ��ɾ��ģʽʱUI�쳣: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ȡ��ɾ����ť�쳣: {ex.Message}");
                
                // ǿ���˳�ɾ��ģʽ
                _isDeleteMode = false;
            }
        }

        private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("��ʼ����ɾ����Ϸ");
                
                // ��ȫ�ػ�ȡѡ�е���Ŀ
                var selectedItems = new List<CustomDataObject>();
                
                // ȷ���� UI �߳���ִ��
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
                        System.Diagnostics.Debug.WriteLine($"ѡ���� {selectedItems.Count} ����Ϸ");
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"��ȡѡ����Ŀʱ�쳣: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                
                await tcs.Task;
                
                if (selectedItems.Count == 0)
                {
                    await ShowErrorDialog("��ѡ��Ҫɾ������Ϸ");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("��ʾ����ɾ��ȷ�϶Ի���");
                
                var confirmDialog = new ContentDialog()
                {
                    Title = "ȷ��ɾ��",
                    Content = $"ȷ��Ҫɾ�� {selectedItems.Count} ����Ϸ�𣿴˲����޷�������",
                    PrimaryButtonText = "ɾ��",
                    SecondaryButtonText = "ȡ��",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    System.Diagnostics.Debug.WriteLine("�û�ȷ������ɾ������ʼִ��ɾ������");
                    
                    // �� UI �߳��ϰ�ȫ��ɾ����Ŀ
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
                                    System.Diagnostics.Debug.WriteLine($"ɾ����Ϸ: {item.Title}");
                                }
                            }
                            

                            System.Diagnostics.Debug.WriteLine("����ɾ����ɣ��˳�ɾ��ģʽ");
                            
                            // Exit delete mode
                            _isDeleteMode = false;
                            UpdateDeleteModeUI();
                            
                            deleteTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"UI�߳�ɾ�������쳣: {ex.Message}");
                            deleteTcs.SetException(ex);
                        }
                    });
                    
                    await deleteTcs.Task;
                    
                    // Save the updated data
                    await SaveGamesData();
                    ApplyCategoryFilter();
                    UpdateCategoryGameCounts();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("�û�ȡ������ɾ������");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"����ɾ����Ϸʱ�����쳣: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"�쳣��ջ: {ex.StackTrace}");
                
                // ȷ���˳�ɾ��ģʽ
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
                            System.Diagnostics.Debug.WriteLine($"����UI�쳣: {updateEx.Message}");
                        }
                    });
                }
                catch
                {
                    // ����Ƕ���쳣
                }
                
                await ShowErrorDialog($"ɾ����Ϸʱ����: {ex.Message}");
            }
        }

        #endregion

        #region Steam Import Methods

        private async Task ImportSteamGames()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("��ʼ���� Steam ��Ϸ");

                // ��� Steam �Ƿ�װ
                if (!SteamService.IsSteamInstalled())
                {
                    await ShowErrorDialog("δ��⵽ Steam ��װ����ȷ�� Steam ����ȷ��װ��");
                    return;
                }

                // ��ʾ���ȶԻ���
                var progressDialog = new ContentDialog()
                {
                    Title = "���� Steam ��Ϸ",
                    Content = "����ɨ�� Steam ��Ϸ�⣬���Ժ�...",
                    XamlRoot = this.XamlRoot
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
                        await ShowErrorDialog("δ�ҵ��Ѱ�װ�� Steam ��Ϸ��");
                        return;
                    }

                    // ���˵��Ѿ����ڵ���Ϸ - ʹ�ö��ؼ������ظ�
                    var existingAppIds = Items.Where(item => item.IsSteamGame)
                                            .Select(item => item.SteamAppId)
                                            .Where(appId => !string.IsNullOrEmpty(appId))
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // ͬʱ����ִ���ļ�·��������·����Сд���µ��ظ�
                    var existingPaths = Items.Where(item => !string.IsNullOrEmpty(item.ExecutablePath))
                                           .Select(item => Path.GetFullPath(item.ExecutablePath).ToLowerInvariant())
                                           .ToHashSet();

                    var newGames = steamGames.Where(game => 
                    {
                        // ��� AppID �Ƿ��Ѵ���
                        if (existingAppIds.Contains(game.AppId))
                        {
                            System.Diagnostics.Debug.WriteLine($"�����ظ��� Steam AppID: {game.AppId} - {game.Name}");
                            return false;
                        }

                        // ����ִ���ļ�·���Ƿ��Ѵ��ڣ���׼��·���Ƚϣ�'
                        if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                        {
                            var normalizedPath = Path.GetFullPath(game.ExecutablePath).ToLowerInvariant();
                            if (existingPaths.Contains(normalizedPath))
                            {
                                System.Diagnostics.Debug.WriteLine($"�����ظ��Ŀ�ִ���ļ�·��: {game.ExecutablePath} - {game.Name}");
                                return false;
                            }
                        }

                        return true;
                    }).ToList();

                    if (newGames.Count == 0)
                    {
                        await ShowErrorDialog("���� Steam ��Ϸ���ѵ��롣");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"�ҵ� {newGames.Count} ���µ� Steam ��Ϸ������");

                    // ��ʾѡ��Ի���
                    var selectedGames = await ShowSteamGameSelectionDialog(newGames);
                    if (selectedGames.Count > 0)
                    {
                        await ImportSelectedSteamGames(selectedGames);
                        await ShowInfoDialog($"�ɹ����� {selectedGames.Count} �� Steam ��Ϸ��");
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
                System.Diagnostics.Debug.WriteLine($"���� Steam ��Ϸʱ�����쳣: {ex.Message}");
                await ShowErrorDialog($"���� Steam ��Ϸʱ����: {ex.Message}");
            }
        }

        private async Task<List<SteamGame>> ShowSteamGameSelectionDialog(List<SteamGame> steamGames)
        {
            var selectedGames = new List<SteamGame>();

            try
            {
                // ����һ����װ�����ڰ�
                var gameSelectionItems = steamGames.Select(game => new GameSelectionItem
                {
                    Game = game,
                    IsSelected = true, // Ĭ��ȫѡ
                    DisplayName = game.Name
                }).ToList();

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
                    Text = $"�ҵ� {steamGames.Count} ���µ� Steam ��Ϸ����ѡ��Ҫ�������Ϸ:",
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
                    Title = "ѡ�� Steam ��Ϸ",
                    Content = contentPanel,
                    PrimaryButtonText = "����ѡ�е���Ϸ",
                    SecondaryButtonText = "ȡ��",
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
                System.Diagnostics.Debug.WriteLine($"��ʾ Steam ��Ϸѡ��Ի���ʱ����: {ex.Message}");
            }

            return selectedGames;
        }

        private async Task ImportSelectedSteamGames(List<SteamGame> steamGames)
        {
            try
            {
                foreach (var steamGame in steamGames)
                {
                    System.Diagnostics.Debug.WriteLine($"���� Steam ��Ϸ: {steamGame.Name}");

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

                    Items.Add(gameData);
                }

                // ��������
                await SaveGamesData();
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"����ѡ���� Steam ��Ϸʱ����: {ex.Message}");
                throw;
            }
        }

        private async Task ShowInfoDialog(string message)
        {
            try
            {
                var dialog = new ContentDialog()
                {
                    Title = "��Ϣ",
                    Content = message,
                    CloseButtonText = "ȷ��",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ʾ��Ϣ�Ի���ʱ����: {ex.Message}");
            }
        }

        private async Task CleanDuplicateGames()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("��ʼ�����ظ���Ϸ");

                var duplicates = new List<CustomDataObject>();
                var uniqueGames = new Dictionary<string, CustomDataObject>();

                foreach (var game in Items.ToList())
                {
                    string uniqueKey = "";

                    // ���� Steam ��Ϸ��ʹ�� AppID ��ΪΨһ��ʶ
                    if (game.IsSteamGame && !string.IsNullOrEmpty(game.SteamAppId))
                    {
                        uniqueKey = $"steam_{game.SteamAppId}";
                    }
                    // ���� Xbox ��Ϸ��ʹ�� Package Family Name ��ΪΨһ��ʶ
                    else if (game.IsXboxGame && !string.IsNullOrEmpty(game.XboxPackageFamilyName))
                    {
                        uniqueKey = $"xbox_{game.XboxPackageFamilyName}";
                    }
                    // ���ڷ� Steam/Xbox ��Ϸ����û�� AppID/Package Name ����Ϸ��ʹ�ñ�׼���Ŀ�ִ���ļ�·��
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
                    // ʹ����Ϸ������Ϊ���Ψһ��ʶ�����Ƽ�������Ϊ��ѡ�� 
                    else 
                    {
                        uniqueKey = $"name_{game.Title.ToLowerInvariant()}";
                    }

                    if (uniqueGames.ContainsKey(uniqueKey))
                    {
                        // �����ظ���Ϸ
                        var existingGame = uniqueGames[uniqueKey];
                        System.Diagnostics.Debug.WriteLine($"�����ظ���Ϸ: {game.Title} (����: {existingGame.Title})");
                        
                        // ѡ��������������Ϸ��Ϣ
                        if (ShouldKeepGame(game, existingGame))
                        {
                            duplicates.Add(existingGame);
                            uniqueGames[uniqueKey] = game;
                            System.Diagnostics.Debug.WriteLine($"��������Ϸ: {game.Title}");
                        }
                        else
                        {
                            duplicates.Add(game);
                            System.Diagnostics.Debug.WriteLine($"����������Ϸ: {existingGame.Title}");
                        }
                    }
                    else
                    {
                        uniqueGames[uniqueKey] = game;
                    }
                }

                if (duplicates.Count == 0)
                {
                    await ShowInfoDialog("δ�����ظ�����Ϸ��");
                    return;
                }

                // ��ʾȷ�϶Ի���
                var gameListText = string.Join("\n", duplicates.Take(5).Select(g => $"? {g.Title}"));
                if (duplicates.Count > 5)
                {
                    gameListText += $"\n... ���� {duplicates.Count - 5} ��";
                }

                var confirmDialog = new ContentDialog()
                {
                    Title = "�����ظ���Ϸ",
                    Content = $"���� {duplicates.Count} ���ظ�����Ϸ���Ƿ�Ҫɾ����Щ�ظ��\n\n��Ҫɾ������Ϸ:\n{gameListText}",
                    PrimaryButtonText = "ɾ���ظ���",
                    SecondaryButtonText = "ȡ��",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // ɾ���ظ�����Ϸ
                    foreach (var duplicate in duplicates)
                    {
                        Items.Remove(duplicate);
                        System.Diagnostics.Debug.WriteLine($"ɾ���ظ���Ϸ: {duplicate.Title}");
                    }

                    // ������º������
                    await SaveGamesData();

                    await ShowInfoDialog($"�ɹ������� {duplicates.Count} ���ظ�����Ϸ��");
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"�����ظ���Ϸʱ�����쳣: {ex.Message}");
                throw;
            }
        }

        private bool ShouldKeepGame(CustomDataObject newGame, CustomDataObject existingGame)
        {
            // ���ȱ�����ͼ�����Ϸ
            if (newGame.IconImage != null && existingGame.IconImage == null)
                return true;
            if (newGame.IconImage == null && existingGame.IconImage != null)
                return false;

            // ���ȱ��� Steam ��Ϸ���� AppID �ģ�
            if (newGame.IsSteamGame && !string.IsNullOrEmpty(newGame.SteamAppId) && 
                (!existingGame.IsSteamGame || string.IsNullOrEmpty(existingGame.SteamAppId)))
                return true;
            if (existingGame.IsSteamGame && !string.IsNullOrEmpty(existingGame.SteamAppId) && 
                (!newGame.IsSteamGame || string.IsNullOrEmpty(newGame.SteamAppId)))
                return false;

            // ���ȱ��� Xbox ��Ϸ���� Package Family Name �ģ�
            if (newGame.IsXboxGame && !string.IsNullOrEmpty(newGame.XboxPackageFamilyName) && 
                (!existingGame.IsXboxGame || string.IsNullOrEmpty(existingGame.XboxPackageFamilyName)))
                return true;
            if (existingGame.IsXboxGame && !string.IsNullOrEmpty(existingGame.XboxPackageFamilyName) && 
                (!newGame.IsXboxGame || string.IsNullOrEmpty(newGame.XboxPackageFamilyName)))
                return false;

            // ���ȱ�������Ч��ִ���ļ�·�����ļ����ڵ���Ϸ
            bool newGameHasValidPath = !string.IsNullOrEmpty(newGame.ExecutablePath) && File.Exists(newGame.ExecutablePath);
            bool existingGameHasValidPath = !string.IsNullOrEmpty(existingGame.ExecutablePath) && File.Exists(existingGame.ExecutablePath);

            if (newGameHasValidPath && !existingGameHasValidPath)
                return true;
            if (!newGameHasValidPath && existingGameHasValidPath)
                return false;

            // Ĭ�ϱ������е���Ϸ
            return false;
        }

        #endregion
        
        private void UpdateDeleteModeUI()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"����ɾ��ģʽUI: _isDeleteMode = {_isDeleteMode}");
                
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
                    ImportGamesDropDownButton.Visibility = Visibility.Collapsed;
                    AddGameButton.Visibility = Visibility.Collapsed;
                    
                    // Initialize delete button state
                    DeleteSelectedButton.IsEnabled = false;
                    
                    System.Diagnostics.Debug.WriteLine("����ɾ��ģʽ");
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
                        System.Diagnostics.Debug.WriteLine($"����ѡ��ʱ�쳣: {ex.Message}");
                    }
                    
                    ContentGridView.SelectionMode = ListViewSelectionMode.None;
                    ContentGridView.IsItemClickEnabled = true;
                    
                    // Hide delete mode buttons
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    
                    // Show normal mode buttons
                    DeleteModeButton.Visibility = Visibility.Visible;
                    CleanDuplicateGamesButton.Visibility = Visibility.Visible;
                    ImportGamesDropDownButton.Visibility = Visibility.Visible;
                    AddGameButton.Visibility = Visibility.Visible;
                    
                    System.Diagnostics.Debug.WriteLine("�˳�ɾ��ģʽ");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateDeleteModeUI �쳣: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"�쳣��ջ: {ex.StackTrace}");
                
                // ����ָ���ǿ�����õ���ȫ״̬
                try
                {
                    _isDeleteMode = false;
                    ContentGridView.SelectionMode = ListViewSelectionMode.None;
                    ContentGridView.IsItemClickEnabled = true;
                    
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    DeleteModeButton.Visibility = Visibility.Visible;
                    CleanDuplicateGamesButton.Visibility = Visibility.Visible;
                    ImportGamesDropDownButton.Visibility = Visibility.Visible;
                    AddGameButton.Visibility = Visibility.Visible;
                }
                catch
                {
                    // ����Ƕ���쳣
                }
            }
        }

        private async Task ShowAddGameDialog()
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
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(gameNameBox.Text))
                {
                    await ShowErrorDialog("��������Ϸ����");
                    return;
                }

                if (string.IsNullOrWhiteSpace(pathBox.Text))
                {
                    await ShowErrorDialog("��ѡ����Ϸ��ִ���ļ�");
                    return;
                }

                if (!File.Exists(pathBox.Text))
                {
                    await ShowErrorDialog("ָ�����ļ�������");
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
                    Category = selectedCategory?.Name ?? "δ����"
                };

                Items.Add(gameData);
                await SaveGamesData();
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
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
                            System.Diagnostics.Debug.WriteLine($"ѡ����: {selectedCount} ����Ŀ��ѡ��");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"����ɾ����ť״̬ʱ�쳣: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContentGridView_SelectionChanged �쳣: {ex.Message}");
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
                await ShowErrorDialog($"������Ϸʱ����: {ex.Message}");
            }
        }

        private async Task LaunchGame(CustomDataObject game)
        {
            try
            {
                if (game == null)
                {
                    await ShowErrorDialog("��Ϸ������Ч");
                    return;
                }

                // ����� Steam ��Ϸ������ʹ�� Steam Э������
                if (game.IsSteamGame && !string.IsNullOrEmpty(game.SteamAppId))
                {
                    System.Diagnostics.Debug.WriteLine($"ͨ�� Steam ������Ϸ: {game.Title} (AppID: {game.SteamAppId})");
                    
                    if (SteamService.LaunchSteamGame(game.SteamAppId))
                    {
                        return; // Steam �����ɹ�
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Steam ����ʧ�ܣ�����ֱ�����п�ִ���ļ�");
                    }
                }

                // ����� Xbox ��Ϸ������ʹ�� Xbox Э������
                if (game.IsXboxGame && !string.IsNullOrEmpty(game.XboxPackageFamilyName))
                {
                    System.Diagnostics.Debug.WriteLine($"ͨ�� Xbox ������Ϸ: {game.Title} (Package: {game.XboxPackageFamilyName})");
                    
                    if (XboxService.LaunchXboxGame(game.XboxPackageFamilyName))
                    {
                        return; // Xbox �����ɹ�
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Xbox ����ʧ�ܣ�����ֱ�����п�ִ���ļ�");
                        
                        // ����ͨ����ִ���ļ����� Xbox ��Ϸ
                        if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                        {
                            if (XboxService.LaunchXboxGameByExecutable(game.ExecutablePath))
                            {
                                return; // ͨ����ִ���ļ������ɹ�
                            }
                        }
                    }
                }

                // ֱ�����п�ִ���ļ��������� Steam ��Ϸ�� Steam ����ʧ��ʱ�ı�ѡ������
                if (string.IsNullOrEmpty(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
                {
                    await ShowErrorDialog($"��Ϸ�ļ�������: {game.ExecutablePath}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                System.Diagnostics.Debug.WriteLine($"ֱ��������Ϸ: {game.Title}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"������Ϸʧ��: {ex.Message}");
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
                            
                            // ������ʾ˳��Ȼ����ӵ�����
                            var sortedGames = gameDataList
                                .Where(g => g != null && !string.IsNullOrEmpty(g.Title))
                                .OrderBy(g => g.DisplayOrder)
                                .ToList();
                            
                            foreach (var gameJson in sortedGames)
                            {
                                BitmapImage? iconImage = null;
                                
                                // Ϊ�п�ִ���ļ�·������Ϸ��ȡͼ��
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
                                    Category = gameJson.Category ?? "δ����"
                                };
                                Items.Add(game);
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"�Ѽ��� {Items.Count} ����Ϸ������˳��ָ�");
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
                System.Diagnostics.Debug.WriteLine("��ʼ������Ϸ����");
                
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
                                           Category = item.Category
                                       }).ToList();
                
                System.Diagnostics.Debug.WriteLine($"׼������ {gameDataList.Count} ����Ϸ");
                
                var json = JsonSerializer.Serialize(gameDataList, new JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(file, json);
                
                System.Diagnostics.Debug.WriteLine("��Ϸ���ݱ���ɹ�");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"������Ϸ����ʱ�����쳣: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"�쳣��ջ: {ex.StackTrace}");
                
                // ��Ҫ�����׳��쳣�����Ǽ�¼���󲢳���֪ͨ�û�
                await ShowErrorDialog($"������Ϸ����ʧ��: {ex.Message}");
            }
        }

        private static bool _isErrorDialogShowing = false;

        private async Task ShowErrorDialog(string message)
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
                    XamlRoot = this.XamlRoot
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

        // ��Ӹ�����������Ϸѡ��
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
                // ����ÿ����Ϸ����ʾ˳��
                for (int i = 0; i < Items.Count; i++)
                {
                    Items[i].DisplayOrder = i;
                }
                
                // ���浽�ļ�
                await SaveGamesData();
                System.Diagnostics.Debug.WriteLine("��Ϸ˳���ѱ���");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"������Ϸ˳��ʱ����: {ex.Message}");
            }
        }

        private void ContentGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            try
            {
                // �϶�������ɺ󣬱������µ���Ϸ˳��
                _ = SaveCurrentGameOrder();
                System.Diagnostics.Debug.WriteLine("�϶�������ɣ���Ϸ˳���ѱ���");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�����϶�����¼�ʱ����: {ex.Message}");
            }
        }

        #region Category Management Methods

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is GameCategory selectedCategory)
            {
                _selectedCategory = selectedCategory;
                ApplyCategoryFilter();
            }
        }

        private async void ManageCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowCategoryManagementDialog();
        }

        private async Task ShowCategoryManagementDialog()
        {
            bool shouldContinue = true;
            
            while (shouldContinue)
            {
                try
                {
                    var categoriesListView = new ListView()
                    {
                        SelectionMode = ListViewSelectionMode.Single,
                        Height = 300
                    };

                    // �ֶ������б��ÿ���������ɫ������
                    var categories = Categories.Where(c => c.Id != "all" && c.Id != "uncategorized").ToList();
                    foreach (var category in categories)
                    {
                        var listViewItem = new ListViewItem()
                        {
                            Tag = category,
                            Padding = new Thickness(8)
                        };

                        var itemGrid = new Grid();
                        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        // ��ɫԲ��
                        var colorEllipse = new Ellipse()
                        {
                            Width = 16,
                            Height = 16,
                            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(ParseColor(category.Color)),
                            Margin = new Thickness(0, 0, 12, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(colorEllipse, 0);
                        itemGrid.Children.Add(colorEllipse);

                        // ��������
                        var nameText = new TextBlock()
                        {
                            Text = category.Name,
                            VerticalAlignment = VerticalAlignment.Center,
                            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
                        };
                        Grid.SetColumn(nameText, 1);
                        itemGrid.Children.Add(nameText);

                        listViewItem.Content = itemGrid;
                        categoriesListView.Items.Add(listViewItem);
                    }

                    var stackPanel = new StackPanel()
                    {
                        Spacing = 12
                    };

                    stackPanel.Children.Add(new TextBlock()
                    {
                        Text = "������Ϸ����",
                        Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
                    });

                    stackPanel.Children.Add(categoriesListView);

                    // ��ť���
                    var buttonPanel = new StackPanel()
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8
                    };

                    var addButton = new Button() { Content = "��ӷ���" };
                    var editButton = new Button() { Content = "�༭����", IsEnabled = false };
                    var deleteButton = new Button() { Content = "ɾ������", IsEnabled = false };

                    var actionRequested = "";
                    GameCategory? selectedCategoryForAction = null;

                    var dialog = new ContentDialog()
                    {
                        Title = "�������",
                        Content = stackPanel,
                        CloseButtonText = "���",
                        XamlRoot = this.XamlRoot
                    };

                    addButton.Click += async (s, e) =>
                    {
                        try
                        {
                            // �������ص�ǰ�Ի���
                            dialog.Hide();
                            // ��ʾ��ӷ���Ի���
                            await ShowAddCategoryDialog();
                            // �����Ҫ������ʾ����Ի���
                            actionRequested = "continue";
                        }
                        catch (Exception ex)
                        {
                            await ShowErrorDialog($"��ӷ���ʱ����: {ex.Message}");
                        }
                    };

                    categoriesListView.SelectionChanged += (s, e) =>
                    {
                        var hasSelection = categoriesListView.SelectedItem != null;
                        editButton.IsEnabled = hasSelection;
                        deleteButton.IsEnabled = hasSelection;
                    };

                    editButton.Click += async (s, e) =>
                    {
                        try
                        {
                            if (categoriesListView.SelectedItem is ListViewItem selectedItem && 
                                selectedItem.Tag is GameCategory category)
                            {
                                selectedCategoryForAction = category;
                                // �������ص�ǰ�Ի���
                                dialog.Hide();
                                // ��ʾ�༭����Ի���
                                await ShowEditCategoryDialog(selectedCategoryForAction);
                                // �����Ҫ������ʾ����Ի���
                                actionRequested = "continue";
                            }
                        }
                        catch (Exception ex)
                        {
                            await ShowErrorDialog($"�༭����ʱ����: {ex.Message}");
                        }
                    };

                    deleteButton.Click += async (s, e) =>
                    {
                        try
                        {
                            if (categoriesListView.SelectedItem is ListViewItem selectedItem && 
                                selectedItem.Tag is GameCategory category)
                            {
                                selectedCategoryForAction = category;
                                // �������ص�ǰ�Ի���
                                dialog.Hide();
                                // ��ʾɾ��ȷ�϶Ի���
                                await DeleteCategory(selectedCategoryForAction);
                                // �����Ҫ������ʾ����Ի���
                                actionRequested = "continue";
                            }
                        }
                        catch (Exception ex)
                        {
                            await ShowErrorDialog($"ɾ������ʱ����: {ex.Message}");
                        }
                    };

                    buttonPanel.Children.Add(addButton);
                    buttonPanel.Children.Add(editButton);
                    buttonPanel.Children.Add(deleteButton);

                    stackPanel.Children.Add(buttonPanel);

                    var result = await dialog.ShowAsync();
                    
                    // �����û�����Ĳ���
                    if (actionRequested == "continue")
                    {
                        // ������ʾ����Ի���
                        shouldContinue = true;
                        actionRequested = ""; // ����״̬
                    }
                    else
                    {
                        // �û��������ɣ��˳�ѭ��
                        shouldContinue = false;
                    }
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog($"��ʾ�������Ի���ʱ����: {ex.Message}");
                    shouldContinue = false;
                }
            }
            
            // ˢ�½���
            ApplyCategoryFilter();
            UpdateCategoryGameCounts();
        }

        private async Task ShowAddCategoryDialog()
        {
            var nameBox = new TextBox()
            {
                PlaceholderText = "�����������",
                Margin = new Thickness(0, 0, 0, 12)
            };

            // �������ӻ���ɫѡ����
            var colorSelectionPanel = CreateColorSelectionPanel();

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock() { Text = "��������:", Margin = new Thickness(0, 0, 0, 4) });
            stackPanel.Children.Add(nameBox);
            stackPanel.Children.Add(new TextBlock() { Text = "������ɫ:", Margin = new Thickness(0, 8, 0, 4) });
            stackPanel.Children.Add(colorSelectionPanel);

            var dialog = new ContentDialog()
            {
                Title = "��ӷ���",
                Content = stackPanel,
                PrimaryButtonText = "ȷ��",
                SecondaryButtonText = "ȡ��",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(nameBox.Text))
                    {
                        await ShowErrorDialog("�������������");
                        return;
                    }

                    var selectedColor = GetSelectedColorFromPanel(colorSelectionPanel);
                    await CategoryService.Instance.AddCategoryAsync(nameBox.Text, selectedColor);
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog($"��ӷ���ʱ����: {ex.Message}");
                }
            }
        }

        private async Task ShowEditCategoryDialog(GameCategory category)
        {
            var nameBox = new TextBox()
            {
                Text = category.Name,
                Margin = new Thickness(0, 0, 0, 12)
            };

            // �������ӻ���ɫѡ������Ԥѡ��ǰ��ɫ
            var colorSelectionPanel = CreateColorSelectionPanel(category.Color);

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock() { Text = "��������:", Margin = new Thickness(0, 0, 0, 4) });
            stackPanel.Children.Add(nameBox);
            stackPanel.Children.Add(new TextBlock() { Text = "������ɫ:", Margin = new Thickness(0, 8, 0, 4) });
            stackPanel.Children.Add(colorSelectionPanel);

            var dialog = new ContentDialog()
            {
                Title = "�༭����",
                Content = stackPanel,
                PrimaryButtonText = "ȷ��",
                SecondaryButtonText = "ȡ��",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(nameBox.Text))
                    {
                        await ShowErrorDialog("�������������");
                        return;
                    }

                    var selectedColor = GetSelectedColorFromPanel(colorSelectionPanel);
                    await CategoryService.Instance.UpdateCategoryAsync(category.Id, nameBox.Text, selectedColor);
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog($"�༭����ʱ����: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// �������ӻ���ɫѡ�����
        /// </summary>
        private StackPanel CreateColorSelectionPanel(string? selectedColor = null)
        {
            var mainPanel = new StackPanel();
            
            // ��ȡ��ɫ��Ϣ
            var colorInfos = CategoryService.GetPresetColorsWithNames();
            
            // ������ɫ��������
            var colorGrid = new Grid();
            
            // ��������������4�У�
            for (int i = 0; i < 4; i++)
            {
                colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            
            // ������������
            int rowCount = (int)Math.Ceiling(colorInfos.Length / 4.0);
            for (int i = 0; i < rowCount; i++)
            {
                colorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // �����ɫѡ��ť
            for (int i = 0; i < colorInfos.Length; i++)
            {
                var colorInfo = colorInfos[i];
                var row = i / 4;
                var col = i % 4;

                var colorButton = CreateColorButton(colorInfo, selectedColor == colorInfo.ColorCode);
                
                Grid.SetRow(colorButton, row);
                Grid.SetColumn(colorButton, col);
                colorGrid.Children.Add(colorButton);
            }

            mainPanel.Children.Add(colorGrid);
            return mainPanel;
        }

        /// <summary>
        /// ����������ɫѡ��ť
        /// </summary>
        private Border CreateColorButton(ColorInfo colorInfo, bool isSelected = false)
        {
            var colorBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(ParseColor(colorInfo.ColorCode));
            
            var colorCircle = new Ellipse()
            {
                Width = 32,
                Height = 32,
                Fill = colorBrush,
                Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                StrokeThickness = 1
            };

            var checkMark = new SymbolIcon(Symbol.Accept)
            {
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed
            };

            var contentGrid = new Grid();
            contentGrid.Children.Add(colorCircle);
            contentGrid.Children.Add(checkMark);

            var nameText = new TextBlock()
            {
                Text = colorInfo.DisplayName,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var buttonContent = new StackPanel()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 4
            };
            buttonContent.Children.Add(contentGrid);
            buttonContent.Children.Add(nameText);

            var border = new Border()
            {
                Child = buttonContent,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = isSelected 
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Margin = new Thickness(4),
                Tag = colorInfo // �洢��ɫ��Ϣ
            };

            // ��ӵ���¼�
            border.Tapped += (s, e) =>
            {
                // �������ѡ��
                var parent = border.Parent as Grid;
                if (parent != null)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is Border otherBorder)
                        {
                            otherBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                            
                            // �������������
                            var otherContent = otherBorder.Child as StackPanel;
                            var otherGrid = otherContent?.Children[0] as Grid;
                            var otherCheckMark = otherGrid?.Children.OfType<SymbolIcon>().FirstOrDefault();
                            if (otherCheckMark != null)
                            {
                                otherCheckMark.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                }

                // ���õ�ǰѡ��
                border.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                checkMark.Visibility = Visibility.Visible;
            };

            // �����ͣЧ��
            border.PointerEntered += (s, e) =>
            {
                if (border.BorderBrush is Microsoft.UI.Xaml.Media.SolidColorBrush brush && 
                    brush.Color == Microsoft.UI.Colors.Transparent)
                {
                    border.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray);
                }
            };

            border.PointerExited += (s, e) =>
            {
                if (border.BorderBrush is Microsoft.UI.Xaml.Media.SolidColorBrush brush && 
                    brush.Color == Microsoft.UI.Colors.LightGray)
                {
                    border.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
            };

            return border;
        }

        /// <summary>
        /// ����ɫѡ������ȡѡ�е���ɫ
        /// </summary>
        private string GetSelectedColorFromPanel(StackPanel panel)
        {
            var colorGrid = panel.Children[0] as Grid;
            if (colorGrid != null)
            {
                foreach (var child in colorGrid.Children)
                {
                    if (child is Border border && 
                        border.BorderBrush is Microsoft.UI.Xaml.Media.SolidColorBrush brush &&
                        brush.Color == Microsoft.UI.Colors.DodgerBlue)
                    {
                        if (border.Tag is ColorInfo colorInfo)
                        {
                            return colorInfo.ColorCode;
                        }
                    }
                }
            }
            
            // ���û��ѡ�񣬷���Ĭ����ɫ
            return "#2196F3";
        }

        /// <summary>
        /// ������ɫ�ַ���Ϊ Windows.UI.Color
        /// </summary>
        private Windows.UI.Color ParseColor(string colorCode)
        {
            try
            {
                if (colorCode.StartsWith("#") && colorCode.Length == 7)
                {
                    var r = Convert.ToByte(colorCode.Substring(1, 2), 16);
                    var g = Convert.ToByte(colorCode.Substring(3, 2), 16);
                    var b = Convert.ToByte(colorCode.Substring(5, 2), 16);
                    return Windows.UI.Color.FromArgb(255, r, g, b);
                }
            }
            catch
            {
                // �������ʧ�ܣ�����Ĭ����ɫ
            }
            
            return Windows.UI.Color.FromArgb(255, 33, 150, 243); // #2196F3
        }

        private async Task ShowSetCategoryDialog(CustomDataObject game)
        {
            var availableCategories = Categories.Where(c => c.Id != "all").ToList();
            
            var categoryComboBox = new ComboBox()
            {
                ItemsSource = availableCategories,
                DisplayMemberPath = "Name",
                SelectedValuePath = "Id",
                SelectedValue = string.IsNullOrEmpty(game.CategoryId) ? "uncategorized" : game.CategoryId,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock() 
            { 
                Text = $"Ϊ��Ϸ \"{game.Title}\" ���÷���:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
            stackPanel.Children.Add(categoryComboBox);

            var dialog = new ContentDialog()
            {
                Title = "������Ϸ����",
                Content = stackPanel,
                PrimaryButtonText = "ȷ��",
                SecondaryButtonText = "ȡ��",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && categoryComboBox.SelectedItem is GameCategory selectedCategory)
            {
                game.CategoryId = selectedCategory.Id;
                game.Category = selectedCategory.Name;
                
                await SaveGamesData();
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
            }
        }

        private async Task DeleteCategory(GameCategory category)
        {
            try
            {
                // ����Ƿ�����Ϸʹ�ô˷���
                var gamesInCategory = Items.Where(g => g.CategoryId == category.Id).ToList();
                
                string message = $"ȷ��Ҫɾ������ \"{category.Name}\" ��";
                if (gamesInCategory.Count > 0)
                {
                    message += $"\n\n�÷������� {gamesInCategory.Count} ����Ϸ��ɾ������Щ��Ϸ����Ϊ\"δ����\"��";
                }

                var confirmDialog = new ContentDialog()
                {
                    Title = "ȷ��ɾ��",
                    Content = message,
                    PrimaryButtonText = "ɾ��",
                    SecondaryButtonText = "ȡ��",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // ���÷����µ���Ϸ��Ϊδ����
                    foreach (var game in gamesInCategory)
                    {
                        game.CategoryId = "uncategorized";
                        game.Category = "δ����";
                    }

                    await CategoryService.Instance.DeleteCategoryAsync(category.Id);
                    await SaveGamesData(); // ������Ϸ���ݵķ�����
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"ɾ������ʱ����: {ex.Message}");
            }
        }

        #endregion

        // Update existing context menu handler
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
                await ShowErrorDialog($"������Ϸ����ʱ����: {ex.Message}");
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
                await ShowErrorDialog($"���� Steam ��Ϸʱ����: {ex.Message}");
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
                await ShowErrorDialog($"���� Xbox ��Ϸʱ����: {ex.Message}");
            }
        }

        #region Xbox Import Methods

        private async Task ImportXboxGames()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("��ʼ���� Xbox ��Ϸ");

                // ��ʾ���ȶԻ���
                var progressDialog = new ContentDialog()
                {
                    Title = "���� Xbox ��Ϸ",
                    Content = "����ɨ�� Xbox ��Ϸ�⣬���Ժ�...",
                    XamlRoot = this.XamlRoot
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
                        await ShowErrorDialog("δ�ҵ��Ѱ�װ�� Xbox ��Ϸ��");
                        return;
                    }

                    // ���˵��Ѿ����ڵ���Ϸ - ʹ�ö��ؼ������ظ�
                    var existingPackageNames = Items.Where(item => item.IsXboxGame)
                                                  .Select(item => item.XboxPackageFamilyName)
                                                  .Where(name => !string.IsNullOrEmpty(name))
                                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // ͬʱ����ִ���ļ�·�����ⲻͬ·�����ظ�
                    var existingPaths = Items.Where(item => !string.IsNullOrEmpty(item.ExecutablePath))
                                           .Select(item => Path.GetFullPath(item.ExecutablePath).ToLowerInvariant())
                                           .ToHashSet();

                    var newGames = xboxGames.Where(game => 
                    {
                        // ��� Package Family Name �Ƿ��Ѵ���
                        if (existingPackageNames.Contains(game.PackageFamilyName))
                        {
                            System.Diagnostics.Debug.WriteLine($"�����ظ��� Xbox Package: {game.PackageFamilyName} - {game.Name}");
                            return false;
                        }

                        // ����ִ���ļ�·���Ƿ��Ѵ��ڣ���׼��·���Ƚϣ�
                        if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                        {
                            var normalizedPath = Path.GetFullPath(game.ExecutablePath).ToLowerInvariant();
                            if (existingPaths.Contains(normalizedPath))
                            {
                                System.Diagnostics.Debug.WriteLine($"�����ظ��Ŀ�ִ���ļ�·��: {game.ExecutablePath} - {game.Name}");
                                return false;
                            }
                        }

                        return true;
                    }).ToList();

                    if (newGames.Count == 0)
                    {
                        await ShowErrorDialog("���� Xbox ��Ϸ���ѵ��롣");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"�ҵ� {newGames.Count} ���µ� Xbox ��Ϸ������");

                    // ��ʾѡ��Ի���
                    var selectedGames = await ShowXboxGameSelectionDialog(newGames);
                    if (selectedGames.Count > 0)
                    {
                        await ImportSelectedXboxGames(selectedGames);
                        await ShowInfoDialog($"�ɹ����� {selectedGames.Count} �� Xbox ��Ϸ��");
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
                System.Diagnostics.Debug.WriteLine($"���� Xbox ��Ϸʱ�����쳣: {ex.Message}");
                await ShowErrorDialog($"���� Xbox ��Ϸʱ����: {ex.Message}");
            }
        }

        private async Task<List<XboxGame>> ShowXboxGameSelectionDialog(List<XboxGame> xboxGames)
        {
            var selectedGames = new List<XboxGame>();

            try
            {
                // ����һ��װ����������
                var gameSelectionItems = xboxGames.Select(game => new XboxGameSelectionItem
                {
                    Game = game,
                    IsSelected = true, // Ĭ��ȫѡ
                    DisplayName = game.Name
                }).ToList();

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
                        if (checkBox.Tag is XboxGameSelectionItem selectionItem)
                            selectionItem.IsSelected = true;
                    };
                    checkBox.Unchecked += (s, e) => 
                    {
                        if (checkBox.Tag is XboxGameSelectionItem selectionItem)
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
                    Text = $"�ҵ� {xboxGames.Count} ���µ� Xbox ��Ϸ����ѡ��Ҫ�������Ϸ:",
                    Margin = new Thickness(0, 0, 0, 10),
                    TextWrapping = TextWrapping.Wrap
                });
                contentPanel.Children.Add(scrollViewer);

                // ���ȫѡ��ť
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
                            if (checkBox.Tag is XboxGameSelectionItem selectionItem)
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
                            if (checkBox.Tag is XboxGameSelectionItem selectionItem)
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
                    Title = "ѡ�� Xbox ��Ϸ",
                    Content = contentPanel,
                    PrimaryButtonText = "����ѡ�е���Ϸ",
                    SecondaryButtonText = "ȡ��",
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
                System.Diagnostics.Debug.WriteLine($"��ʾ Xbox ��Ϸѡ��Ի���ʱ����: {ex.Message}");
            }

            return selectedGames;
        }

        private async Task ImportSelectedXboxGames(List<XboxGame> xboxGames)
        {
            try
            {
                foreach (var xboxGame in xboxGames)
                {
                    System.Diagnostics.Debug.WriteLine($"���� Xbox ��Ϸ: {xboxGame.Name}");

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

                    Items.Add(gameData);
                }

                // ��������
                await SaveGamesData();
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"����ѡ�е� Xbox ��Ϸʱ����: {ex.Message}");
                throw;
            }
        }

        // ��Ӹ���������Xbox��Ϸѡ��
        private class XboxGameSelectionItem
        {
            public XboxGame Game { get; set; } = new XboxGame();
            public bool IsSelected { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }

        #endregion
    }

    /// <summary>
    /// ��ɫ�ַ�������ɫת����
    /// </summary>
    public class ColorStringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string colorString && !string.IsNullOrEmpty(colorString))
            {
                try
                {
                    if (colorString.StartsWith("#") && colorString.Length == 7)
                    {
                        var r = System.Convert.ToByte(colorString.Substring(1, 2), 16);
                        var g = System.Convert.ToByte(colorString.Substring(3, 2), 16);
                        var b = System.Convert.ToByte(colorString.Substring(5, 2), 16);
                        return Windows.UI.Color.FromArgb(255, r, g, b);
                    }
                }
                catch
                {
                    // �������ʧ�ܣ�����Ĭ����ɫ
                }
            }
            
            // Ĭ�Ϸ�����ɫ
            return Windows.UI.Color.FromArgb(255, 33, 150, 243);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

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
        public string Category { get; set; } = "δ����";
    }
}
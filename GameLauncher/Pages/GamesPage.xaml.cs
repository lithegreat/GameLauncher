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
    /// ��Ϸҳ�� - �ع���ļ򻯰汾
    /// </summary>
    public sealed partial class GamesPage : Page, INotifyPropertyChanged
    {
        #region Fields and Properties

        // ������
        private readonly GameDataManager _gameDataManager;
        private readonly GameSelectionManager _gameSelectionManager;
        private readonly GameCategoryManager _gameCategoryManager;
        private readonly GameDialogManager _gameDialogManager;
        private readonly GameImportManager _gameImportManager;
        private readonly GameOperationManager _gameOperationManager;
        private readonly GameDragDropManager _gameDragDropManager;

        // UI״̬
        private bool _isDeleteMode = false;
        private CustomDataObject? _contextMenuGame = null;

        // ���԰�
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
            
            // ��ʼ��������
            _gameDataManager = new GameDataManager();
            _gameSelectionManager = new GameSelectionManager(this);
            _gameCategoryManager = new GameCategoryManager(_gameDataManager);
            _gameDialogManager = new GameDialogManager(this, _gameDataManager, _gameCategoryManager);
            _gameImportManager = new GameImportManager(this, _gameDataManager, _gameCategoryManager, _gameDialogManager);
            _gameOperationManager = new GameOperationManager(_gameDataManager, _gameCategoryManager, _gameDialogManager);
            _gameDragDropManager = new GameDragDropManager(GamesListView, _gameDataManager, _gameCategoryManager, _gameDialogManager);

            // �����¼�����
            SetupEventSubscriptions();

            this.Loaded += GamesPage_Loaded;
        }

        private void SetupEventSubscriptions()
        {
            // ����ɾ���¼�
            CategoryService.Instance.CategoryDeleted += OnCategoryDeleted;

            // ��Ϸѡ���¼�
            _gameSelectionManager.SelectedGameChanged += OnSelectedGameChanged;
            _gameSelectionManager.SetCategoryRequested += OnSetCategoryRequested;
            _gameSelectionManager.OpenInSteamRequested += OnOpenInSteamRequested;
            _gameSelectionManager.DeleteGameRequested += OnDeleteGameRequested;

            // ��������¼�
            _gameCategoryManager.FilterChanged += OnFilterChanged;
            _gameCategoryManager.CategoriesChanged += OnCategoriesChanged;
        }

        private async void GamesPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("GamesPage_Loaded ��ʼ");

                // ȷ����������Ѽ���
                await CategoryService.Instance.LoadCategoriesAsync();

                // ֻ������δ����ʱ�������ݣ����⸲�������������
                if (!_gameDataManager.IsDataLoaded)
                {
                    Debug.WriteLine("�״μ��أ�������Ϸ����");
                    await _gameDataManager.LoadGamesDataAsync();
                }

                // ��ʼ������ѡ��
                _gameCategoryManager.InitializeDefaultCategory();
                _gameCategoryManager.ApplyCategoryFilter();
                _gameCategoryManager.UpdateCategoryGameCounts();

                Debug.WriteLine("GamesPage_Loaded ���");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GamesPage_Loaded �쳣: {ex.Message}");
                await _gameDialogManager.ShowErrorDialogAsync($"ҳ�����ʱ����: {ex.Message}");
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // �첽ִ�г�ʼ������
            _ = Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("GamesPage OnNavigatedTo ��ʼ");

                    // ֻ������δ����ʱ���¼�������
                    if (!_gameDataManager.IsDataLoaded)
                    {
                        Debug.WriteLine("����δ���أ����¼�����Ϸ����");
                        await CategoryService.Instance.LoadCategoriesAsync();
                        await _gameDataManager.LoadGamesDataAsync();

                        // ��UI�߳���ִ��UI����
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            _gameCategoryManager.InitializeDefaultCategory();
                            _gameCategoryManager.ApplyCategoryFilter();
                            _gameCategoryManager.UpdateCategoryGameCounts();
                        });
                    }
                    else
                    {
                        Debug.WriteLine("�����Ѽ��أ����ֵ�ǰ״̬");

                        // ȷ����������Ѽ��أ���ҳ�浼������ʱ������Ҫ��
                        await CategoryService.Instance.LoadCategoriesAsync();

                        // ��UI�߳���ִ��UI����
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            _gameCategoryManager.ApplyCategoryFilter();
                            _gameCategoryManager.UpdateCategoryGameCounts();
                        });
                    }

                    Debug.WriteLine("GamesPage OnNavigatedTo ���");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnNavigatedTo �쳣: {ex.Message}");
                }
            });
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // ҳ���뿪ʱ���浱ǰ����Ϸ˳��
            await _gameDataManager.SaveGamesDataAsync();
        }

        #endregion

        #region Event Handlers - Manager Events

        private async void OnCategoryDeleted(string deletedCategoryId)
        {
            await _gameCategoryManager.HandleCategoryDeleted(deletedCategoryId);

            // �����ǰѡ�е���Ϸ�ܵ�Ӱ�죬����������ʾ
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
            // UI��ͨ�����Զ�����
        }

        private void OnCategoriesChanged()
        {
            // ���������Ѹ��£�UI���Զ���ӳ
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
                            Debug.WriteLine($"ѡ����: {selectedCount} ����Ŀ��ѡ��");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"����ɾ����ť״̬ʱ�쳣: {ex.Message}");
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
                Debug.WriteLine("�Ҽ�����¼�����");

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
                    Debug.WriteLine($"���������Ĳ˵���Ϸ: {game.Title}");

                    // ������Ϸ���Ͷ�̬��ʾ�˵���
                    UpdateContextMenuForGame(game);

                    // Context menu will show automatically due to ContextFlyout in XAML
                }
                else
                {
                    _contextMenuGame = null;
                    Debug.WriteLine("δ�ҵ���Ϸ����������");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"�Ҽ���������쳣: {ex.Message}");
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
                Debug.WriteLine($"����ѡ����ʱ����: {ex.Message}");
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
                await _gameDialogManager.ShowErrorDialogAsync($"�����Ϸʱ����: {ex.Message}");
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
                await _gameDialogManager.ShowErrorDialogAsync($"������Ϸʱ����: {ex.Message}");
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
                await _gameDialogManager.ShowErrorDialogAsync($"����ϷĿ¼ʱ����: {ex.Message}");
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
                Debug.WriteLine("ȡ��ɾ��ģʽ");

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        _isDeleteMode = false;
                        UpdateDeleteModeUI();
                        Debug.WriteLine("ɾ��ģʽ��ȡ��");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ȡ��ɾ��ģʽʱUI�쳣: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ȡ��ɾ����ť�쳣: {ex.Message}");
                _isDeleteMode = false;
            }
        }

        private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("��ʼ����ɾ����Ϸ");

                // ��ȫ�ػ�ȡѡ�е���Ŀ
                var selectedItems = new List<CustomDataObject>();

                // ȷ���� UI �߳���ִ��
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
                        Debug.WriteLine($"ѡ���� {selectedItems.Count} ����Ϸ");
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"��ȡѡ����Ŀʱ�쳣: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });

                await tcs.Task;

                if (selectedItems.Count == 0)
                {
                    await _gameDialogManager.ShowErrorDialogAsync("��ѡ��Ҫɾ������Ϸ");
                    return;
                }

                bool confirmed = await _gameDialogManager.ShowDeleteConfirmationDialogAsync(
                    "ȷ��ɾ��",
                    $"ȷ��Ҫɾ�� {selectedItems.Count} ����Ϸ�𣿴˲����޷�������");

                if (confirmed)
                {
                    Debug.WriteLine("�û�ȷ������ɾ������ʼִ��ɾ������");

                    // Clear selected game if it's being deleted
                    if (SelectedGame != null && selectedItems.Contains(SelectedGame))
                    {
                        SelectedGame = null;
                    }

                    // �� UI �߳��ϰ�ȫ��ɾ����Ŀ
                    var deleteTcs = new TaskCompletionSource<bool>();
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            // Remove selected items from the collection
                            _gameDataManager.RemoveGames(selectedItems);

                            Debug.WriteLine("����ɾ����ɣ��˳�ɾ��ģʽ");

                            // Exit delete mode
                            _isDeleteMode = false;
                            UpdateDeleteModeUI();

                            deleteTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"UI�߳�ɾ�������쳣: {ex.Message}");
                            deleteTcs.SetException(ex);
                        }
                    });

                    await deleteTcs.Task;

                    // Save the updated data
                    await _gameDataManager.SaveGamesDataAsync();

                    // ȷ��UI��������
                    _gameCategoryManager.ApplyCategoryFilter();
                    _gameCategoryManager.UpdateCategoryGameCounts();
                }
                else
                {
                    Debug.WriteLine("�û�ȡ������ɾ������");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"����ɾ����Ϸʱ�����쳣: {ex.Message}");

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
                            Debug.WriteLine($"����UI�쳣: {updateEx.Message}");
                        }
                    });
                }
                catch
                {
                    // ����Ƕ���쳣
                }

                await _gameDialogManager.ShowErrorDialogAsync($"ɾ����Ϸʱ����: {ex.Message}");
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
                await _gameDialogManager.ShowErrorDialogAsync($"�������ʱ����: {ex.Message}");
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
                await _gameDialogManager.ShowErrorDialogAsync($"����Steam��Ϸʱ����: {ex.Message}");
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
                await _gameDialogManager.ShowErrorDialogAsync($"����Xbox��Ϸʱ����: {ex.Message}");
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
                    Debug.WriteLine($"����Ϸ: {_contextMenuGame.Title}");
                    await _gameOperationManager.LaunchGameAsync(_contextMenuGame);
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"����Ϸʱ����: {ex.Message}");
            }
        }

        private async void DeleteGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    Debug.WriteLine($"ɾ����Ϸ�˵�����: {_contextMenuGame.Title}");
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
                await _gameDialogManager.ShowErrorDialogAsync($"ɾ����Ϸʱ����: {ex.Message}");
            }
        }

        private async void OpenGameDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null)
                {
                    Debug.WriteLine($"����ϷĿ¼: {_contextMenuGame.Title}");
                    await _gameOperationManager.OpenGameDirectoryAsync(_contextMenuGame);
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"����ϷĿ¼ʱ����: {ex.Message}");
            }
        }

        private async void OpenInSteamMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_contextMenuGame != null && _contextMenuGame.IsSteamGame)
                {
                    Debug.WriteLine($"�� Steam �д���Ϸ: {_contextMenuGame.Title}");
                    await _gameOperationManager.OpenInSteamAsync(_contextMenuGame);
                }
                else
                {
                    await _gameDialogManager.ShowErrorDialogAsync("����Ϸ���� Steam ��Ϸ");
                }
            }
            catch (Exception ex)
            {
                await _gameDialogManager.ShowErrorDialogAsync($"�� Steam �д���Ϸʱ����: {ex.Message}");
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
                await _gameDialogManager.ShowErrorDialogAsync($"���÷���ʱ����: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"���������Ĳ˵�ʱ�쳣: {ex.Message}");
            }
        }

        private void UpdateDeleteModeUI()
        {
            try
            {
                Debug.WriteLine($"����ɾ��ģʽUI: _isDeleteMode = {_isDeleteMode}");
                
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
                    
                    Debug.WriteLine("����ɾ��ģʽ");
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
                        Debug.WriteLine($"���ѡ��ʱ�쳣: {ex.Message}");
                    }
                    
                    GamesListView.SelectionMode = ListViewSelectionMode.Single;
                    GamesListView.IsItemClickEnabled = true;
                    
                    // Hide delete mode buttons
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                    CancelDeleteButton.Visibility = Visibility.Collapsed;
                    
                    // Show normal mode buttons
                    DeleteModeButton.Visibility = Visibility.Visible;
                    AddGameDropDownButton.Visibility = Visibility.Visible;
                    
                    Debug.WriteLine("�˳�ɾ��ģʽ");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateDeleteModeUI �쳣: {ex.Message}");
                
                // �����޸���ǿ�����õ���ȫ״̬
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
                    // ����Ƕ���쳣
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

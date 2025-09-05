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
            
            // ���ķ���ɾ���¼�
            CategoryService.Instance.CategoryDeleted += OnCategoryDeleted;
        }

        private async void OnCategoryDeleted(string deletedCategoryId)
        {
            try
            {
                // ����ɾ�������µ�������Ϸ�ƶ���"δ����"
                var affectedGames = Items.Where(g => g.CategoryId == deletedCategoryId).ToList();
                
                foreach (var game in affectedGames)
                {
                    game.CategoryId = "uncategorized";
                    game.Category = "δ����";
                    // CategoryColor ���Զ�ͨ�� CategoryId �ı仯������
                }
                
                if (affectedGames.Any())
                {
                    // ������º����Ϸ����
                    await SaveGamesData();
                    
                    // ˢ��UI
                    ApplyCategoryFilter();
                    UpdateCategoryGameCounts();
                    
                    // �����ǰѡ�е���Ϸ�ܵ�Ӱ�죬����������ʾ
                    if (SelectedGame != null && affectedGames.Contains(SelectedGame))
                    {
                        UpdateGameDetails();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�������ɾ���¼�ʱ����: {ex.Message}");
            }
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
                            System.Diagnostics.Debug.WriteLine($"ѡ����: {selectedCount} ����Ŀ��ѡ��");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"����ɾ����ť״̬ʱ�쳣: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine("�Ҽ�����¼�����");
                
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"���������Ĳ˵�ʱ�쳣: {ex.Message}");
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
                    var steamText = new TextBlock { Text = "Steam ��Ϸ", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
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
                    var xboxText = new TextBlock { Text = "Xbox ��Ϸ", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
                    var xboxPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    xboxPanel.Children.Add(xboxIcon);
                    xboxPanel.Children.Add(xboxText);
                    GameTypePanel.Children.Add(xboxPanel);
                }
                else
                {
                    var localIcon = new FontIcon { Glyph = "\uE8B7", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) };
                    var localText = new TextBlock { Text = "������Ϸ", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
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
                    PlaytimeText.Text = $"��Ϸʱ��: {hours:N0} Сʱ {minutes} ����";
                }
                else
                {
                    PlaytimeText.Text = "��Ϸʱ��: ��������";
                }

                // Format last activity
                if (SelectedGame.LastActivity.HasValue)
                {
                    LastActivityText.Text = $"�������: {SelectedGame.LastActivity.Value:yyyy��MM��dd��}";
                }
                else
                {
                    LastActivityText.Text = "�������: ��������";
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
                    LastModifiedText.Text = fileInfo.LastWriteTime.ToString("yyyy��MM��dd�� HH:mm");
                }
                else
                {
                    FileSizeText.Text = "�ļ�������";
                    LastModifiedText.Text = "-";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateFileInformation error: {ex.Message}");
                FileSizeText.Text = "�޷���ȡ";
                LastModifiedText.Text = "�޷���ȡ";
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
                    Content = "���÷���",
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
                        Content = "�� Steam �д�",
                        Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    openInSteamButton.Click += async (s, e) => await OpenInSteam(SelectedGame);
                    ActionsPanel.Children.Add(openInSteamButton);
                }

                // Delete game button
                var deleteButton = new Button
                {
                    Content = "ɾ����Ϸ",
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
                await ShowErrorDialog($"������Ϸʱ����: {ex.Message}");
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
                await ShowErrorDialog($"����ϷĿ¼ʱ����: {ex.Message}");
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
                    await ShowErrorDialog("����Ϸ���� Steam ��Ϸ");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"�� Steam �д���Ϸʱ����: {ex.Message}");
            }
        }

        #endregion

        #region Category and Filter Management

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
                System.Diagnostics.Debug.WriteLine($"����ѡ����ʱ����: {ex.Message}");
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
                await ShowErrorDialog($"���÷���ʱ����: {ex.Message}");
            }
        }

        #endregion

        #region Game Management Methods

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
                    
                    // Clear selected game if it's being deleted
                    if (SelectedGame == game)
                    {
                        SelectedGame = null;
                    }
                    
                    // ȷ����Ϸ�ڼ����д���
                    if (Items.Contains(game))
                    {
                        Items.Remove(game);
                        System.Diagnostics.Debug.WriteLine("��Ϸ�ѴӼ������Ƴ�");
                        
                        // ������º������
                        await SaveGamesData();
                        
                        // ȷ��UI��������
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
                        foreach (var item in GamesListView.SelectedItems)
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
                    
                    // ȷ��UI��������
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

        private async void ManageCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowManageCategoriesDialog();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"�������ʱ����: {ex.Message}");
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
                await ShowErrorDialog($"����Steam��Ϸʱ����: {ex.Message}");
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
                await ShowErrorDialog($"����Xbox��Ϸʱ����: {ex.Message}");
            }
        }

        #endregion

        #region Delete Mode UI

        private void UpdateDeleteModeUI()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"����ɾ��ģʽUI: _isDeleteMode = {_isDeleteMode}");
                
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
                    
                    System.Diagnostics.Debug.WriteLine("����ɾ��ģʽ");
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
                        System.Diagnostics.Debug.WriteLine($"����ѡ��ʱ�쳣: {ex.Message}");
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
                    // ����Ƕ���쳣
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
                            
                            // ����ʾ˳��Ȼ����ӵ�����
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
                                    Category = gameJson.Category ?? "δ����",
                                    Playtime = gameJson.Playtime,
                                    LastActivity = gameJson.LastActivity
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
                                           Category = item.Category,
                                           Playtime = item.Playtime,
                                           LastActivity = item.LastActivity
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

        private async Task SaveCurrentGameOrder()
        {
            try
            {
                await SaveGamesData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"������Ϸ˳��ʱ����: {ex.Message}");
            }
        }

        #endregion

        #region Dialog Methods

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
                
                // ȷ��UI����������ʾ����ӵ���Ϸ
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
                        
                        // ȷ��UI���������Է�ӳ������
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
                System.Diagnostics.Debug.WriteLine($"��ʾ���÷���Ի���ʱ����: {ex.Message}");
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
                
                // �Ի���رպ�ˢ�·�����ص�UI
                if (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
                {
                    // ���¼���������Ϸ����
                    UpdateCategoryGameCounts();
                    
                    // �����ǰѡ�еķ��౻ɾ���ˣ��л���"ȫ����Ϸ"
                    if (_selectedCategory != null && !Categories.Contains(_selectedCategory))
                    {
                        _selectedCategory = Categories.FirstOrDefault(c => c.Id == "all");
                        CategoryComboBox.SelectedItem = _selectedCategory;
                    }
                    
                    // ����Ӧ�÷���ɸѡ
                    ApplyCategoryFilter();
                    
                    // ����ѡ����Ϸ�ķ�����Ϣ��������౻�޸ģ�
                    if (SelectedGame != null)
                    {
                        var updatedCategory = Categories.FirstOrDefault(c => c.Id == SelectedGame.CategoryId);
                        if (updatedCategory != null)
                        {
                            SelectedGame.Category = updatedCategory.Name;
                            // CategoryColor ���Զ�ͨ�� CategoryId ���£�����Ҫ�ֶ�����
                        }
                        UpdateGameDetails();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ʾ�������Ի���ʱ����: {ex.Message}");
                throw;
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

        private async Task ShowInfoDialog(string content)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "��Ϣ",
                    Content = content,
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

        #endregion

        #region Steam and Xbox Import (Simplified placeholders)

        private async Task ImportSteamGames()
        {
            await ShowInfoDialog("Steam ��Ϸ���빦�ܼ���ʵ��");
        }

        private async Task ImportXboxGames()
        {
            await ShowInfoDialog("Xbox ��Ϸ���빦�ܼ���ʵ��");
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
                        System.Diagnostics.Debug.WriteLine($"������ {removedCount} ���ظ���Ϸ");
                        await ShowInfoDialog($"������ {removedCount} ���ظ���Ϸ");
                    }
                    else
                    {
                        await ShowInfoDialog("û�з����ظ���Ϸ");
                    }
                }
                else
                {
                    await ShowInfoDialog("û�з����ظ���Ϸ");
                }

                // ʼ��ˢ��UI��ȷ����ʾ����״̬
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�����ظ���Ϸʱ����: {ex.Message}");
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
            public string Category { get; set; } = "δ����";
            public ulong Playtime { get; set; } = 0;
            public DateTime? LastActivity { get; set; }
        }

        #endregion
    }
}

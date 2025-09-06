using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using GameLauncher.Models;
using GameLauncher.Services;
using System.Diagnostics;

namespace GameLauncher.Managers
{
    /// <summary>
    /// 游戏选择和详情显示管理器
    /// </summary>
    public class GameSelectionManager
    {
        private readonly Page _page;
        private CustomDataObject? _selectedGame;

        // UI 控件引用
        private readonly StackPanel _gameDetailsPanel;
        private readonly StackPanel _emptyStatePanel;
        private readonly Image _gameIcon;
        private readonly TextBlock _gameTitle;
        private readonly TextBlock _gameCategory;
        private readonly StackPanel _gameTypePanel;
        private readonly StackPanel _actionsPanel;
        private readonly TextBlock _playtimeText;
        private readonly TextBlock _lastActivityText;
        private readonly TextBlock _executablePathText;
        private readonly TextBlock _fileSizeText;
        private readonly TextBlock _lastModifiedText;
        private readonly Microsoft.UI.Xaml.Shapes.Ellipse _categoryColorIndicator;

        public event Action<CustomDataObject?>? SelectedGameChanged;

        public CustomDataObject? SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (_selectedGame != value)
                {
                    _selectedGame = value;
                    SelectedGameChanged?.Invoke(_selectedGame);
                    _ = UpdateGameDetailsAsync();
                }
            }
        }

        public GameSelectionManager(Page page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            
            // 获取UI控件引用 - 这些需要在页面中通过Name属性访问
            _gameDetailsPanel = (StackPanel)_page.FindName("GameDetailsPanel");
            _emptyStatePanel = (StackPanel)_page.FindName("EmptyStatePanel");
            _gameIcon = (Image)_page.FindName("GameIcon");
            _gameTitle = (TextBlock)_page.FindName("GameTitle");
            _gameCategory = (TextBlock)_page.FindName("GameCategory");
            _gameTypePanel = (StackPanel)_page.FindName("GameTypePanel");
            _actionsPanel = (StackPanel)_page.FindName("ActionsPanel");
            _playtimeText = (TextBlock)_page.FindName("PlaytimeText");
            _lastActivityText = (TextBlock)_page.FindName("LastActivityText");
            _executablePathText = (TextBlock)_page.FindName("ExecutablePathText");
            _fileSizeText = (TextBlock)_page.FindName("FileSizeText");
            _lastModifiedText = (TextBlock)_page.FindName("LastModifiedText");
            _categoryColorIndicator = (Microsoft.UI.Xaml.Shapes.Ellipse)_page.FindName("CategoryColorIndicator");
        }

        public async Task UpdateGameDetailsAsync()
        {
            try
            {
                if (SelectedGame == null)
                {
                    ShowEmptyState();
                    return;
                }

                ShowGameDetails();
                await UpdateGameInformationAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateGameDetails error: {ex.Message}");
            }
        }

        private void ShowEmptyState()
        {
            _gameDetailsPanel.Visibility = Visibility.Collapsed;
            _emptyStatePanel.Visibility = Visibility.Visible;
        }

        private void ShowGameDetails()
        {
            _emptyStatePanel.Visibility = Visibility.Collapsed;
            _gameDetailsPanel.Visibility = Visibility.Visible;
        }

        private async Task UpdateGameInformationAsync()
        {
            if (SelectedGame == null) return;

            // Update game header
            _gameIcon.Source = SelectedGame.IconImage;
            _gameTitle.Text = SelectedGame.Title;
            _gameCategory.Text = SelectedGame.Category;

            // Update category color indicator
            UpdateCategoryColorIndicator();

            // Update game type panel
            UpdateGameTypePanel();

            // Update game stats
            UpdateGameStats();

            // Update file information
            await UpdateFileInformationAsync();

            // Update actions panel
            UpdateActionsPanel();
        }

        private void UpdateCategoryColorIndicator()
        {
            if (_categoryColorIndicator != null && !string.IsNullOrEmpty(SelectedGame?.CategoryColor))
            {
                var converter = new ColorStringToColorConverter();
                var color = (Windows.UI.Color)converter.Convert(SelectedGame.CategoryColor, typeof(Windows.UI.Color), null, "");
                _categoryColorIndicator.Fill = new SolidColorBrush(color);
            }
        }

        private void UpdateGameTypePanel()
        {
            try
            {
                _gameTypePanel.Children.Clear();

                if (SelectedGame == null) return;

                if (SelectedGame.IsSteamGame)
                {
                    var steamIcon = new FontIcon { Glyph = "\uE968", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) };
                    var steamText = new TextBlock { Text = "Steam 游戏", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
                    var steamPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    steamPanel.Children.Add(steamIcon);
                    steamPanel.Children.Add(steamText);
                    _gameTypePanel.Children.Add(steamPanel);

                    if (!string.IsNullOrEmpty(SelectedGame.SteamAppId))
                    {
                        var appIdText = new TextBlock
                        {
                            Text = $"App ID: {SelectedGame.SteamAppId}",
                            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                        };
                        _gameTypePanel.Children.Add(appIdText);
                    }
                }
                else if (SelectedGame.IsXboxGame)
                {
                    var xboxIcon = new FontIcon { Glyph = "\uE990", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) };
                    var xboxText = new TextBlock { Text = "Xbox 游戏", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
                    var xboxPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    xboxPanel.Children.Add(xboxIcon);
                    xboxPanel.Children.Add(xboxText);
                    _gameTypePanel.Children.Add(xboxPanel);
                }
                else
                {
                    var localIcon = new FontIcon { Glyph = "\uE8B7", FontSize = 16, Margin = new Thickness(0, 0, 8, 0) };
                    var localText = new TextBlock { Text = "本地游戏", Style = (Style)Application.Current.Resources["BodyTextBlockStyle"] };
                    var localPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    localPanel.Children.Add(localIcon);
                    localPanel.Children.Add(localText);
                    _gameTypePanel.Children.Add(localPanel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateGameTypePanel error: {ex.Message}");
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
                    _playtimeText.Text = $"游戏时间: {hours:N0} 小时 {minutes} 分钟";
                }
                else
                {
                    _playtimeText.Text = "游戏时间: 暂无数据";
                }

                // Format last activity
                if (SelectedGame.LastActivity.HasValue)
                {
                    _lastActivityText.Text = $"最后游玩: {SelectedGame.LastActivity.Value:yyyy年MM月dd日}";
                }
                else
                {
                    _lastActivityText.Text = "最后游玩: 暂无数据";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateGameStats error: {ex.Message}");
            }
        }

        private async Task UpdateFileInformationAsync()
        {
            try
            {
                if (SelectedGame == null) return;

                _executablePathText.Text = SelectedGame.ExecutablePath;

                if (!string.IsNullOrEmpty(SelectedGame.ExecutablePath) && File.Exists(SelectedGame.ExecutablePath))
                {
                    var fileInfo = new FileInfo(SelectedGame.ExecutablePath);

                    // File size
                    var sizeInMB = fileInfo.Length / (1024.0 * 1024.0);
                    if (sizeInMB >= 1024)
                    {
                        _fileSizeText.Text = $"{sizeInMB / 1024.0:F2} GB";
                    }
                    else
                    {
                        _fileSizeText.Text = $"{sizeInMB:F2} MB";
                    }

                    // Last modified
                    _lastModifiedText.Text = fileInfo.LastWriteTime.ToString("yyyy年MM月dd日 HH:mm");
                }
                else
                {
                    _fileSizeText.Text = "文件不存在";
                    _lastModifiedText.Text = "-";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateFileInformation error: {ex.Message}");
                _fileSizeText.Text = "无法获取";
                _lastModifiedText.Text = "无法获取";
            }
        }

        private void UpdateActionsPanel()
        {
            try
            {
                _actionsPanel.Children.Clear();

                if (SelectedGame == null) return;

                // Set Category button
                var setCategoryButton = new Button
                {
                    Content = "设置分类",
                    Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                setCategoryButton.Click += SetCategoryButton_Click;
                _actionsPanel.Children.Add(setCategoryButton);

                // Steam specific actions
                if (SelectedGame.IsSteamGame && !string.IsNullOrEmpty(SelectedGame.SteamAppId))
                {
                    var openInSteamButton = new Button
                    {
                        Content = "在 Steam 中打开",
                        Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    openInSteamButton.Click += OpenInSteamButton_Click;
                    _actionsPanel.Children.Add(openInSteamButton);
                }

                // Delete game button
                var deleteButton = new Button
                {
                    Content = "删除游戏",
                    Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
                };
                deleteButton.Click += DeleteGameButton_Click;
                _actionsPanel.Children.Add(deleteButton);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateActionsPanel error: {ex.Message}");
            }
        }

        // 事件处理器
        public event Action<CustomDataObject>? SetCategoryRequested;
        public event Action<CustomDataObject>? OpenInSteamRequested;
        public event Action<CustomDataObject>? DeleteGameRequested;

        private async void SetCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGame != null)
            {
                SetCategoryRequested?.Invoke(SelectedGame);
            }
        }

        private async void OpenInSteamButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGame != null)
            {
                OpenInSteamRequested?.Invoke(SelectedGame);
            }
        }

        private async void DeleteGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGame != null)
            {
                DeleteGameRequested?.Invoke(SelectedGame);
            }
        }
    }
}
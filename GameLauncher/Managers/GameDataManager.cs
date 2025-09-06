using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using GameLauncher.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using GameLauncher.Services;

namespace GameLauncher.Managers
{
    /// <summary>
    /// ��Ϸ���ݹ�������������Ϸ���ݵļ��ء�����ͻ�������
    /// </summary>
    public class GameDataManager
    {
        private const string GamesDataFileName = "games.json";
        private bool _isDataLoaded = false;

        public ObservableCollection<CustomDataObject> Items { get; } = new ObservableCollection<CustomDataObject>();
        public ObservableCollection<CustomDataObject> FilteredItems { get; } = new ObservableCollection<CustomDataObject>();

        public bool IsDataLoaded => _isDataLoaded;

        // �¼�
        public event Action? DataLoaded;
        public event Action? DataSaved;

        public async Task LoadGamesDataAsync()
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
                                .ToList();

                            // Ϊû��DisplayOrder����Ϸ������ţ������ݣ�
                            for (int i = 0; i < sortedGames.Count; i++)
                            {
                                if (sortedGames[i].DisplayOrder == 0 && i > 0)
                                {
                                    sortedGames[i].DisplayOrder = i;
                                }
                            }

                            // ��DisplayOrder����
                            sortedGames = sortedGames.OrderBy(g => g.DisplayOrder).ToList();

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

                            Debug.WriteLine($"�Ѽ��� {Items.Count} ����Ϸ������˳��ָ�");
                        }
                    }
                }

                _isDataLoaded = true;
                DataLoaded?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadGamesData error: {ex.Message}");
                _isDataLoaded = true; // ��ʹʧ��Ҳ����Ϊ�Ѽ��أ�������������
                throw;
            }
        }

        public async Task SaveGamesDataAsync()
        {
            try
            {
                Debug.WriteLine("��ʼ������Ϸ����");

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

                Debug.WriteLine($"׼������ {gameDataList.Count} ����Ϸ");

                var json = JsonSerializer.Serialize(gameDataList, new JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(file, json);

                Debug.WriteLine("��Ϸ���ݱ���ɹ�");
                DataSaved?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"������Ϸ����ʱ�����쳣: {ex.Message}");
                throw;
            }
        }

        public void AddGame(CustomDataObject game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            
            game.DisplayOrder = GetNextDisplayOrder();
            Items.Add(game);
        }

        public bool RemoveGame(CustomDataObject game)
        {
            if (game == null) return false;
            return Items.Remove(game);
        }

        public void RemoveGames(IEnumerable<CustomDataObject> games)
        {
            if (games == null) return;

            foreach (var game in games.ToList())
            {
                Items.Remove(game);
            }
        }

        public int GetNextDisplayOrder()
        {
            if (Items.Count == 0)
                return 0;

            return Items.Max(g => g.DisplayOrder) + 1;
        }

        public void UpdateGameOrder()
        {
            // ����FilteredItems��������Ϸ��DisplayOrder
            for (int i = 0; i < FilteredItems.Count; i++)
            {
                if (FilteredItems[i] is CustomDataObject game)
                {
                    game.DisplayOrder = i;
                }
            }

            // �ҵ�FilteredItems��ÿ����Ϸ��Items�еĶ�Ӧ�������DisplayOrder
            foreach (var filteredGame in FilteredItems)
            {
                var mainGame = Items.FirstOrDefault(g => g.Title == filteredGame.Title && g.ExecutablePath == filteredGame.ExecutablePath);
                if (mainGame != null)
                {
                    mainGame.DisplayOrder = filteredGame.DisplayOrder;
                }
            }

            // ����δ��ʾ�ڵ�ǰɸѡ�е���Ϸ��Ϊ�����һ�������DisplayOrderֵ
            var maxDisplayOrder = FilteredItems.Count;
            foreach (var game in Items.Where(g => !FilteredItems.Contains(g)))
            {
                if (game.DisplayOrder < maxDisplayOrder)
                {
                    game.DisplayOrder = maxDisplayOrder++;
                }
            }
        }

        public void MoveCategoryGamesToUncategorized(string categoryId)
        {
            var affectedGames = Items.Where(g => g.CategoryId == categoryId).ToList();

            foreach (var game in affectedGames)
            {
                game.CategoryId = "uncategorized";
                game.Category = "δ����";
            }
        }

        /// <summary>
        /// ��Ϸ����JSON���л���
        /// </summary>
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
    }
}
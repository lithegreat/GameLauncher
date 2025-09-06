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
    /// 游戏数据管理器，负责游戏数据的加载、保存和基本操作
    /// </summary>
    public class GameDataManager
    {
        private const string GamesDataFileName = "games.json";
        private bool _isDataLoaded = false;

        public ObservableCollection<CustomDataObject> Items { get; } = new ObservableCollection<CustomDataObject>();
        public ObservableCollection<CustomDataObject> FilteredItems { get; } = new ObservableCollection<CustomDataObject>();

        public bool IsDataLoaded => _isDataLoaded;

        // 事件
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

                            // 按照显示顺序然后添加到集合
                            var sortedGames = gameDataList
                                .Where(g => g != null && !string.IsNullOrEmpty(g.Title))
                                .ToList();

                            // 为没有DisplayOrder的游戏分配序号（向后兼容）
                            for (int i = 0; i < sortedGames.Count; i++)
                            {
                                if (sortedGames[i].DisplayOrder == 0 && i > 0)
                                {
                                    sortedGames[i].DisplayOrder = i;
                                }
                            }

                            // 按DisplayOrder排序
                            sortedGames = sortedGames.OrderBy(g => g.DisplayOrder).ToList();

                            foreach (var gameJson in sortedGames)
                            {
                                BitmapImage? iconImage = null;

                                // 为有可执行文件路径的游戏提取图标
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
                                    Category = gameJson.Category ?? "未分类",
                                    Playtime = gameJson.Playtime,
                                    LastActivity = gameJson.LastActivity
                                };
                                Items.Add(game);
                            }

                            Debug.WriteLine($"已加载 {Items.Count} 个游戏，包括顺序恢复");
                        }
                    }
                }

                _isDataLoaded = true;
                DataLoaded?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadGamesData error: {ex.Message}");
                _isDataLoaded = true; // 即使失败也设置为已加载，避免无限重试
                throw;
            }
        }

        public async Task SaveGamesDataAsync()
        {
            try
            {
                Debug.WriteLine("开始保存游戏数据");

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

                Debug.WriteLine($"准备保存 {gameDataList.Count} 个游戏");

                var json = JsonSerializer.Serialize(gameDataList, new JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(file, json);

                Debug.WriteLine("游戏数据保存成功");
                DataSaved?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存游戏数据时发生异常: {ex.Message}");
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
            // 更新FilteredItems中所有游戏的DisplayOrder
            for (int i = 0; i < FilteredItems.Count; i++)
            {
                if (FilteredItems[i] is CustomDataObject game)
                {
                    game.DisplayOrder = i;
                }
            }

            // 找到FilteredItems中每个游戏在Items中的对应项并更新其DisplayOrder
            foreach (var filteredGame in FilteredItems)
            {
                var mainGame = Items.FirstOrDefault(g => g.Title == filteredGame.Title && g.ExecutablePath == filteredGame.ExecutablePath);
                if (mainGame != null)
                {
                    mainGame.DisplayOrder = filteredGame.DisplayOrder;
                }
            }

            // 对于未显示在当前筛选中的游戏，为其分配一个更大的DisplayOrder值
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
                game.Category = "未分类";
            }
        }

        /// <summary>
        /// 游戏数据JSON序列化类
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
            public string Category { get; set; } = "未分类";
            public ulong Playtime { get; set; } = 0;
            public DateTime? LastActivity { get; set; }
        }
    }
}
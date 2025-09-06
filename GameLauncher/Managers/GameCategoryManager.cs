using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using GameLauncher.Models;
using GameLauncher.Services;
using System.Diagnostics;

namespace GameLauncher.Managers
{
    /// <summary>
    /// 游戏分类管理器
    /// </summary>
    public class GameCategoryManager
    {
        private readonly GameDataManager _gameDataManager;
        private GameCategory? _selectedCategory;

        public event Action? CategoriesChanged;
        public event Action? FilterChanged;

        public GameCategoryManager(GameDataManager gameDataManager)
        {
            _gameDataManager = gameDataManager ?? throw new ArgumentNullException(nameof(gameDataManager));
        }

        public GameCategory? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    ApplyCategoryFilter();
                }
            }
        }

        public void ApplyCategoryFilter()
        {
            _gameDataManager.FilteredItems.Clear();

            IEnumerable<CustomDataObject> gamesToShow;

            if (_selectedCategory == null)
            {
                // 如果没有选择分类，显示所有游戏
                gamesToShow = _gameDataManager.Items;
            }
            else if (_selectedCategory.Id == "all")
            {
                // 显示所有游戏
                gamesToShow = _gameDataManager.Items;
            }
            else if (_selectedCategory.Id == "uncategorized")
            {
                // 显示未分类的游戏
                gamesToShow = _gameDataManager.Items.Where(g => string.IsNullOrEmpty(g.CategoryId) || g.CategoryId == "uncategorized");
            }
            else
            {
                // 显示特定分类的游戏
                gamesToShow = _gameDataManager.Items.Where(g => g.CategoryId == _selectedCategory.Id);
            }

            // 按照DisplayOrder排序后添加到FilteredItems
            var sortedGames = gamesToShow.OrderBy(g => g.DisplayOrder).ToList();
            foreach (var game in sortedGames)
            {
                _gameDataManager.FilteredItems.Add(game);
            }

            Debug.WriteLine($"分类筛选完成，显示 {_gameDataManager.FilteredItems.Count} 个游戏，按DisplayOrder排序");
            FilterChanged?.Invoke();
        }

        public void UpdateCategoryGameCounts()
        {
            CategoryService.Instance.UpdateCategoryGameCounts(_gameDataManager.Items);
            CategoriesChanged?.Invoke();
        }

        public async Task HandleCategoryDeleted(string deletedCategoryId)
        {
            try
            {
                // 将被删除分类下的所有游戏移动到"未分类"
                _gameDataManager.MoveCategoryGamesToUncategorized(deletedCategoryId);

                // 保存更新后的游戏数据
                await _gameDataManager.SaveGamesDataAsync();

                // 刷新UI
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理分类删除事件时出错: {ex.Message}");
                throw;
            }
        }

        public void InitializeDefaultCategory()
        {
            if (_selectedCategory == null)
            {
                _selectedCategory = CategoryService.Instance.Categories.FirstOrDefault(c => c.Id == "all");
            }
        }

        public async Task SetGameCategory(CustomDataObject game, string categoryId, string categoryName)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            game.CategoryId = categoryId;
            game.Category = categoryName;

            await _gameDataManager.SaveGamesDataAsync();

            // 确保UI立即更新以反映分类变更
            ApplyCategoryFilter();
            UpdateCategoryGameCounts();
        }
    }
}
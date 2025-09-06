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
    /// ��Ϸ���������
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
                // ���û��ѡ����࣬��ʾ������Ϸ
                gamesToShow = _gameDataManager.Items;
            }
            else if (_selectedCategory.Id == "all")
            {
                // ��ʾ������Ϸ
                gamesToShow = _gameDataManager.Items;
            }
            else if (_selectedCategory.Id == "uncategorized")
            {
                // ��ʾδ�������Ϸ
                gamesToShow = _gameDataManager.Items.Where(g => string.IsNullOrEmpty(g.CategoryId) || g.CategoryId == "uncategorized");
            }
            else
            {
                // ��ʾ�ض��������Ϸ
                gamesToShow = _gameDataManager.Items.Where(g => g.CategoryId == _selectedCategory.Id);
            }

            // ����DisplayOrder�������ӵ�FilteredItems
            var sortedGames = gamesToShow.OrderBy(g => g.DisplayOrder).ToList();
            foreach (var game in sortedGames)
            {
                _gameDataManager.FilteredItems.Add(game);
            }

            Debug.WriteLine($"����ɸѡ��ɣ���ʾ {_gameDataManager.FilteredItems.Count} ����Ϸ����DisplayOrder����");
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
                // ����ɾ�������µ�������Ϸ�ƶ���"δ����"
                _gameDataManager.MoveCategoryGamesToUncategorized(deletedCategoryId);

                // ������º����Ϸ����
                await _gameDataManager.SaveGamesDataAsync();

                // ˢ��UI
                ApplyCategoryFilter();
                UpdateCategoryGameCounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"�������ɾ���¼�ʱ����: {ex.Message}");
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

            // ȷ��UI���������Է�ӳ������
            ApplyCategoryFilter();
            UpdateCategoryGameCounts();
        }
    }
}
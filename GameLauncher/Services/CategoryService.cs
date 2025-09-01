using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using GameLauncher.Models;

namespace GameLauncher.Services
{
    public class CategoryService
    {
        private const string CategoriesDataFileName = "categories.json";
        private static CategoryService? _instance;
        private readonly ObservableCollection<GameCategory> _categories = new();

        public static CategoryService Instance => _instance ??= new CategoryService();

        public ObservableCollection<GameCategory> Categories => _categories;

        private CategoryService()
        {
            // 初始化默认分类
            InitializeDefaultCategories();
        }

        private void InitializeDefaultCategories()
        {
            _categories.Clear();
            _categories.Add(GameCategory.CreateAllGames());
            _categories.Add(GameCategory.CreateUncategorized());
        }

        /// <summary>
        /// 加载分类数据
        /// </summary>
        public async Task LoadCategoriesAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var file = await localFolder.TryGetItemAsync(CategoriesDataFileName) as StorageFile;

                if (file != null)
                {
                    var json = await FileIO.ReadTextAsync(file);

                    if (!string.IsNullOrEmpty(json))
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            WriteIndented = true
                        };

                        var categoriesData = JsonSerializer.Deserialize<CategoryDataJson[]>(json, options);

                        if (categoriesData != null && categoriesData.Length > 0)
                        {
                            // 保留默认分类，添加自定义分类
                            var customCategories = categoriesData
                                .Where(c => c != null && !string.IsNullOrEmpty(c.Name) && 
                                           c.Id != "all" && c.Id != "uncategorized")
                                .Select(c => new GameCategory
                                {
                                    Id = c.Id,
                                    Name = c.Name,
                                    Color = c.Color ?? "#2196F3"
                                });

                            foreach (var category in customCategories)
                            {
                                _categories.Add(category);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载分类数据时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存分类数据
        /// </summary>
        public async Task SaveCategoriesAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var file = await localFolder.CreateFileAsync(CategoriesDataFileName, CreationCollisionOption.ReplaceExisting);

                // 只保存自定义分类（不包括默认的"全部游戏"和"未分类"）
                var customCategories = _categories
                    .Where(c => c.Id != "all" && c.Id != "uncategorized")
                    .Select(c => new CategoryDataJson
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Color = c.Color
                    })
                    .ToArray();

                var json = JsonSerializer.Serialize(customCategories, new JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(file, json);

                System.Diagnostics.Debug.WriteLine($"分类数据保存成功，共保存 {customCategories.Length} 个自定义分类");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存分类数据时发生异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 添加新分类
        /// </summary>
        public async Task<GameCategory> AddCategoryAsync(string name, string color = "#2196F3")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("分类名称不能为空", nameof(name));

            // 检查是否已存在同名分类
            if (_categories.Any(c => c.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"分类 '{name}' 已存在");

            var newCategory = new GameCategory
            {
                Id = Guid.NewGuid().ToString(),
                Name = name.Trim(),
                Color = color
            };

            _categories.Add(newCategory);
            await SaveCategoriesAsync();

            return newCategory;
        }

        /// <summary>
        /// 删除分类
        /// </summary>
        public async Task<bool> DeleteCategoryAsync(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId))
                return false;

            // 不能删除默认分类
            if (categoryId == "all" || categoryId == "uncategorized")
                return false;

            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category != null)
            {
                _categories.Remove(category);
                await SaveCategoriesAsync();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新分类
        /// </summary>
        public async Task<bool> UpdateCategoryAsync(string categoryId, string newName, string newColor)
        {
            if (string.IsNullOrEmpty(categoryId) || string.IsNullOrWhiteSpace(newName))
                return false;

            // 不能修改默认分类
            if (categoryId == "all" || categoryId == "uncategorized")
                return false;

            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category != null)
            {
                // 检查新名称是否与其他分类冲突
                if (_categories.Any(c => c.Id != categoryId && 
                                        c.Name.Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"分类 '{newName}' 已存在");
                }

                category.Name = newName.Trim();
                category.Color = newColor ?? "#2196F3";
                await SaveCategoriesAsync();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 根据ID获取分类
        /// </summary>
        public GameCategory? GetCategoryById(string categoryId)
        {
            return _categories.FirstOrDefault(c => c.Id == categoryId);
        }

        /// <summary>
        /// 获取预设颜色信息（包含颜色代码和显示名称）
        /// </summary>
        public static ColorInfo[] GetPresetColorsWithNames()
        {
            return new[]
            {
                new ColorInfo("#2196F3", "蓝色", "经典蓝"),
                new ColorInfo("#4CAF50", "绿色", "生机绿"),
                new ColorInfo("#FF9800", "橙色", "活力橙"),
                new ColorInfo("#9C27B0", "紫色", "优雅紫"),
                new ColorInfo("#F44336", "红色", "热情红"),
                new ColorInfo("#00BCD4", "青色", "清新青"),
                new ColorInfo("#FFEB3B", "黄色", "明亮黄"),
                new ColorInfo("#795548", "棕色", "自然棕"),
                new ColorInfo("#607D8B", "灰蓝", "沉稳灰"),
                new ColorInfo("#E91E63", "粉色", "浪漫粉"),
                new ColorInfo("#3F51B5", "靛蓝", "深邃靛"),
                new ColorInfo("#8BC34A", "浅绿", "清新浅绿")
            };
        }

        /// <summary>
        /// 获取默认的预设颜色代码（向后兼容）
        /// </summary>
        public static string[] GetPresetColors()
        {
            return GetPresetColorsWithNames().Select(c => c.ColorCode).ToArray();
        }

        /// <summary>
        /// 更新分类的游戏数量
        /// </summary>
        public void UpdateCategoryGameCounts(IEnumerable<CustomDataObject> games)
        {
            var gameCounts = games.GroupBy(g => g.CategoryId)
                                 .ToDictionary(g => g.Key, g => g.Count());

            foreach (var category in _categories)
            {
                if (category.Id == "all")
                {
                    category.GameCount = games.Count();
                }
                else if (category.Id == "uncategorized")
                {
                    category.GameCount = games.Count(g => string.IsNullOrEmpty(g.CategoryId) || g.CategoryId == "uncategorized");
                }
                else
                {
                    category.GameCount = gameCounts.GetValueOrDefault(category.Id, 0);
                }
            }
        }
    }

    /// <summary>
    /// 颜色信息类
    /// </summary>
    public class ColorInfo
    {
        public string ColorCode { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        public ColorInfo(string colorCode, string displayName, string description)
        {
            ColorCode = colorCode;
            DisplayName = displayName;
            Description = description;
        }

        public override string ToString() => DisplayName;
    }

    public class CategoryDataJson
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#2196F3";
    }
}
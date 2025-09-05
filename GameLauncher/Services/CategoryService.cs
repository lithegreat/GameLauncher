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

            // 禁止删除默认分类
            if (categoryId == "all" || categoryId == "uncategorized")
                return false;

            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category != null)
            {
                _categories.Remove(category);
                await SaveCategoriesAsync();
                
                // 触发分类删除事件，让其他组件知道需要更新游戏数据
                CategoryDeleted?.Invoke(categoryId);
                
                return true;
            }

            return false;
        }

        /// <summary>
        /// 分类删除事件，用于通知其他组件更新游戏数据
        /// </summary>
        public event Action<string>? CategoryDeleted;

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
                // 第一行 - 经典色彩
                new ColorInfo("#2196F3", "蓝色", "经典蓝"),
                new ColorInfo("#4CAF50", "绿色", "生机绿"),
                new ColorInfo("#FF9800", "橙色", "活力橙"),
                new ColorInfo("#9C27B0", "紫色", "优雅紫"),
                new ColorInfo("#F44336", "红色", "热情红"),
                new ColorInfo("#00BCD4", "青色", "清新青"),
                
                // 第二行 - 明亮色彩
                new ColorInfo("#FFEB3B", "黄色", "明亮黄"),
                new ColorInfo("#E91E63", "粉色", "浪漫粉"),
                new ColorInfo("#3F51B5", "靛蓝", "深邃靛"),
                new ColorInfo("#8BC34A", "浅绿", "清新浅绿"),
                new ColorInfo("#FF5722", "深橙", "温暖橙"),
                new ColorInfo("#673AB7", "深紫", "神秘紫"),
                
                // 第三行 - 柔和色彩
                new ColorInfo("#795548", "棕色", "自然棕"),
                new ColorInfo("#607D8B", "灰蓝", "沉稳灰"),
                new ColorInfo("#FFC107", "琥珀", "温暖琥珀"),
                new ColorInfo("#009688", "蓝绿", "清新蓝绿"),
                new ColorInfo("#CDDC39", "青柠", "活力青柠"),
                new ColorInfo("#FF6F00", "深黄", "阳光黄"),
                
                // 第四行 - 深色系
                new ColorInfo("#37474F", "深灰", "商务灰"),
                new ColorInfo("#1565C0", "深蓝", "专业蓝"),
                new ColorInfo("#2E7D32", "深绿", "森林绿"),
                new ColorInfo("#C62828", "深红", "酒红色"),
                new ColorInfo("#6A1B9A", "深紫", "皇室紫"),
                new ColorInfo("#EF6C00", "深橙", "夕阳橙"),
                
                // 第五行 - 粉嫩色系
                new ColorInfo("#E1BEE7", "淡紫", "梦幻紫"),
                new ColorInfo("#FFCDD2", "淡粉", "温柔粉"),
                new ColorInfo("#C8E6C9", "淡绿", "薄荷绿"),
                new ColorInfo("#BBDEFB", "淡蓝", "天空蓝"),
                new ColorInfo("#FFE0B2", "淡橙", "蜜桃橙"),
                new ColorInfo("#F8BBD9", "樱花", "樱花粉")
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
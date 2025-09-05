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
            // ��ʼ��Ĭ�Ϸ���
            InitializeDefaultCategories();
        }

        private void InitializeDefaultCategories()
        {
            _categories.Clear();
            _categories.Add(GameCategory.CreateAllGames());
            _categories.Add(GameCategory.CreateUncategorized());
        }

        /// <summary>
        /// ���ط�������
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
                            // ����Ĭ�Ϸ��࣬����Զ������
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
                System.Diagnostics.Debug.WriteLine($"���ط�������ʱ�����쳣: {ex.Message}");
            }
        }

        /// <summary>
        /// �����������
        /// </summary>
        public async Task SaveCategoriesAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var file = await localFolder.CreateFileAsync(CategoriesDataFileName, CreationCollisionOption.ReplaceExisting);

                // ֻ�����Զ�����ࣨ������Ĭ�ϵ�"ȫ����Ϸ"��"δ����"��
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

                System.Diagnostics.Debug.WriteLine($"�������ݱ���ɹ��������� {customCategories.Length} ���Զ������");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�����������ʱ�����쳣: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ����·���
        /// </summary>
        public async Task<GameCategory> AddCategoryAsync(string name, string color = "#2196F3")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("�������Ʋ���Ϊ��", nameof(name));

            // ����Ƿ��Ѵ���ͬ������
            if (_categories.Any(c => c.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"���� '{name}' �Ѵ���");

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
        /// ɾ������
        /// </summary>
        public async Task<bool> DeleteCategoryAsync(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId))
                return false;

            // ��ֹɾ��Ĭ�Ϸ���
            if (categoryId == "all" || categoryId == "uncategorized")
                return false;

            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category != null)
            {
                _categories.Remove(category);
                await SaveCategoriesAsync();
                
                // ��������ɾ���¼������������֪����Ҫ������Ϸ����
                CategoryDeleted?.Invoke(categoryId);
                
                return true;
            }

            return false;
        }

        /// <summary>
        /// ����ɾ���¼�������֪ͨ�������������Ϸ����
        /// </summary>
        public event Action<string>? CategoryDeleted;

        /// <summary>
        /// ���·���
        /// </summary>
        public async Task<bool> UpdateCategoryAsync(string categoryId, string newName, string newColor)
        {
            if (string.IsNullOrEmpty(categoryId) || string.IsNullOrWhiteSpace(newName))
                return false;

            // �����޸�Ĭ�Ϸ���
            if (categoryId == "all" || categoryId == "uncategorized")
                return false;

            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            if (category != null)
            {
                // ����������Ƿ������������ͻ
                if (_categories.Any(c => c.Id != categoryId && 
                                        c.Name.Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"���� '{newName}' �Ѵ���");
                }

                category.Name = newName.Trim();
                category.Color = newColor ?? "#2196F3";
                await SaveCategoriesAsync();
                return true;
            }

            return false;
        }

        /// <summary>
        /// ����ID��ȡ����
        /// </summary>
        public GameCategory? GetCategoryById(string categoryId)
        {
            return _categories.FirstOrDefault(c => c.Id == categoryId);
        }

        /// <summary>
        /// ��ȡԤ����ɫ��Ϣ��������ɫ�������ʾ���ƣ�
        /// </summary>
        public static ColorInfo[] GetPresetColorsWithNames()
        {
            return new[]
            {
                // ��һ�� - ����ɫ��
                new ColorInfo("#2196F3", "��ɫ", "������"),
                new ColorInfo("#4CAF50", "��ɫ", "������"),
                new ColorInfo("#FF9800", "��ɫ", "������"),
                new ColorInfo("#9C27B0", "��ɫ", "������"),
                new ColorInfo("#F44336", "��ɫ", "�����"),
                new ColorInfo("#00BCD4", "��ɫ", "������"),
                
                // �ڶ��� - ����ɫ��
                new ColorInfo("#FFEB3B", "��ɫ", "������"),
                new ColorInfo("#E91E63", "��ɫ", "������"),
                new ColorInfo("#3F51B5", "����", "�����"),
                new ColorInfo("#8BC34A", "ǳ��", "����ǳ��"),
                new ColorInfo("#FF5722", "���", "��ů��"),
                new ColorInfo("#673AB7", "����", "������"),
                
                // ������ - ���ɫ��
                new ColorInfo("#795548", "��ɫ", "��Ȼ��"),
                new ColorInfo("#607D8B", "����", "���Ȼ�"),
                new ColorInfo("#FFC107", "����", "��ů����"),
                new ColorInfo("#009688", "����", "��������"),
                new ColorInfo("#CDDC39", "����", "��������"),
                new ColorInfo("#FF6F00", "���", "�����"),
                
                // ������ - ��ɫϵ
                new ColorInfo("#37474F", "���", "�����"),
                new ColorInfo("#1565C0", "����", "רҵ��"),
                new ColorInfo("#2E7D32", "����", "ɭ����"),
                new ColorInfo("#C62828", "���", "�ƺ�ɫ"),
                new ColorInfo("#6A1B9A", "����", "������"),
                new ColorInfo("#EF6C00", "���", "Ϧ����"),
                
                // ������ - ����ɫϵ
                new ColorInfo("#E1BEE7", "����", "�λ���"),
                new ColorInfo("#FFCDD2", "����", "�����"),
                new ColorInfo("#C8E6C9", "����", "������"),
                new ColorInfo("#BBDEFB", "����", "�����"),
                new ColorInfo("#FFE0B2", "����", "���ҳ�"),
                new ColorInfo("#F8BBD9", "ӣ��", "ӣ����")
            };
        }

        /// <summary>
        /// ��ȡĬ�ϵ�Ԥ����ɫ���루�����ݣ�
        /// </summary>
        public static string[] GetPresetColors()
        {
            return GetPresetColorsWithNames().Select(c => c.ColorCode).ToArray();
        }

        /// <summary>
        /// ���·������Ϸ����
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
    /// ��ɫ��Ϣ��
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
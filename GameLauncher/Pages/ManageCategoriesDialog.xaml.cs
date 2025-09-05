using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Data;
using GameLauncher.Models;
using GameLauncher.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GameLauncher.Pages
{
    public sealed partial class ManageCategoriesDialog : ContentDialog
    {
        private readonly ObservableCollection<GameCategory> _editableCategories = new();
        private bool _hasChanges = false;

        public ManageCategoriesDialog()
        {
            this.InitializeComponent();
            LoadCategories();
            RefreshCategoriesList();
        }

        private void LoadCategories()
        {
            _editableCategories.Clear();
            
            // 只加载可编辑的分类（排除"全部游戏"和"未分类"）
            foreach (var category in CategoryService.Instance.Categories)
            {
                if (category.Id != "all" && category.Id != "uncategorized")
                {
                    _editableCategories.Add(category);
                }
            }
        }

        private void RefreshCategoriesList()
        {
            CategoriesPanel.Children.Clear();
            
            if (_editableCategories.Count == 0)
            {
                // 显示空状态
                if (FindName("EmptyStatePanel") is Border emptyStatePanel)
                {
                    emptyStatePanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // 隐藏空状态
                if (FindName("EmptyStatePanel") is Border emptyStatePanel)
                {
                    emptyStatePanel.Visibility = Visibility.Collapsed;
                }
                
                // 添加分类项
                foreach (var category in _editableCategories)
                {
                    var categoryItem = CreateCategoryItem(category);
                    CategoriesPanel.Children.Add(categoryItem);
                }
            }
        }

        private Border CreateCategoryItem(GameCategory category)
        {
            var border = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(8),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 颜色指示器
            var colorIndicator = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            try
            {
                var converter = new ColorStringToColorConverter();
                var color = (Windows.UI.Color)converter.Convert(category.Color, typeof(Windows.UI.Color), null, "");
                colorIndicator.Fill = new SolidColorBrush(color);
            }
            catch
            {
                colorIndicator.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243));
            }

            Grid.SetColumn(colorIndicator, 0);
            grid.Children.Add(colorIndicator);

            // 分类信息
            var infoPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameText = new TextBlock
            {
                Text = category.Name,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Margin = new Thickness(0, 0, 0, 4)
            };

            var countText = new TextBlock
            {
                Text = category.DisplayText,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            infoPanel.Children.Add(nameText);
            infoPanel.Children.Add(countText);

            Grid.SetColumn(infoPanel, 1);
            grid.Children.Add(infoPanel);

            // 操作按钮
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 编辑按钮
            var editButton = new Button
            {
                Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                Tag = category
            };
            
            ToolTipService.SetToolTip(editButton, "编辑分类");
            
            var editIcon = new FontIcon 
            { 
                Glyph = "\uE70F", 
                FontSize = 16 
            };
            editButton.Content = editIcon;
            editButton.Click += EditCategoryButton_Click;

            // 删除按钮
            var deleteButton = new Button
            {
                Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                Tag = category
            };
            
            ToolTipService.SetToolTip(deleteButton, "删除分类");
            
            var deleteIcon = new FontIcon 
            { 
                Glyph = "\uE74D", 
                FontSize = 16,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
            };
            deleteButton.Content = deleteIcon;
            deleteButton.Click += DeleteCategoryButton_Click;

            buttonPanel.Children.Add(editButton);
            buttonPanel.Children.Add(deleteButton);

            Grid.SetColumn(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            border.Child = grid;
            return border;
        }

        private async void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowCategoryEditDialog(null);
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"添加分类时出错: {ex.Message}");
            }
        }

        private async void EditCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is GameCategory category)
                {
                    await ShowCategoryEditDialog(category);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"编辑分类时出错: {ex.Message}");
            }
        }

        private async void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is GameCategory category)
                {
                    await ShowDeleteCategoryConfirmation(category);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"删除分类时出错: {ex.Message}");
            }
        }

        private async Task ShowCategoryEditDialog(GameCategory? categoryToEdit)
        {
            bool isEditing = categoryToEdit != null;
            string title = isEditing ? "编辑分类" : "新建分类";

            // 隐藏主对话框
            this.Hide();

            try
            {
                var nameBox = new TextBox
                {
                    PlaceholderText = "请输入分类名称",
                    Text = categoryToEdit?.Name ?? "",
                    Margin = new Thickness(0, 10, 0, 20)
                };

                // 创建颜色选择区域
                var colorSelectionPanel = new StackPanel
                {
                    Margin = new Thickness(0, 10, 0, 0)
                };

                // 当前选中颜色指示器
                var selectedColorPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Margin = new Thickness(0, 0, 0, 16)
                };

                var selectedColorLabel = new TextBlock
                {
                    Text = "当前选择:",
                    Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                    VerticalAlignment = VerticalAlignment.Center
                };

                var selectedColorIndicator = new Microsoft.UI.Xaml.Shapes.Ellipse
                {
                    Width = 36,
                    Height = 36,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var selectedColorName = new TextBlock
                {
                    Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                    VerticalAlignment = VerticalAlignment.Center
                };

                selectedColorPanel.Children.Add(selectedColorLabel);
                selectedColorPanel.Children.Add(selectedColorIndicator);
                selectedColorPanel.Children.Add(selectedColorName);

                // 颜色网格
                var colorsGrid = CreateColorGrid();

                var presetColors = CategoryService.GetPresetColorsWithNames();
                string selectedColor = categoryToEdit?.Color ?? presetColors[0].ColorCode;
                var selectedColorInfo = presetColors.FirstOrDefault(c => c.ColorCode == selectedColor) ?? presetColors[0];

                // 更新选中颜色显示
                UpdateSelectedColorDisplay(selectedColorIndicator, selectedColorName, selectedColorInfo);

                // 创建颜色按钮
                for (int i = 0; i < presetColors.Length; i++)
                {
                    var colorInfo = presetColors[i];
                    var colorButton = CreateColorButton(colorInfo, selectedColor == colorInfo.ColorCode);
                    
                    // 为容器添加点击事件处理
                    if (colorButton is Border border)
                    {
                        border.Tapped += (s, args) =>
                        {
                            selectedColor = colorInfo.ColorCode;
                            
                            // 更新选中颜色显示
                            UpdateSelectedColorDisplay(selectedColorIndicator, selectedColorName, colorInfo);

                            // 更新所有按钮的选中状态
                            UpdateColorButtonsSelection(colorsGrid, selectedColor);
                        };
                    }

                    // 计算网格位置
                    int row = i / 6;
                    int col = i % 6;
                    Grid.SetRow(colorButton, row);
                    Grid.SetColumn(colorButton, col);
                    
                    colorsGrid.Children.Add(colorButton);
                }

                colorSelectionPanel.Children.Add(selectedColorPanel);
                colorSelectionPanel.Children.Add(new TextBlock 
                { 
                    Text = "选择颜色:", 
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                    Margin = new Thickness(0, 0, 0, 8)
                });
                colorSelectionPanel.Children.Add(colorsGrid);

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock 
                { 
                    Text = "分类名称:", 
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                });
                contentPanel.Children.Add(nameBox);
                contentPanel.Children.Add(colorSelectionPanel);

                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = new ScrollViewer 
                    { 
                        Content = contentPanel,
                        MaxHeight = 500,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                    },
                    PrimaryButtonText = "确定",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot,
                    DefaultButton = ContentDialogButton.Primary,
                    Width = 560
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var categoryName = nameBox.Text?.Trim();
                    
                    if (string.IsNullOrEmpty(categoryName))
                    {
                        await ShowErrorMessage("请输入分类名称");
                        return;
                    }

                    try
                    {
                        if (isEditing && categoryToEdit != null)
                        {
                            // 编辑现有分类
                            await CategoryService.Instance.UpdateCategoryAsync(categoryToEdit.Id, categoryName, selectedColor);
                        }
                        else
                        {
                            // 创建新分类
                            var newCategory = await CategoryService.Instance.AddCategoryAsync(categoryName, selectedColor);
                            _editableCategories.Add(newCategory);
                        }

                        _hasChanges = true;
                        RefreshCategoriesList();
                    }
                    catch (InvalidOperationException ex)
                    {
                        await ShowErrorMessage(ex.Message);
                    }
                }
            }
            finally
            {
                // 重新显示主对话框
                _ = this.ShowAsync();
            }
        }

        private Grid CreateColorGrid()
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 8, 0, 0)
            };

            // 创建6列的网格布局
            for (int i = 0; i < 6; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // 动态创建行（根据颜色数量）
            var presetColors = CategoryService.GetPresetColorsWithNames();
            int rows = (int)Math.Ceiling(presetColors.Length / 6.0);
            for (int i = 0; i < rows; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            return grid;
        }

        private FrameworkElement CreateColorButton(ColorInfo colorInfo, bool isSelected)
        {
            // 使用 Border 而不是 Button 来避免默认的悬停效果
            var container = new Border
            {
                Width = 56,
                Height = 56,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(28),
                BorderThickness = new Thickness(isSelected ? 3 : 2),
                BorderBrush = isSelected ? 
                    (Brush)Application.Current.Resources["AccentButtonBorderBrush"] :
                    (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
                Tag = colorInfo.ColorCode,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 设置工具提示
            ToolTipService.SetToolTip(container, $"{colorInfo.DisplayName} - {colorInfo.Description}");

            // 设置背景色
            try
            {
                var converter = new ColorStringToColorConverter();
                var color = (Windows.UI.Color)converter.Convert(colorInfo.ColorCode, typeof(Windows.UI.Color), null, "");
                container.Background = new SolidColorBrush(color);
            }
            catch
            {
                container.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243));
            }

            // 添加点击事件处理，而不使用鼠标指针事件（避免悬停效果）
            container.Tapped += (s, e) =>
            {
                // 点击事件将在外层处理
            };
            
            return container;
        }

        private void UpdateSelectedColorDisplay(Microsoft.UI.Xaml.Shapes.Ellipse indicator, TextBlock nameText, ColorInfo colorInfo)
        {
            try
            {
                var converter = new ColorStringToColorConverter();
                var color = (Windows.UI.Color)converter.Convert(colorInfo.ColorCode, typeof(Windows.UI.Color), null, "");
                indicator.Fill = new SolidColorBrush(color);
                nameText.Text = $"{colorInfo.DisplayName} - {colorInfo.Description}";
            }
            catch
            {
                indicator.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243));
                nameText.Text = "蓝色 - 默认色";
            }
        }

        private void UpdateColorButtonsSelection(Grid colorsGrid, string selectedColor)
        {
            foreach (var child in colorsGrid.Children)
            {
                if (child is Border border)
                {
                    bool isSelected = border.Tag?.ToString() == selectedColor;
                    
                    // 更新边框来显示选中状态
                    border.BorderThickness = new Thickness(isSelected ? 3 : 2);
                    border.BorderBrush = isSelected ? 
                        (Brush)Application.Current.Resources["AccentButtonBorderBrush"] :
                        (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
                }
            }
        }

        private async Task ShowDeleteCategoryConfirmation(GameCategory category)
        {
            // 隐藏主对话框
            this.Hide();

            try
            {
                var contentText = $"确定要删除分类 \"{category.Name}\" 吗？" + Environment.NewLine + Environment.NewLine + 
                                 "该分类下的所有游戏将被移动到\"未分类\"。此操作无法撤销。";
                
                var dialog = new ContentDialog
                {
                    Title = "确认删除",
                    Content = contentText,
                    PrimaryButtonText = "删除",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot,
                    DefaultButton = ContentDialogButton.Secondary
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    try
                    {
                        var success = await CategoryService.Instance.DeleteCategoryAsync(category.Id);
                        if (success)
                        {
                            _editableCategories.Remove(category);
                            _hasChanges = true;
                            RefreshCategoriesList();
                        }
                        else
                        {
                            await ShowErrorMessage("删除分类失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        await ShowErrorMessage($"删除分类时出错: {ex.Message}");
                    }
                }
            }
            finally
            {
                // 重新显示主对话框
                _ = this.ShowAsync();
            }
        }

        private async Task ShowErrorMessage(string message)
        {
            // 隐藏主对话框
            this.Hide();

            try
            {
                var dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = message,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            finally
            {
                // 重新显示主对话框
                _ = this.ShowAsync();
            }
        }
    }
}
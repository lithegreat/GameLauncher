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
            
            // ֻ���ؿɱ༭�ķ��ࣨ�ų�"ȫ����Ϸ"��"δ����"��
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
                // ��ʾ��״̬
                if (FindName("EmptyStatePanel") is Border emptyStatePanel)
                {
                    emptyStatePanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // ���ؿ�״̬
                if (FindName("EmptyStatePanel") is Border emptyStatePanel)
                {
                    emptyStatePanel.Visibility = Visibility.Collapsed;
                }
                
                // ��ӷ�����
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

            // ��ɫָʾ��
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

            // ������Ϣ
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

            // ������ť
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            // �༭��ť
            var editButton = new Button
            {
                Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                Tag = category
            };
            
            ToolTipService.SetToolTip(editButton, "�༭����");
            
            var editIcon = new FontIcon 
            { 
                Glyph = "\uE70F", 
                FontSize = 16 
            };
            editButton.Content = editIcon;
            editButton.Click += EditCategoryButton_Click;

            // ɾ����ť
            var deleteButton = new Button
            {
                Style = (Style)Application.Current.Resources["DefaultButtonStyle"],
                Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                Tag = category
            };
            
            ToolTipService.SetToolTip(deleteButton, "ɾ������");
            
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
                await ShowErrorMessage($"��ӷ���ʱ����: {ex.Message}");
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
                await ShowErrorMessage($"�༭����ʱ����: {ex.Message}");
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
                await ShowErrorMessage($"ɾ������ʱ����: {ex.Message}");
            }
        }

        private async Task ShowCategoryEditDialog(GameCategory? categoryToEdit)
        {
            bool isEditing = categoryToEdit != null;
            string title = isEditing ? "�༭����" : "�½�����";

            // �������Ի���
            this.Hide();

            try
            {
                var nameBox = new TextBox
                {
                    PlaceholderText = "�������������",
                    Text = categoryToEdit?.Name ?? "",
                    Margin = new Thickness(0, 10, 0, 20)
                };

                // ������ɫѡ������
                var colorSelectionPanel = new StackPanel
                {
                    Margin = new Thickness(0, 10, 0, 0)
                };

                // ��ǰѡ����ɫָʾ��
                var selectedColorPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Margin = new Thickness(0, 0, 0, 16)
                };

                var selectedColorLabel = new TextBlock
                {
                    Text = "��ǰѡ��:",
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

                // ��ɫ����
                var colorsGrid = CreateColorGrid();

                var presetColors = CategoryService.GetPresetColorsWithNames();
                string selectedColor = categoryToEdit?.Color ?? presetColors[0].ColorCode;
                var selectedColorInfo = presetColors.FirstOrDefault(c => c.ColorCode == selectedColor) ?? presetColors[0];

                // ����ѡ����ɫ��ʾ
                UpdateSelectedColorDisplay(selectedColorIndicator, selectedColorName, selectedColorInfo);

                // ������ɫ��ť
                for (int i = 0; i < presetColors.Length; i++)
                {
                    var colorInfo = presetColors[i];
                    var colorButton = CreateColorButton(colorInfo, selectedColor == colorInfo.ColorCode);
                    
                    // Ϊ������ӵ���¼�����
                    if (colorButton is Border border)
                    {
                        border.Tapped += (s, args) =>
                        {
                            selectedColor = colorInfo.ColorCode;
                            
                            // ����ѡ����ɫ��ʾ
                            UpdateSelectedColorDisplay(selectedColorIndicator, selectedColorName, colorInfo);

                            // �������а�ť��ѡ��״̬
                            UpdateColorButtonsSelection(colorsGrid, selectedColor);
                        };
                    }

                    // ��������λ��
                    int row = i / 6;
                    int col = i % 6;
                    Grid.SetRow(colorButton, row);
                    Grid.SetColumn(colorButton, col);
                    
                    colorsGrid.Children.Add(colorButton);
                }

                colorSelectionPanel.Children.Add(selectedColorPanel);
                colorSelectionPanel.Children.Add(new TextBlock 
                { 
                    Text = "ѡ����ɫ:", 
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                    Margin = new Thickness(0, 0, 0, 8)
                });
                colorSelectionPanel.Children.Add(colorsGrid);

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock 
                { 
                    Text = "��������:", 
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
                    PrimaryButtonText = "ȷ��",
                    SecondaryButtonText = "ȡ��",
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
                        await ShowErrorMessage("�������������");
                        return;
                    }

                    try
                    {
                        if (isEditing && categoryToEdit != null)
                        {
                            // �༭���з���
                            await CategoryService.Instance.UpdateCategoryAsync(categoryToEdit.Id, categoryName, selectedColor);
                        }
                        else
                        {
                            // �����·���
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
                // ������ʾ���Ի���
                _ = this.ShowAsync();
            }
        }

        private Grid CreateColorGrid()
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 8, 0, 0)
            };

            // ����6�е����񲼾�
            for (int i = 0; i < 6; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // ��̬�����У�������ɫ������
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
            // ʹ�� Border ������ Button ������Ĭ�ϵ���ͣЧ��
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

            // ���ù�����ʾ
            ToolTipService.SetToolTip(container, $"{colorInfo.DisplayName} - {colorInfo.Description}");

            // ���ñ���ɫ
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

            // ��ӵ���¼���������ʹ�����ָ���¼���������ͣЧ����
            container.Tapped += (s, e) =>
            {
                // ����¼�������㴦��
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
                nameText.Text = "��ɫ - Ĭ��ɫ";
            }
        }

        private void UpdateColorButtonsSelection(Grid colorsGrid, string selectedColor)
        {
            foreach (var child in colorsGrid.Children)
            {
                if (child is Border border)
                {
                    bool isSelected = border.Tag?.ToString() == selectedColor;
                    
                    // ���±߿�����ʾѡ��״̬
                    border.BorderThickness = new Thickness(isSelected ? 3 : 2);
                    border.BorderBrush = isSelected ? 
                        (Brush)Application.Current.Resources["AccentButtonBorderBrush"] :
                        (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
                }
            }
        }

        private async Task ShowDeleteCategoryConfirmation(GameCategory category)
        {
            // �������Ի���
            this.Hide();

            try
            {
                var contentText = $"ȷ��Ҫɾ������ \"{category.Name}\" ��" + Environment.NewLine + Environment.NewLine + 
                                 "�÷����µ�������Ϸ�����ƶ���\"δ����\"���˲����޷�������";
                
                var dialog = new ContentDialog
                {
                    Title = "ȷ��ɾ��",
                    Content = contentText,
                    PrimaryButtonText = "ɾ��",
                    SecondaryButtonText = "ȡ��",
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
                            await ShowErrorMessage("ɾ������ʧ��");
                        }
                    }
                    catch (Exception ex)
                    {
                        await ShowErrorMessage($"ɾ������ʱ����: {ex.Message}");
                    }
                }
            }
            finally
            {
                // ������ʾ���Ի���
                _ = this.ShowAsync();
            }
        }

        private async Task ShowErrorMessage(string message)
        {
            // �������Ի���
            this.Hide();

            try
            {
                var dialog = new ContentDialog
                {
                    Title = "����",
                    Content = message,
                    CloseButtonText = "ȷ��",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            finally
            {
                // ������ʾ���Ի���
                _ = this.ShowAsync();
            }
        }
    }
}
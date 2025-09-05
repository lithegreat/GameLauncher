using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GameLauncher
{
    public class CustomDataObject : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _executablePath = string.Empty;
        private BitmapImage? _iconImage;
        private string _steamAppId = string.Empty;
        private bool _isSteamGame = false;
        private string _xboxPackageFamilyName = string.Empty;
        private bool _isXboxGame = false;
        private int _displayOrder = 0;
        private string _categoryId = string.Empty;
        private string _category = "未分类";
        private ulong _playtime = 0;
        private DateTime? _lastActivity;

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public string ExecutablePath
        {
            get => _executablePath;
            set
            {
                if (_executablePath != value)
                {
                    _executablePath = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public BitmapImage? IconImage
        {
            get => _iconImage;
            set
            {
                if (_iconImage != value)
                {
                    _iconImage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Steam AppID (仅适用于 Steam 游戏)
        /// </summary>
        public string SteamAppId
        {
            get => _steamAppId;
            set
            {
                if (_steamAppId != value)
                {
                    _steamAppId = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否为 Steam 游戏
        /// </summary>
        public bool IsSteamGame
        {
            get => _isSteamGame;
            set
            {
                if (_isSteamGame != value)
                {
                    _isSteamGame = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Xbox Package Family Name (仅适用于 Xbox 游戏)
        /// </summary>
        public string XboxPackageFamilyName
        {
            get => _xboxPackageFamilyName;
            set
            {
                if (_xboxPackageFamilyName != value)
                {
                    _xboxPackageFamilyName = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否为 Xbox 游戏
        /// </summary>
        public bool IsXboxGame
        {
            get => _isXboxGame;
            set
            {
                if (_isXboxGame != value)
                {
                    _isXboxGame = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 显示顺序，用于保存用户拖拽排序
        /// </summary>
        public int DisplayOrder
        {
            get => _displayOrder;
            set
            {
                if (_displayOrder != value)
                {
                    _displayOrder = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 游戏分类ID
        /// </summary>
        public string CategoryId
        {
            get => _categoryId;
            set
            {
                if (_categoryId != value)
                {
                    _categoryId = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CategoryColor)); // 通知颜色属性也发生了变化
                }
            }
        }

        /// <summary>
        /// 游戏分类名称
        /// </summary>
        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value ?? "未分类";
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 游戏时长（分钟）
        /// </summary>
        public ulong Playtime
        {
            get => _playtime;
            set
            {
                if (_playtime != value)
                {
                    _playtime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlaytimeFormatted));
                }
            }
        }

        /// <summary>
        /// 最后游玩时间
        /// </summary>
        public DateTime? LastActivity
        {
            get => _lastActivity;
            set
            {
                if (_lastActivity != value)
                {
                    _lastActivity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LastActivityFormatted));
                }
            }
        }

        /// <summary>
        /// 格式化的游戏时长字符串
        /// </summary>
        public string PlaytimeFormatted
        {
            get
            {
                if (_playtime == 0)
                    return "未游玩";

                var hours = _playtime / 60;
                var minutes = _playtime % 60;

                if (hours == 0)
                    return $"{minutes} 分钟";
                
                if (minutes == 0)
                    return $"{hours} 小时";
                    
                return $"{hours} 小时 {minutes} 分钟";
            }
        }

        /// <summary>
        /// 格式化的最后游玩时间字符串
        /// </summary>
        public string LastActivityFormatted
        {
            get
            {
                if (!_lastActivity.HasValue)
                    return "未知";

                var now = DateTime.Now;
                var timeSpan = now - _lastActivity.Value;

                if (timeSpan.TotalDays < 1)
                {
                    if (timeSpan.TotalHours < 1)
                    {
                        if (timeSpan.TotalMinutes < 1)
                            return "刚刚";
                        return $"{(int)timeSpan.TotalMinutes} 分钟前";
                    }
                    return $"{(int)timeSpan.TotalHours} 小时前";
                }
                else if (timeSpan.TotalDays < 7)
                {
                    return $"{(int)timeSpan.TotalDays} 天前";
                }
                else if (timeSpan.TotalDays < 30)
                {
                    return $"{(int)(timeSpan.TotalDays / 7)} 周前";
                }
                else if (timeSpan.TotalDays < 365)
                {
                    return $"{(int)(timeSpan.TotalDays / 30)} 月前";
                }
                else
                {
                    return $"{(int)(timeSpan.TotalDays / 365)} 年前";
                }
            }
        }

        /// <summary>
        /// 分类颜色，十六进制颜色字符串
        /// </summary>
        public string CategoryColor
        {
            get
            {
                // 根据 CategoryId 从 CategoryService 获取颜色
                var categoryService = GameLauncher.Services.CategoryService.Instance;
                var category = categoryService.GetCategoryById(_categoryId);
                return category?.Color ?? "#757575"; // 默认灰色
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
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
        private string _category = "δ����";
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
        /// Steam AppID (�������� Steam ��Ϸ)
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
        /// �Ƿ�Ϊ Steam ��Ϸ
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
        /// Xbox Package Family Name (�������� Xbox ��Ϸ)
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
        /// �Ƿ�Ϊ Xbox ��Ϸ
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
        /// ��ʾ˳�����ڱ����û���ק����
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
        /// ��Ϸ����ID
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
                    OnPropertyChanged(nameof(CategoryColor)); // ֪ͨ��ɫ����Ҳ�����˱仯
                }
            }
        }

        /// <summary>
        /// ��Ϸ��������
        /// </summary>
        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value ?? "δ����";
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// ��Ϸʱ�������ӣ�
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
        /// �������ʱ��
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
        /// ��ʽ������Ϸʱ���ַ���
        /// </summary>
        public string PlaytimeFormatted
        {
            get
            {
                if (_playtime == 0)
                    return "δ����";

                var hours = _playtime / 60;
                var minutes = _playtime % 60;

                if (hours == 0)
                    return $"{minutes} ����";
                
                if (minutes == 0)
                    return $"{hours} Сʱ";
                    
                return $"{hours} Сʱ {minutes} ����";
            }
        }

        /// <summary>
        /// ��ʽ�����������ʱ���ַ���
        /// </summary>
        public string LastActivityFormatted
        {
            get
            {
                if (!_lastActivity.HasValue)
                    return "δ֪";

                var now = DateTime.Now;
                var timeSpan = now - _lastActivity.Value;

                if (timeSpan.TotalDays < 1)
                {
                    if (timeSpan.TotalHours < 1)
                    {
                        if (timeSpan.TotalMinutes < 1)
                            return "�ո�";
                        return $"{(int)timeSpan.TotalMinutes} ����ǰ";
                    }
                    return $"{(int)timeSpan.TotalHours} Сʱǰ";
                }
                else if (timeSpan.TotalDays < 7)
                {
                    return $"{(int)timeSpan.TotalDays} ��ǰ";
                }
                else if (timeSpan.TotalDays < 30)
                {
                    return $"{(int)(timeSpan.TotalDays / 7)} ��ǰ";
                }
                else if (timeSpan.TotalDays < 365)
                {
                    return $"{(int)(timeSpan.TotalDays / 30)} ��ǰ";
                }
                else
                {
                    return $"{(int)(timeSpan.TotalDays / 365)} ��ǰ";
                }
            }
        }

        /// <summary>
        /// ������ɫ��ʮ��������ɫ�ַ���
        /// </summary>
        public string CategoryColor
        {
            get
            {
                // ���� CategoryId �� CategoryService ��ȡ��ɫ
                var categoryService = GameLauncher.Services.CategoryService.Instance;
                var category = categoryService.GetCategoryById(_categoryId);
                return category?.Color ?? "#757575"; // Ĭ�ϻ�ɫ
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
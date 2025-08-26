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
        /// Steam AppID (如果是 Steam 游戏)
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
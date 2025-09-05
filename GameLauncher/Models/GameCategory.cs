using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameLauncher.Models
{
    public class GameCategory : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _color = "#2196F3";
        private int _gameCount = 0;

        /// <summary>
        /// ����Ψһ��ʶ��
        /// </summary>
        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanEdit));
                    OnPropertyChanged(nameof(CanDelete));
                }
            }
        }

        /// <summary>
        /// ��������
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// ������ɫ��ʮ��������ɫ���룩
        /// </summary>
        public string Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value ?? "#2196F3";
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// �÷����µ���Ϸ����
        /// </summary>
        public int GameCount
        {
            get => _gameCount;
            set
            {
                if (_gameCount != value)
                {
                    _gameCount = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// ��ʾ�ı���������Ϸ������
        /// </summary>
        public string DisplayText => $"{Name} ({GameCount})";

        /// <summary>
        /// �Ƿ���Ա༭��ϵͳĬ�Ϸ��಻�ɱ༭��
        /// </summary>
        public bool CanEdit => Id != "all" && Id != "uncategorized";

        /// <summary>
        /// �Ƿ����ɾ����ϵͳĬ�Ϸ��಻��ɾ����
        /// </summary>
        public bool CanDelete => Id != "all" && Id != "uncategorized";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// ����Ĭ�ϵ�"δ����"����
        /// </summary>
        public static GameCategory CreateUncategorized()
        {
            return new GameCategory
            {
                Id = "uncategorized",
                Name = "δ����",
                Color = "#757575"
            };
        }

        /// <summary>
        /// ����"ȫ����Ϸ"����
        /// </summary>
        public static GameCategory CreateAllGames()
        {
            return new GameCategory
            {
                Id = "all",
                Name = "ȫ����Ϸ",
                Color = "#4CAF50"
            };
        }
    }
}
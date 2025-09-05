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
        /// 分类唯一标识符
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
        /// 分类名称
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
        /// 分类颜色（十六进制颜色代码）
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
        /// 该分类下的游戏数量
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
        /// 显示文本（包含游戏数量）
        /// </summary>
        public string DisplayText => $"{Name} ({GameCount})";

        /// <summary>
        /// 是否可以编辑（系统默认分类不可编辑）
        /// </summary>
        public bool CanEdit => Id != "all" && Id != "uncategorized";

        /// <summary>
        /// 是否可以删除（系统默认分类不可删除）
        /// </summary>
        public bool CanDelete => Id != "all" && Id != "uncategorized";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 创建默认的"未分类"分类
        /// </summary>
        public static GameCategory CreateUncategorized()
        {
            return new GameCategory
            {
                Id = "uncategorized",
                Name = "未分类",
                Color = "#757575"
            };
        }

        /// <summary>
        /// 创建"全部游戏"分类
        /// </summary>
        public static GameCategory CreateAllGames()
        {
            return new GameCategory
            {
                Id = "all",
                Name = "全部游戏",
                Color = "#4CAF50"
            };
        }
    }
}
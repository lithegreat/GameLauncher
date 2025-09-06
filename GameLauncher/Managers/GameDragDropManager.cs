using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameLauncher.Models;
using System.Diagnostics;

namespace GameLauncher.Managers
{
    /// <summary>
    /// 拖拽排序管理器
    /// </summary>
    public class GameDragDropManager
    {
        private readonly ListView _gamesList;
        private readonly GameDataManager _gameDataManager;
        private readonly GameCategoryManager _categoryManager;
        private readonly GameDialogManager _dialogManager;

        public GameDragDropManager(ListView gamesList, GameDataManager gameDataManager, 
            GameCategoryManager categoryManager, GameDialogManager dialogManager)
        {
            _gamesList = gamesList ?? throw new ArgumentNullException(nameof(gamesList));
            _gameDataManager = gameDataManager ?? throw new ArgumentNullException(nameof(gameDataManager));
            _categoryManager = categoryManager ?? throw new ArgumentNullException(nameof(categoryManager));
            _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));

            // 注册拖拽事件
            _gamesList.DragItemsStarting += OnDragItemsStarting;
            _gamesList.DragItemsCompleted += OnDragItemsCompleted;
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                Debug.WriteLine($"开始拖拽游戏，拖拽项数量: {e.Items.Count}");

                // 在拖拽开始时记录当前顺序，用于后续比较是否发生了变化
                foreach (var item in _gameDataManager.FilteredItems)
                {
                    if (item is CustomDataObject game)
                    {
                        var index = _gameDataManager.FilteredItems.IndexOf(item);
                        game.DisplayOrder = index;
                        Debug.WriteLine($"记录游戏 {game.Title} 的原始顺序: {index}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"拖拽开始事件异常: {ex.Message}");
            }
        }

        private async void OnDragItemsCompleted(object sender, DragItemsCompletedEventArgs e)
        {
            try
            {
                Debug.WriteLine("拖拽完成，开始更新游戏顺序");

                bool orderChanged = false;

                // 更新FilteredItems中所有游戏的DisplayOrder
                for (int i = 0; i < _gameDataManager.FilteredItems.Count; i++)
                {
                    if (_gameDataManager.FilteredItems[i] is CustomDataObject game)
                    {
                        if (game.DisplayOrder != i)
                        {
                            Debug.WriteLine($"游戏 {game.Title} 顺序改变: {game.DisplayOrder} -> {i}");
                            game.DisplayOrder = i;
                            orderChanged = true;
                        }
                    }
                }

                // 如果顺序发生了变化，需要同步到Items集合并保存
                if (orderChanged)
                {
                    Debug.WriteLine("游戏顺序发生变化，开始同步到主集合");

                    _gameDataManager.UpdateGameOrder();

                    Debug.WriteLine("开始保存游戏数据");
                    await _gameDataManager.SaveGamesDataAsync();
                    Debug.WriteLine("游戏顺序保存完成");

                    // 强制刷新UI以确保显示最新的排序
                    Debug.WriteLine("刷新UI显示");
                    await Task.Run(() =>
                    {
                        _gamesList.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                _categoryManager.ApplyCategoryFilter();
                                Debug.WriteLine("UI刷新完成");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"UI刷新异常: {ex.Message}");
                            }
                        });
                    });
                }
                else
                {
                    Debug.WriteLine("游戏顺序未发生变化，无需保存");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"拖拽完成事件异常: {ex.Message}");
                await _dialogManager.ShowErrorDialogAsync($"保存游戏顺序时出错: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_gamesList != null)
            {
                _gamesList.DragItemsStarting -= OnDragItemsStarting;
                _gamesList.DragItemsCompleted -= OnDragItemsCompleted;
            }
        }
    }
}
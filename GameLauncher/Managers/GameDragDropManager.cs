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
    /// ��ק���������
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

            // ע����ק�¼�
            _gamesList.DragItemsStarting += OnDragItemsStarting;
            _gamesList.DragItemsCompleted += OnDragItemsCompleted;
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                Debug.WriteLine($"��ʼ��ק��Ϸ����ק������: {e.Items.Count}");

                // ����ק��ʼʱ��¼��ǰ˳�����ں����Ƚ��Ƿ����˱仯
                foreach (var item in _gameDataManager.FilteredItems)
                {
                    if (item is CustomDataObject game)
                    {
                        var index = _gameDataManager.FilteredItems.IndexOf(item);
                        game.DisplayOrder = index;
                        Debug.WriteLine($"��¼��Ϸ {game.Title} ��ԭʼ˳��: {index}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"��ק��ʼ�¼��쳣: {ex.Message}");
            }
        }

        private async void OnDragItemsCompleted(object sender, DragItemsCompletedEventArgs e)
        {
            try
            {
                Debug.WriteLine("��ק��ɣ���ʼ������Ϸ˳��");

                bool orderChanged = false;

                // ����FilteredItems��������Ϸ��DisplayOrder
                for (int i = 0; i < _gameDataManager.FilteredItems.Count; i++)
                {
                    if (_gameDataManager.FilteredItems[i] is CustomDataObject game)
                    {
                        if (game.DisplayOrder != i)
                        {
                            Debug.WriteLine($"��Ϸ {game.Title} ˳��ı�: {game.DisplayOrder} -> {i}");
                            game.DisplayOrder = i;
                            orderChanged = true;
                        }
                    }
                }

                // ���˳�����˱仯����Ҫͬ����Items���ϲ�����
                if (orderChanged)
                {
                    Debug.WriteLine("��Ϸ˳�����仯����ʼͬ����������");

                    _gameDataManager.UpdateGameOrder();

                    Debug.WriteLine("��ʼ������Ϸ����");
                    await _gameDataManager.SaveGamesDataAsync();
                    Debug.WriteLine("��Ϸ˳�򱣴����");

                    // ǿ��ˢ��UI��ȷ����ʾ���µ�����
                    Debug.WriteLine("ˢ��UI��ʾ");
                    await Task.Run(() =>
                    {
                        _gamesList.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                _categoryManager.ApplyCategoryFilter();
                                Debug.WriteLine("UIˢ�����");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"UIˢ���쳣: {ex.Message}");
                            }
                        });
                    });
                }
                else
                {
                    Debug.WriteLine("��Ϸ˳��δ�����仯�����豣��");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"��ק����¼��쳣: {ex.Message}");
                await _dialogManager.ShowErrorDialogAsync($"������Ϸ˳��ʱ����: {ex.Message}");
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
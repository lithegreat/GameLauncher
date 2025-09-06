using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using GameLauncher.Models;
using GameLauncher.Services;
using System.Diagnostics;
using System.IO;

namespace GameLauncher.Managers
{
    /// <summary>
    /// ��Ϸ������������������Ϸ������ɾ���Ȳ���
    /// </summary>
    public class GameOperationManager
    {
        private readonly GameDataManager _gameDataManager;
        private readonly GameCategoryManager _categoryManager;
        private readonly GameDialogManager _dialogManager;

        public GameOperationManager(GameDataManager gameDataManager, 
            GameCategoryManager categoryManager, GameDialogManager dialogManager)
        {
            _gameDataManager = gameDataManager ?? throw new ArgumentNullException(nameof(gameDataManager));
            _categoryManager = categoryManager ?? throw new ArgumentNullException(nameof(categoryManager));
            _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        }

        public async Task LaunchGameAsync(CustomDataObject game)
        {
            try
            {
                if (game == null)
                {
                    await _dialogManager.ShowErrorDialogAsync("��Ϸ������Ч");
                    return;
                }

                // ����� Steam ��Ϸ������ʹ�� Steam Э������
                if (game.IsSteamGame && !string.IsNullOrEmpty(game.SteamAppId))
                {
                    Debug.WriteLine($"ͨ�� Steam ������Ϸ: {game.Title} (AppID: {game.SteamAppId})");

                    if (Services.SteamService.LaunchSteamGame(game.SteamAppId))
                    {
                        return; // Steam �����ɹ�
                    }
                    else
                    {
                        Debug.WriteLine("Steam ����ʧ�ܣ�����ֱ�����п�ִ���ļ�");
                    }
                }

                // ����� Xbox ��Ϸ������ʹ�� Xbox Э������
                if (game.IsXboxGame && !string.IsNullOrEmpty(game.XboxPackageFamilyName))
                {
                    Debug.WriteLine($"ͨ�� Xbox ������Ϸ: {game.Title} (Package: {game.XboxPackageFamilyName})");

                    if (Services.XboxService.LaunchXboxGame(game.XboxPackageFamilyName))
                    {
                        return; // Xbox �����ɹ�
                    }
                    else
                    {
                        Debug.WriteLine("Xbox ����ʧ�ܣ�����ֱ�����п�ִ���ļ�");

                        // ����ͨ����ִ���ļ����� Xbox ��Ϸ
                        if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                        {
                            if (Services.XboxService.LaunchXboxGameByExecutable(game.ExecutablePath))
                            {
                                return; // ͨ����ִ���ļ������ɹ�
                            }
                        }
                    }
                }

                // ֱ�����п�ִ���ļ��������� Steam ��Ϸ�� Steam ����ʧ��ʱ�ı�ѡ������
                if (string.IsNullOrEmpty(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
                {
                    await _dialogManager.ShowErrorDialogAsync($"��Ϸ�ļ�������: {game.ExecutablePath}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                Debug.WriteLine($"ֱ��������Ϸ: {game.Title}");
            }
            catch (Exception ex)
            {
                await _dialogManager.ShowErrorDialogAsync($"������Ϸʧ��: {ex.Message}");
            }
        }

        public async Task OpenGameDirectoryAsync(CustomDataObject game)
        {
            try
            {
                if (string.IsNullOrEmpty(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
                {
                    await _dialogManager.ShowErrorDialogAsync("��Ϸ�ļ������ڣ��޷���Ŀ¼");
                    return;
                }

                var directory = Path.GetDirectoryName(game.ExecutablePath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{directory}\"",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                else
                {
                    await _dialogManager.ShowErrorDialogAsync("��ϷĿ¼������");
                }
            }
            catch (Exception ex)
            {
                await _dialogManager.ShowErrorDialogAsync($"����ϷĿ¼ʱ����: {ex.Message}");
            }
        }

        public async Task OpenInSteamAsync(CustomDataObject game)
        {
            try
            {
                if (game.IsSteamGame && !string.IsNullOrEmpty(game.SteamAppId))
                {
                    var steamStoreUrl = $"https://store.steampowered.com/app/{game.SteamAppId}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = steamStoreUrl,
                        UseShellExecute = true
                    });
                }
                else
                {
                    await _dialogManager.ShowErrorDialogAsync("����Ϸ���� Steam ��Ϸ");
                }
            }
            catch (Exception ex)
            {
                await _dialogManager.ShowErrorDialogAsync($"�� Steam �д���Ϸʱ����: {ex.Message}");
            }
        }

        public async Task DeleteSingleGameAsync(CustomDataObject game)
        {
            try
            {
                Debug.WriteLine($"��ʼɾ����Ϸ: {game.Title}");

                bool confirmed = await _dialogManager.ShowDeleteConfirmationDialogAsync(
                    "ȷ��ɾ��",
                    $"ȷ��Ҫɾ����Ϸ \"{game.Title}\" �𣿴˲����޷�������");

                if (confirmed)
                {
                    Debug.WriteLine("�û�ȷ��ɾ������ʼִ��ɾ������");

                    // ȷ����Ϸ�ڼ����д���
                    if (_gameDataManager.Items.Contains(game))
                    {
                        _gameDataManager.RemoveGame(game);
                        Debug.WriteLine("��Ϸ�ѴӼ������Ƴ�");

                        // ������º������
                        await _gameDataManager.SaveGamesDataAsync();

                        // ȷ��UI��������
                        _categoryManager.ApplyCategoryFilter();
                        _categoryManager.UpdateCategoryGameCounts();

                        Debug.WriteLine("��Ϸ���ݱ������");
                    }
                    else
                    {
                        Debug.WriteLine("���棺��Ϸ���ڼ�����");
                    }
                }
                else
                {
                    Debug.WriteLine("�û�ȡ��ɾ������");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ɾ����Ϸʱ�����쳣: {ex.Message}");
                await _dialogManager.ShowErrorDialogAsync($"ɾ����Ϸʱ����: {ex.Message}");
            }
        }
    }
}
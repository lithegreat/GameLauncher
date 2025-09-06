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
    /// 游戏操作管理器，处理游戏启动、删除等操作
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
                    await _dialogManager.ShowErrorDialogAsync("游戏数据无效");
                    return;
                }

                // 如果是 Steam 游戏，优先使用 Steam 协议启动
                if (game.IsSteamGame && !string.IsNullOrEmpty(game.SteamAppId))
                {
                    Debug.WriteLine($"通过 Steam 启动游戏: {game.Title} (AppID: {game.SteamAppId})");

                    if (Services.SteamService.LaunchSteamGame(game.SteamAppId))
                    {
                        return; // Steam 启动成功
                    }
                    else
                    {
                        Debug.WriteLine("Steam 启动失败，尝试直接运行可执行文件");
                    }
                }

                // 如果是 Xbox 游戏，优先使用 Xbox 协议启动
                if (game.IsXboxGame && !string.IsNullOrEmpty(game.XboxPackageFamilyName))
                {
                    Debug.WriteLine($"通过 Xbox 启动游戏: {game.Title} (Package: {game.XboxPackageFamilyName})");

                    if (Services.XboxService.LaunchXboxGame(game.XboxPackageFamilyName))
                    {
                        return; // Xbox 启动成功
                    }
                    else
                    {
                        Debug.WriteLine("Xbox 启动失败，尝试直接运行可执行文件");

                        // 尝试通过可执行文件启动 Xbox 游戏
                        if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
                        {
                            if (Services.XboxService.LaunchXboxGameByExecutable(game.ExecutablePath))
                            {
                                return; // 通过可执行文件启动成功
                            }
                        }
                    }
                }

                // 直接运行可执行文件（适用于 Steam 游戏或 Steam 启动失败时的备选方案）
                if (string.IsNullOrEmpty(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
                {
                    await _dialogManager.ShowErrorDialogAsync($"游戏文件不存在: {game.ExecutablePath}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                Debug.WriteLine($"直接启动游戏: {game.Title}");
            }
            catch (Exception ex)
            {
                await _dialogManager.ShowErrorDialogAsync($"启动游戏失败: {ex.Message}");
            }
        }

        public async Task OpenGameDirectoryAsync(CustomDataObject game)
        {
            try
            {
                if (string.IsNullOrEmpty(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
                {
                    await _dialogManager.ShowErrorDialogAsync("游戏文件不存在，无法打开目录");
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
                    await _dialogManager.ShowErrorDialogAsync("游戏目录不存在");
                }
            }
            catch (Exception ex)
            {
                await _dialogManager.ShowErrorDialogAsync($"打开游戏目录时错误: {ex.Message}");
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
                    await _dialogManager.ShowErrorDialogAsync("该游戏不是 Steam 游戏");
                }
            }
            catch (Exception ex)
            {
                await _dialogManager.ShowErrorDialogAsync($"在 Steam 中打开游戏时错误: {ex.Message}");
            }
        }

        public async Task DeleteSingleGameAsync(CustomDataObject game)
        {
            try
            {
                Debug.WriteLine($"开始删除游戏: {game.Title}");

                bool confirmed = await _dialogManager.ShowDeleteConfirmationDialogAsync(
                    "确认删除",
                    $"确定要删除游戏 \"{game.Title}\" 吗？此操作无法撤销。");

                if (confirmed)
                {
                    Debug.WriteLine("用户确认删除，开始执行删除操作");

                    // 确保游戏在集合中存在
                    if (_gameDataManager.Items.Contains(game))
                    {
                        _gameDataManager.RemoveGame(game);
                        Debug.WriteLine("游戏已从集合中移除");

                        // 保存更新后的数据
                        await _gameDataManager.SaveGamesDataAsync();

                        // 确保UI立即更新
                        _categoryManager.ApplyCategoryFilter();
                        _categoryManager.UpdateCategoryGameCounts();

                        Debug.WriteLine("游戏数据保存完成");
                    }
                    else
                    {
                        Debug.WriteLine("警告：游戏不在集合中");
                    }
                }
                else
                {
                    Debug.WriteLine("用户取消删除操作");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"删除游戏时发生异常: {ex.Message}");
                await _dialogManager.ShowErrorDialogAsync($"删除游戏时出错: {ex.Message}");
            }
        }
    }
}
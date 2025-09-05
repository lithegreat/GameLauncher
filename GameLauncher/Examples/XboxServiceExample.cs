using System;
using System.Threading.Tasks;
using GameLauncher.Services;

namespace GameLauncher.Examples
{
    /// <summary>
    /// Xbox 服务使用示例
    /// </summary>
    public static class XboxServiceExample
    {
        /// <summary>
        /// 演示如何使用 Xbox 服务
        /// </summary>
        public static async Task RunExampleAsync()
        {
            Console.WriteLine("=== Xbox 服务功能演示 ===");

            // 1. 检查 Xbox Pass 应用是否安装
            Console.WriteLine($"Xbox Pass App 安装状态: {XboxService.IsXboxPassAppInstalled}");

            // 2. 扫描 Xbox 游戏
            Console.WriteLine("\n开始扫描 Xbox 游戏...");
            var games = await XboxService.ScanXboxGamesAsync();
            
            Console.WriteLine($"\n找到 {games.Count} 个 Xbox 游戏:");
            foreach (var game in games)
            {
                Console.WriteLine($"- {game.Name}");
                Console.WriteLine($"  ID: {game.AppId}");
                Console.WriteLine($"  安装位置: {game.InstallLocation}");
                Console.WriteLine($"  可执行文件: {game.ExecutablePath}");
                Console.WriteLine($"  发布商: {game.Publisher}");
                Console.WriteLine($"  版本: {game.Version}");
                Console.WriteLine();
            }

            // 3. 演示游戏启动功能
            if (games.Count > 0)
            {
                var firstGame = games[0];
                Console.WriteLine($"演示启动游戏: {firstGame.Name}");
                
                // 检查游戏是否正在运行
                var isRunning = XboxService.IsGameRunning(firstGame);
                Console.WriteLine($"游戏运行状态: {(isRunning ? "运行中" : "未运行")}");

                // 获取启动参数
                var launchArgs = XboxService.GetGameLaunchArguments(firstGame);
                Console.WriteLine($"启动参数: {launchArgs}");

                // 实际启动游戏（注释掉以避免意外启动）
                // var launched = XboxService.LaunchXboxGame(firstGame.PackageFamilyName);
                // Console.WriteLine($"启动结果: {(launched ? "成功" : "失败")}");
            }

            // 4. 演示打开 Xbox Pass 应用
            Console.WriteLine("\n演示打开 Xbox Pass 应用:");
            if (XboxService.IsXboxPassAppInstalled)
            {
                // 实际打开应用（注释掉以避免意外打开）
                // var opened = XboxService.OpenXboxPassApp();
                // Console.WriteLine($"打开结果: {(opened ? "成功" : "失败")}");
                Console.WriteLine("Xbox Pass 应用已安装，可以打开");
            }
            else
            {
                Console.WriteLine("Xbox Pass 应用未安装");
            }

            Console.WriteLine("\n=== Xbox 服务演示完成 ===");
        }
    }
}
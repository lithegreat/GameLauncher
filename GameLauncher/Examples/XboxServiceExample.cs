using System;
using System.Threading.Tasks;
using GameLauncher.Services;

namespace GameLauncher.Examples
{
    /// <summary>
    /// Xbox ����ʹ��ʾ��
    /// </summary>
    public static class XboxServiceExample
    {
        /// <summary>
        /// ��ʾ���ʹ�� Xbox ����
        /// </summary>
        public static async Task RunExampleAsync()
        {
            Console.WriteLine("=== Xbox ��������ʾ ===");

            // 1. ��� Xbox Pass Ӧ���Ƿ�װ
            Console.WriteLine($"Xbox Pass App ��װ״̬: {XboxService.IsXboxPassAppInstalled}");

            // 2. ɨ�� Xbox ��Ϸ
            Console.WriteLine("\n��ʼɨ�� Xbox ��Ϸ...");
            var games = await XboxService.ScanXboxGamesAsync();
            
            Console.WriteLine($"\n�ҵ� {games.Count} �� Xbox ��Ϸ:");
            foreach (var game in games)
            {
                Console.WriteLine($"- {game.Name}");
                Console.WriteLine($"  ID: {game.AppId}");
                Console.WriteLine($"  ��װλ��: {game.InstallLocation}");
                Console.WriteLine($"  ��ִ���ļ�: {game.ExecutablePath}");
                Console.WriteLine($"  ������: {game.Publisher}");
                Console.WriteLine($"  �汾: {game.Version}");
                Console.WriteLine();
            }

            // 3. ��ʾ��Ϸ��������
            if (games.Count > 0)
            {
                var firstGame = games[0];
                Console.WriteLine($"��ʾ������Ϸ: {firstGame.Name}");
                
                // �����Ϸ�Ƿ���������
                var isRunning = XboxService.IsGameRunning(firstGame);
                Console.WriteLine($"��Ϸ����״̬: {(isRunning ? "������" : "δ����")}");

                // ��ȡ��������
                var launchArgs = XboxService.GetGameLaunchArguments(firstGame);
                Console.WriteLine($"��������: {launchArgs}");

                // ʵ��������Ϸ��ע�͵��Ա�������������
                // var launched = XboxService.LaunchXboxGame(firstGame.PackageFamilyName);
                // Console.WriteLine($"�������: {(launched ? "�ɹ�" : "ʧ��")}");
            }

            // 4. ��ʾ�� Xbox Pass Ӧ��
            Console.WriteLine("\n��ʾ�� Xbox Pass Ӧ��:");
            if (XboxService.IsXboxPassAppInstalled)
            {
                // ʵ�ʴ�Ӧ�ã�ע�͵��Ա�������򿪣�
                // var opened = XboxService.OpenXboxPassApp();
                // Console.WriteLine($"�򿪽��: {(opened ? "�ɹ�" : "ʧ��")}");
                Console.WriteLine("Xbox Pass Ӧ���Ѱ�װ�����Դ�");
            }
            else
            {
                Console.WriteLine("Xbox Pass Ӧ��δ��װ");
            }

            Console.WriteLine("\n=== Xbox ������ʾ��� ===");
        }
    }
}
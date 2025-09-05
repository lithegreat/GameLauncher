using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Diagnostics;
using GameLauncher.Models;

namespace GameLauncher.Services
{
    public static class Programs
    {
        private const string UwpAppsRegistryPath = @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        /// <summary>
        /// ��ȡ�����Ѱ�װ�� UWP Ӧ�ó���
        /// </summary>
        /// <returns>UWP Ӧ�ó����б�</returns>
        public static List<UwpApp> GetUWPApps()
        {
            var apps = new List<UwpApp>();

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UwpAppsRegistryPath);
                if (key == null)
                {
                    System.Diagnostics.Debug.WriteLine("�޷��� UWP Ӧ��ע����");
                    return apps;
                }

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var packageKey = key.OpenSubKey(subKeyName);
                        if (packageKey == null) continue;

                        var packageRootFolder = packageKey.GetValue("PackageRootFolder") as string;
                        var packageName = packageKey.GetValue("PackageName") as string;
                        
                        if (string.IsNullOrEmpty(packageRootFolder) || string.IsNullOrEmpty(packageName))
                            continue;

                        // �����Ƿ�Ϊ��Ϸ��Ӧ��
                        var manifestPath = Path.Combine(packageRootFolder, "AppxManifest.xml");
                        if (!File.Exists(manifestPath))
                            continue;

                        var appInfo = ParseAppxManifest(manifestPath);
                        if (appInfo != null)
                        {
                            appInfo.AppId = packageName;
                            appInfo.WorkDir = packageRootFolder;
                            apps.Add(appInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"���� UWP ��ʱ����: {subKeyName} - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ȡ UWP Ӧ��ʱ����: {ex.Message}");
            }

            return apps;
        }

        /// <summary>
        /// ���� AppxManifest.xml �ļ�
        /// </summary>
        private static UwpApp ParseAppxManifest(string manifestPath)
        {
            try
            {
                var content = File.ReadAllText(manifestPath);
                
                // �򵥵�XML��������ȡӦ����Ϣ
                var displayName = ExtractXmlValue(content, "DisplayName");
                var executable = ExtractXmlValue(content, "Executable");
                var isGame = content.Contains("Category=\"games\"", StringComparison.OrdinalIgnoreCase) ||
                           content.Contains("windows.game", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(displayName))
                    return null;

                var app = new UwpApp
                {
                    Name = displayName,
                    IsGame = isGame
                };

                if (!string.IsNullOrEmpty(executable))
                {
                    app.Path = Path.Combine(Path.GetDirectoryName(manifestPath), executable);
                }

                return app;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"�����嵥�ļ�ʱ����: {manifestPath} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ��XML��������ȡָ����ǩ��ֵ
        /// </summary>
        private static string ExtractXmlValue(string xmlContent, string tagName)
        {
            try
            {
                var patterns = new[]
                {
                    $@"<{tagName}>([^<]+)</{tagName}>",
                    $@"{tagName}\s*=\s*""([^""]+)""",
                    $@"{tagName}\s*=\s*'([^']+)'"
                };

                foreach (var pattern in patterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(xmlContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        return match.Groups[1].Value.Trim();
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
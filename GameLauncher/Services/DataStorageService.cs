using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace GameLauncher.Services
{
    /// <summary>
    /// �ṩ����/�Ǵ��ģʽ�����ݴ洢����
    /// </summary>
    public static class DataStorageService
    {
        private static bool? _isPackaged = null;
        private static string? _localDataPath = null;

        /// <summary>
        /// ���Ӧ���Ƿ��ڴ��ģʽ������
        /// </summary>
        public static bool IsPackaged
        {
            get
            {
                if (_isPackaged == null)
                {
                    try
                    {
                        // ���Է��� ApplicationData�����ʧ��˵����δ���ģʽ
                        var test = ApplicationData.Current.LocalFolder;
                        _isPackaged = true;
                        System.Diagnostics.Debug.WriteLine("��⵽���ģʽ");
                    }
                    catch (Exception ex)
                    {
                        _isPackaged = false;
                        System.Diagnostics.Debug.WriteLine($"��⵽δ���ģʽ: {ex.Message}");
                    }
                }
                return _isPackaged.Value;
            }
        }

        /// <summary>
        /// ��ȡ�������ݴ洢·��
        /// </summary>
        public static string LocalDataPath
        {
            get
            {
                if (_localDataPath == null)
                {
                    if (IsPackaged)
                    {
                        try
                        {
                            _localDataPath = ApplicationData.Current.LocalFolder.Path;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"��ȡ���ģʽ·��ʧ��: {ex.Message}");
                            // ������δ���ģʽ·��
                            _localDataPath = GetUnpackagedDataPath();
                        }
                    }
                    else
                    {
                        _localDataPath = GetUnpackagedDataPath();
                    }
                }
                return _localDataPath;
            }
        }

        private static string GetUnpackagedDataPath()
        {
            // Ϊδ���ģʽ��������Ŀ¼
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var gameDataPath = Path.Combine(appDataPath, "GameLauncher");
            
            try
            {
                if (!Directory.Exists(gameDataPath))
                {
                    Directory.CreateDirectory(gameDataPath);
                    System.Diagnostics.Debug.WriteLine($"����δ�������Ŀ¼: {gameDataPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��������Ŀ¼ʧ��: {ex.Message}");
                // ���˵�Ӧ�ó���Ŀ¼
                gameDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(gameDataPath))
                {
                    Directory.CreateDirectory(gameDataPath);
                }
            }
            
            return gameDataPath;
        }

        /// <summary>
        /// ��ȡ�ı��ļ�
        /// </summary>
        public static async Task<string> ReadTextFileAsync(string fileName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"��ȡ�ļ�: {fileName}");
                
                if (IsPackaged)
                {
                    try
                    {
                        var localFolder = ApplicationData.Current.LocalFolder;
                        var file = await localFolder.TryGetItemAsync(fileName) as StorageFile;
                        
                        if (file != null)
                        {
                            var content = await FileIO.ReadTextAsync(file);
                            System.Diagnostics.Debug.WriteLine($"���ģʽ��ȡ�ɹ�: {fileName}");
                            return content;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"���ģʽ��ȡʧ��: {ex.Message}");
                        // �������ļ�ϵͳ��ʽ
                    }
                }

                // δ���ģʽ����ģʽʧ��ʱʹ���ļ�ϵͳ
                var filePath = Path.Combine(LocalDataPath, fileName);
                if (File.Exists(filePath))
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    System.Diagnostics.Debug.WriteLine($"�ļ�ϵͳģʽ��ȡ�ɹ�: {filePath}");
                    return content;
                }
                
                System.Diagnostics.Debug.WriteLine($"�ļ�������: {fileName}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ȡ�ļ�ʧ��: {fileName}, ����: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// д���ı��ļ�
        /// </summary>
        public static async Task WriteTextFileAsync(string fileName, string content)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"д���ļ�: {fileName}");
                
                if (IsPackaged)
                {
                    try
                    {
                        var localFolder = ApplicationData.Current.LocalFolder;
                        var file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                        await FileIO.WriteTextAsync(file, content);
                        System.Diagnostics.Debug.WriteLine($"���ģʽд��ɹ�: {fileName}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"���ģʽд��ʧ��: {ex.Message}");
                        // �������ļ�ϵͳ��ʽ
                    }
                }

                // δ���ģʽ����ģʽʧ��ʱʹ���ļ�ϵͳ
                var filePath = Path.Combine(LocalDataPath, fileName);
                await File.WriteAllTextAsync(filePath, content);
                System.Diagnostics.Debug.WriteLine($"�ļ�ϵͳģʽд��ɹ�: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"д���ļ�ʧ��: {fileName}, ����: {ex.Message}");
                throw new InvalidOperationException($"��������ʧ��: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// ��ȡ����ֵ
        /// </summary>
        public static string ReadSetting(string key, string defaultValue = "")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"��ȡ����: {key}");
                
                if (IsPackaged)
                {
                    try
                    {
                        var localSettings = ApplicationData.Current.LocalSettings;
                        var value = localSettings.Values[key] as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            System.Diagnostics.Debug.WriteLine($"���ģʽ���ö�ȡ�ɹ�: {key} = {value}");
                            return value;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"���ģʽ���ö�ȡʧ��: {ex.Message}");
                        // �������ļ���ʽ
                    }
                }

                // δ���ģʽ����ģʽʧ��ʱʹ���ļ��洢
                var settingsPath = Path.Combine(LocalDataPath, "settings.ini");
                if (File.Exists(settingsPath))
                {
                    var lines = File.ReadAllLines(settingsPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith($"{key}="))
                        {
                            var value = line.Substring(key.Length + 1);
                            System.Diagnostics.Debug.WriteLine($"�ļ�ģʽ���ö�ȡ�ɹ�: {key} = {value}");
                            return value;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"���ò����ڣ�ʹ��Ĭ��ֵ: {key} = {defaultValue}");
                return defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"��ȡ����ʧ��: {key}, ����: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// д������ֵ
        /// </summary>
        public static void WriteSetting(string key, string value)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"д������: {key} = {value}");
                
                if (IsPackaged)
                {
                    try
                    {
                        var localSettings = ApplicationData.Current.LocalSettings;
                        localSettings.Values[key] = value;
                        System.Diagnostics.Debug.WriteLine($"���ģʽ����д��ɹ�: {key}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"���ģʽ����д��ʧ��: {ex.Message}");
                        // �������ļ���ʽ
                    }
                }

                // δ���ģʽ����ģʽʧ��ʱʹ���ļ��洢
                var settingsPath = Path.Combine(LocalDataPath, "settings.ini");
                var lines = new System.Collections.Generic.List<string>();
                
                // ��ȡ��������
                if (File.Exists(settingsPath))
                {
                    lines.AddRange(File.ReadAllLines(settingsPath));
                }
                
                // ���»��������
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith($"{key}="))
                    {
                        lines[i] = $"{key}={value}";
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    lines.Add($"{key}={value}");
                }
                
                File.WriteAllLines(settingsPath, lines);
                System.Diagnostics.Debug.WriteLine($"�ļ�ģʽ����д��ɹ�: {settingsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"д������ʧ��: {key}, ����: {ex.Message}");
                throw new InvalidOperationException($"��������ʧ��: {ex.Message}", ex);
            }
        }
    }
}
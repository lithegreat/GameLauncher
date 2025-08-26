using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace GameLauncher.Services
{
    /// <summary>
    /// 提供跨打包/非打包模式的数据存储服务
    /// </summary>
    public static class DataStorageService
    {
        private static bool? _isPackaged = null;
        private static string? _localDataPath = null;

        /// <summary>
        /// 检查应用是否在打包模式下运行
        /// </summary>
        public static bool IsPackaged
        {
            get
            {
                if (_isPackaged == null)
                {
                    try
                    {
                        // 尝试访问 ApplicationData，如果失败说明是未打包模式
                        var test = ApplicationData.Current.LocalFolder;
                        _isPackaged = true;
                        System.Diagnostics.Debug.WriteLine("检测到打包模式");
                    }
                    catch (Exception ex)
                    {
                        _isPackaged = false;
                        System.Diagnostics.Debug.WriteLine($"检测到未打包模式: {ex.Message}");
                    }
                }
                return _isPackaged.Value;
            }
        }

        /// <summary>
        /// 获取本地数据存储路径
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
                            System.Diagnostics.Debug.WriteLine($"获取打包模式路径失败: {ex.Message}");
                            // 降级到未打包模式路径
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
            // 为未打包模式创建数据目录
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var gameDataPath = Path.Combine(appDataPath, "GameLauncher");
            
            try
            {
                if (!Directory.Exists(gameDataPath))
                {
                    Directory.CreateDirectory(gameDataPath);
                    System.Diagnostics.Debug.WriteLine($"创建未打包数据目录: {gameDataPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建数据目录失败: {ex.Message}");
                // 回退到应用程序目录
                gameDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(gameDataPath))
                {
                    Directory.CreateDirectory(gameDataPath);
                }
            }
            
            return gameDataPath;
        }

        /// <summary>
        /// 读取文本文件
        /// </summary>
        public static async Task<string> ReadTextFileAsync(string fileName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"读取文件: {fileName}");
                
                if (IsPackaged)
                {
                    try
                    {
                        var localFolder = ApplicationData.Current.LocalFolder;
                        var file = await localFolder.TryGetItemAsync(fileName) as StorageFile;
                        
                        if (file != null)
                        {
                            var content = await FileIO.ReadTextAsync(file);
                            System.Diagnostics.Debug.WriteLine($"打包模式读取成功: {fileName}");
                            return content;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"打包模式读取失败: {ex.Message}");
                        // 降级到文件系统方式
                    }
                }

                // 未打包模式或打包模式失败时使用文件系统
                var filePath = Path.Combine(LocalDataPath, fileName);
                if (File.Exists(filePath))
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    System.Diagnostics.Debug.WriteLine($"文件系统模式读取成功: {filePath}");
                    return content;
                }
                
                System.Diagnostics.Debug.WriteLine($"文件不存在: {fileName}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取文件失败: {fileName}, 错误: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 写入文本文件
        /// </summary>
        public static async Task WriteTextFileAsync(string fileName, string content)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"写入文件: {fileName}");
                
                if (IsPackaged)
                {
                    try
                    {
                        var localFolder = ApplicationData.Current.LocalFolder;
                        var file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                        await FileIO.WriteTextAsync(file, content);
                        System.Diagnostics.Debug.WriteLine($"打包模式写入成功: {fileName}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"打包模式写入失败: {ex.Message}");
                        // 降级到文件系统方式
                    }
                }

                // 未打包模式或打包模式失败时使用文件系统
                var filePath = Path.Combine(LocalDataPath, fileName);
                await File.WriteAllTextAsync(filePath, content);
                System.Diagnostics.Debug.WriteLine($"文件系统模式写入成功: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入文件失败: {fileName}, 错误: {ex.Message}");
                throw new InvalidOperationException($"保存数据失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 读取设置值
        /// </summary>
        public static string ReadSetting(string key, string defaultValue = "")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"读取设置: {key}");
                
                if (IsPackaged)
                {
                    try
                    {
                        var localSettings = ApplicationData.Current.LocalSettings;
                        var value = localSettings.Values[key] as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            System.Diagnostics.Debug.WriteLine($"打包模式设置读取成功: {key} = {value}");
                            return value;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"打包模式设置读取失败: {ex.Message}");
                        // 降级到文件方式
                    }
                }

                // 未打包模式或打包模式失败时使用文件存储
                var settingsPath = Path.Combine(LocalDataPath, "settings.ini");
                if (File.Exists(settingsPath))
                {
                    var lines = File.ReadAllLines(settingsPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith($"{key}="))
                        {
                            var value = line.Substring(key.Length + 1);
                            System.Diagnostics.Debug.WriteLine($"文件模式设置读取成功: {key} = {value}");
                            return value;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"设置不存在，使用默认值: {key} = {defaultValue}");
                return defaultValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取设置失败: {key}, 错误: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 写入设置值
        /// </summary>
        public static void WriteSetting(string key, string value)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"写入设置: {key} = {value}");
                
                if (IsPackaged)
                {
                    try
                    {
                        var localSettings = ApplicationData.Current.LocalSettings;
                        localSettings.Values[key] = value;
                        System.Diagnostics.Debug.WriteLine($"打包模式设置写入成功: {key}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"打包模式设置写入失败: {ex.Message}");
                        // 降级到文件方式
                    }
                }

                // 未打包模式或打包模式失败时使用文件存储
                var settingsPath = Path.Combine(LocalDataPath, "settings.ini");
                var lines = new System.Collections.Generic.List<string>();
                
                // 读取现有设置
                if (File.Exists(settingsPath))
                {
                    lines.AddRange(File.ReadAllLines(settingsPath));
                }
                
                // 更新或添加设置
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
                System.Diagnostics.Debug.WriteLine($"文件模式设置写入成功: {settingsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入设置失败: {key}, 错误: {ex.Message}");
                throw new InvalidOperationException($"保存设置失败: {ex.Message}", ex);
            }
        }
    }
}
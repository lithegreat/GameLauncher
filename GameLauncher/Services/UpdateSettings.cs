using System;
using Windows.Storage;

namespace GameLauncher.Services
{
    public enum UpdateFrequency
    {
        OnStartup,  // 每次启动时检查
        Weekly      // 每周检查
    }

    public class UpdateSettings
    {
        private const string AutoUpdateEnabledKey = "AutoUpdateEnabled";
        private const string UpdateFrequencyKey = "UpdateFrequency";

        public bool AutoUpdateEnabled { get; set; } = true; // 默认启用自动更新
        public UpdateFrequency UpdateFrequency { get; set; } = UpdateFrequency.OnStartup; // 默认每次启动检查

        public static UpdateSettings GetSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                
                var autoUpdateEnabled = localSettings.Values[AutoUpdateEnabledKey] as bool? ?? true;
                var updateFrequencyValue = localSettings.Values[UpdateFrequencyKey] as string ?? "OnStartup";
                
                if (!Enum.TryParse<UpdateFrequency>(updateFrequencyValue, out var updateFrequency))
                {
                    updateFrequency = UpdateFrequency.OnStartup;
                }

                return new UpdateSettings
                {
                    AutoUpdateEnabled = autoUpdateEnabled,
                    UpdateFrequency = updateFrequency
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSettings.GetSettings error: {ex.Message}");
                return new UpdateSettings(); // 返回默认设置
            }
        }

        public void SaveSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[AutoUpdateEnabledKey] = AutoUpdateEnabled;
                localSettings.Values[UpdateFrequencyKey] = UpdateFrequency.ToString();
                
                System.Diagnostics.Debug.WriteLine($"UpdateSettings saved: AutoUpdate={AutoUpdateEnabled}, Frequency={UpdateFrequency}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSettings.SaveSettings error: {ex.Message}");
            }
        }

        public static void SaveSettings(bool autoUpdateEnabled, UpdateFrequency updateFrequency)
        {
            var settings = new UpdateSettings
            {
                AutoUpdateEnabled = autoUpdateEnabled,
                UpdateFrequency = updateFrequency
            };
            
            settings.SaveSettings();
        }
    }
}
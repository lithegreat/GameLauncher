using System;
using Windows.Storage;

namespace GameLauncher.Services
{
    public enum UpdateFrequency
    {
        OnStartup,  // ÿ������ʱ���
        Weekly      // ÿ�ܼ��
    }

    public class UpdateSettings
    {
        private const string AutoUpdateEnabledKey = "AutoUpdateEnabled";
        private const string UpdateFrequencyKey = "UpdateFrequency";

        public bool AutoUpdateEnabled { get; set; } = true; // Ĭ�������Զ�����
        public UpdateFrequency UpdateFrequency { get; set; } = UpdateFrequency.OnStartup; // Ĭ��ÿ���������

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
                return new UpdateSettings(); // ����Ĭ������
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
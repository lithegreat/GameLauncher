using Microsoft.UI.Xaml.Data;
using System;

namespace GameLauncher.Services
{
    /// <summary>
    /// 颜色字符串到颜色转换器
    /// </summary>
    public class ColorStringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string colorString && !string.IsNullOrEmpty(colorString))
            {
                try
                {
                    if (colorString.StartsWith("#") && colorString.Length == 7)
                    {
                        var r = System.Convert.ToByte(colorString.Substring(1, 2), 16);
                        var g = System.Convert.ToByte(colorString.Substring(3, 2), 16);
                        var b = System.Convert.ToByte(colorString.Substring(5, 2), 16);
                        return Windows.UI.Color.FromArgb(255, r, g, b);
                    }
                }
                catch
                {
                    // 如果解析失败，返回默认颜色
                }
            }
            
            // 默认返回蓝色
            return Windows.UI.Color.FromArgb(255, 33, 150, 243);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
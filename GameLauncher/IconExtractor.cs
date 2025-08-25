using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace GameLauncher
{
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string lpIconPath, out ushort lpiIcon);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static async System.Threading.Tasks.Task<BitmapImage?> ExtractIconAsync(string? filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return await CreateDefaultIconAsync();

                IntPtr hInst = GetModuleHandle(null);
                ushort uicon = 0;
                IntPtr hIcon = ExtractAssociatedIcon(hInst, filePath, out uicon);

                if (hIcon == IntPtr.Zero)
                    return await CreateDefaultIconAsync();

                using (Icon icon = Icon.FromHandle(hIcon))
                using (Bitmap bitmap = icon.ToBitmap())
                {
                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, ImageFormat.Png);
                        stream.Seek(0, SeekOrigin.Begin);

                        var bitmapImage = new BitmapImage();
                        var randomAccessStream = stream.AsRandomAccessStream();
                        await bitmapImage.SetSourceAsync(randomAccessStream);
                        
                        DestroyIcon(hIcon);
                        return bitmapImage;
                    }
                }
            }
            catch (Exception)
            {
                return await CreateDefaultIconAsync();
            }
        }

        private static async System.Threading.Tasks.Task<BitmapImage?> CreateDefaultIconAsync()
        {
            try
            {
                // 创建一个简单的默认图标
                using (var bitmap = new Bitmap(48, 48))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // 绘制一个简单的游戏控制器图标
                    graphics.Clear(Color.Transparent);
                    using (var brush = new SolidBrush(Color.Gray))
                    {
                        graphics.FillRectangle(brush, 8, 16, 32, 16);
                        graphics.FillEllipse(brush, 4, 20, 8, 8);
                        graphics.FillEllipse(brush, 36, 20, 8, 8);
                    }

                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, ImageFormat.Png);
                        stream.Seek(0, SeekOrigin.Begin);

                        var bitmapImage = new BitmapImage();
                        var randomAccessStream = stream.AsRandomAccessStream();
                        await bitmapImage.SetSourceAsync(randomAccessStream);
                        return bitmapImage;
                    }
                }
            }
            catch
            {
                // 如果连默认图标也创建失败，返回null
                return null;
            }
        }
    }
}
using System;

namespace GameLauncher.Models
{
    public class UwpApp
    {
        public string AppId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkDir { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsGame { get; set; }
    }
}
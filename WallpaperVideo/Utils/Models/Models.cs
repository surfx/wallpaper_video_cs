namespace WallpaperVideo.Utils.Models
{
    public class VideoItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string? Thumbnail { get; set; } = "";
    }

    public class AppConfig
    {
        public double PosX { get; set; } = 100;
        public double PosY { get; set; } = 100;
        public double Width { get; set; } = 800;
        public double Height { get; set; } = 450;
        public string FolderPath { get; set; } = "";
        public string MpvPath { get; set; } = @"D:\programas\executaveis\mpv";
        public bool IsRandom { get; set; }
        public string Minutes { get; set; } = "";
        public bool StartWithWindows { get; set; }
        public List<VideoItem> SavedVideos { get; set; } = new();
        public string? CurrentVideoPath { get; set; }
    }
}

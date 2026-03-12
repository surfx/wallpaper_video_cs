using System.IO;

namespace WallpaperVideo.Utils.Video
{
    public static class VideoExtensions
    {
        public static readonly string[] Supported = { 
            ".mp4", ".mkv", ".webm", ".mov", ".avi", 
            ".wmv", ".flv", ".ogv", ".m4v", ".mpg", 
            ".mpeg", ".m2ts", ".mts", ".ts", ".3gp" 
        };

        public static bool IsVideo(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return Supported.Contains(Path.GetExtension(path).ToLower());
        }
    }
}

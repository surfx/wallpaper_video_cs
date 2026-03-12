using System.Diagnostics;
using System.IO;

namespace WallpaperVideo.Utils.Media
{
    public class ThumbnailGenerator
    {
        private readonly string _mpvPath;
        private readonly bool _useGif;

        public ThumbnailGenerator(string mpvPath, bool useGif = true)
        {
            _mpvPath = mpvPath;
            _useGif = useGif;
        }

        public string? Generate(string videoPath)
        {
            if (!File.Exists(videoPath)) return null;

            var thumbFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "thumbs");
            if (!Directory.Exists(thumbFolder)) Directory.CreateDirectory(thumbFolder);

            var ext = _useGif ? ".gif" : ".jpg";
            var hashName = "thumb_" + Math.Abs(videoPath.GetHashCode()).ToString("X") + ext;
            var thumbPath = Path.Combine(thumbFolder, hashName);

            if (File.Exists(thumbPath)) return thumbPath;

            try
            {
                var mpvExe = Path.Combine(_mpvPath, "mpv.com");
                if (!File.Exists(mpvExe)) return null;

                if (_useGif)
                {
                    return GenerateGif(mpvExe, videoPath, thumbPath, thumbFolder);
                }
                else
                {
                    return GenerateJpg(mpvExe, videoPath, thumbPath, thumbFolder);
                }
            }
            catch
            {
                return null;
            }
        }

        private string? GenerateGif(string mpvExe, string videoPath, string thumbPath, string thumbFolder)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = mpvExe,
                Arguments = $"\"{videoPath}\" --start=5 --length=3 --no-config --no-audio --vf=scale=320:-1 --o=\"{thumbPath}\" --of=gif",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(mpvExe)
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(30000);

            System.Threading.Thread.Sleep(500);

            if (File.Exists(thumbPath) && new FileInfo(thumbPath).Length > 1000)
            {
                return thumbPath;
            }

            return null;
        }

        private string? GenerateJpg(string mpvExe, string videoPath, string thumbPath, string thumbFolder)
        {
            var existingFiles = Directory.GetFiles(thumbFolder, "*.jpg")
                .Select(f =>
                {
                    int num;
                    return int.TryParse(Path.GetFileNameWithoutExtension(f), out num) ? num : 0;
                })
                .ToList();

            int nextNum = existingFiles.Count > 0 ? existingFiles.Max() + 1 : 1;
            var tempFile = Path.Combine(thumbFolder, nextNum.ToString("D8") + ".jpg");

            var startInfo = new ProcessStartInfo
            {
                FileName = mpvExe,
                Arguments = $"\"{videoPath}\" --start=5 --frames=1 --vo=image --vo-image-format=jpg --vo-image-outdir=\"{thumbFolder}\" --no-config --no-audio --vf=scale=320:-1",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(10000);

            if (File.Exists(tempFile))
            {
                if (File.Exists(thumbPath)) File.Delete(thumbPath);
                File.Move(tempFile, thumbPath);
                return thumbPath;
            }

            return null;
        }

        public bool IsValid(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path)) return false;

            try
            {
                var img = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(path);
                img.EndInit();
                return img.PixelWidth > 0 && img.PixelHeight > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}

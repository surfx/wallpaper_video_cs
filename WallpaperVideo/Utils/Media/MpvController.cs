using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using WallpaperVideo.Utils.Logging;
using WallpaperVideo.Utils.Models;

namespace WallpaperVideo.Utils.Media
{
    public class MpvController
    {
        private Process? _process;
        private string _ipcAddress = "";
        private string? _mpvPath;

        public bool IsRunning => _process != null && !_process.HasExited;
        public string? CurrentVideoPath { get; private set; }

        public event EventHandler? VideoChanged;

        public void SetMpvPath(string? path)
        {
            _mpvPath = path;
        }

        public void StartWithPlaylist(List<VideoItem> videos, string? startVideoPath = null)
        {
            Stop();

            var exePath = GetMpvExe();
            if (exePath == null) return;

            var validVideos = videos
                .Where(v => !string.IsNullOrEmpty(v.FullPath) && File.Exists(v.FullPath))
                .ToList();

            if (validVideos.Count == 0) return;

            var playlistArg = string.Join(" ", validVideos.Select(v => $"\"{v.FullPath}\""));
            var startIndex = 0;

            if (!string.IsNullOrEmpty(startVideoPath))
            {
                var idx = validVideos.FindIndex(v => v.FullPath == startVideoPath);
                if (idx > 0) startIndex = idx;
            }

            _ipcAddress = $"wallpaper_video_{Guid.NewGuid():N}";

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--wid=0 --vo=gpu --hwdec=auto --loop-file=inf --no-audio --input-ipc-server=\\\\.\\pipe\\{_ipcAddress} --playlist-start={startIndex} {playlistArg}",
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                }
            };
            _process.Start();

            CurrentVideoPath = validVideos.ElementAtOrDefault(startIndex)?.FullPath;
            Logger.Log($"MpvController: started with {validVideos.Count} videos, ipc={_ipcAddress}");
        }

        public bool SwitchVideo(string videoPath)
        {
            if (string.IsNullOrEmpty(_ipcAddress) || !IsRunning)
                return false;

            try
            {
                using var client = new NamedPipeClientStream(".", _ipcAddress, PipeDirection.Out);
                client.Connect(2000);

                if (!client.IsConnected)
                {
                    Logger.Log("MpvController: pipe not connected");
                    return false;
                }

                using var writer = new StreamWriter(client) { AutoFlush = true };
                var cmd = System.Text.Json.JsonSerializer.Serialize(new { command = new[] { "loadfile", videoPath } });
                writer.WriteLine(cmd);

                CurrentVideoPath = videoPath;
                Logger.Log($"MpvController: switched to {Path.GetFileName(videoPath)}");
                VideoChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"MpvController: switch error - {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch { }
            finally
            {
                _process = null;
                _ipcAddress = "";
                CurrentVideoPath = null;
            }
        }

        public void StopAll()
        {
            Stop();

            System.Threading.Thread.Sleep(500);

            var names = new[] { "mpv", "mpv.exe", "mpv.com" };
            foreach (var name in names)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(3000);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private string? GetMpvExe()
        {
            var path = string.IsNullOrWhiteSpace(_mpvPath)
                ? @"D:\programas\executaveis\mpv"
                : _mpvPath;
            var exe = Path.Combine(path, "mpv.exe");
            return File.Exists(exe) ? exe : null;
        }
    }
}

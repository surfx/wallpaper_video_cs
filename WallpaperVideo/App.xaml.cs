using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Forms = System.Windows.Forms;

namespace WallpaperVideo
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon _trayIcon = null!;
        private MainWindow? _mainWindow;
        private Process? _mpvProcess;

        public void StopWallpaperPlayback()
        {
            try
            {
                if (_mpvProcess != null && !_mpvProcess.HasExited)
                {
                    _mpvProcess.Kill();
                }
            }
            catch { }
            finally
            {
                _mpvProcess = null;
            }
        }

        private void KillAllMpvProcesses()
        {
            StopWallpaperPlayback();

            var mpvProcesses = Process.GetProcessesByName("mpv");
            foreach (var p in mpvProcesses)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(2000);
                }
                catch { }
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Configurações", null, OnSettingsClick);
            contextMenu.Items.Add("Sair", null, OnExitClick);

            var iconStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("WallpaperVideo.wallpaper.ico");

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = iconStream != null
                    ? new System.Drawing.Icon(iconStream)
                    : System.Drawing.SystemIcons.Application,
                Text = "Wallpaper Video",
                Visible = true,
                ContextMenuStrip = contextMenu
            };

            _trayIcon.DoubleClick += (s, args) => ShowSettings();

            PlayStartupVideo();
        }

        private void PlayStartupVideo()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (!File.Exists(configPath)) return;

            try
            {
                var json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json);
                if (config?.SavedVideos == null || config.SavedVideos.Count == 0) return;

                var video = config.IsRandom && config.SavedVideos.Count > 1
                    ? config.SavedVideos[new Random().Next(config.SavedVideos.Count)]
                    : config.SavedVideos[0];

                var mpvPath = string.IsNullOrWhiteSpace(config.MpvPath)
                    ? @"D:\programas\executaveis\mpv"
                    : config.MpvPath;
                var mpvExe = Path.Combine(mpvPath, "mpv.exe");

                if (!File.Exists(mpvExe) || !File.Exists(video.FullPath)) return;

                _mpvProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = mpvExe,
                        Arguments = $"--wid=0 --vo=gpu --hwdec=auto --loop-file=inf --no-audio \"{video.FullPath}\"",
                        UseShellExecute = false
                    }
                };
                _mpvProcess.Start();

                _ = Task.Run(async () =>
                {
                    if (int.TryParse(config.Minutes, out var minutes) && minutes > 0)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(minutes));
                        System.Windows.Application.Current.Dispatcher.Invoke(() => NextRandomVideo(config));
                    }
                });
            }
            catch { }
        }

        private void NextRandomVideo(AppConfig config)
        {
            if (config.SavedVideos == null || config.SavedVideos.Count <= 1) return;

            StopWallpaperPlayback();

            var nextVideo = config.SavedVideos[new Random().Next(config.SavedVideos.Count)];
            var mpvPath = string.IsNullOrWhiteSpace(config.MpvPath)
                ? @"D:\programas\executaveis\mpv"
                : config.MpvPath;
            var mpvExe = Path.Combine(mpvPath, "mpv.exe");

            if (!File.Exists(mpvExe) || !File.Exists(nextVideo.FullPath)) return;

            _mpvProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = mpvExe,
                    Arguments = $"--wid=0 --vo=gpu --hwdec=auto --loop-file=inf --no-audio \"{nextVideo.FullPath}\"",
                    UseShellExecute = false
                }
            };
            _mpvProcess.Start();

            _ = Task.Run(async () =>
            {
                if (int.TryParse(config.Minutes, out var minutes) && minutes > 0)
                {
                    await Task.Delay(TimeSpan.FromMinutes(minutes));
                    System.Windows.Application.Current.Dispatcher.Invoke(() => NextRandomVideo(config));
                }
            });
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            ShowSettings();
        }

        private void ShowSettings()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.Closed += (s, e) => _mainWindow = null;
                _mainWindow.Show();
            }
            else if (_mainWindow.IsVisible)
            {
                _mainWindow.Activate();
            }
            else
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            _mainWindow?.ForceClose();
            KillAllMpvProcesses();
            Shutdown();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            KillAllMpvProcesses();
        }
    }
}

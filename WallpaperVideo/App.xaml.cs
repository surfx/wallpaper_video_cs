using System.IO;
using System.Reflection;
using System.Windows;
using Forms = System.Windows.Forms;
using WallpaperVideo.Utils;
using WallpaperVideo.Utils.Logging;
using WallpaperVideo.Utils.Media;
using WallpaperVideo.Utils.Models;

namespace WallpaperVideo
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon _trayIcon = null!;
        private MainWindow? _mainWindow;
        private readonly MpvController _mpvController;
        private readonly ConfigManager _configManager;
        private ToolStripMenuItem? _menuProximo;
        private CancellationTokenSource? _videoTimerCts;

        public App()
        {
            _configManager = new ConfigManager();
            _mpvController = new MpvController();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Logger.Clear();
            
            _configManager.Load();

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Configurações", null, OnSettingsClick);
            
            _menuProximo = new ToolStripMenuItem("Próximo", null, OnProximoClick);
            _menuProximo.Visible = _configManager.ShouldShowMenuProximo;
            contextMenu.Items.Add(_menuProximo);
            
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

            var config = _configManager.Config;
            if (config != null)
            {
                _mpvController.SetMpvPath(config.MpvPath);
            }

            PlayStartupVideo();
            
            if (!_configManager.HasVideos)
            {
                ShowSettings();
            }
        }

        private void PlayStartupVideo()
        {
            var config = _configManager.Config;
            if (config == null || !_configManager.HasVideos) return;

            var mpvPath = config.MpvPath;
            _mpvController.SetMpvPath(mpvPath);

            _mpvController.StartWithPlaylist(config.SavedVideos, config.CurrentVideoPath);
            _ = StartVideoTimer(config);
        }

        private VideoItem GetRandomVideoExcluding(List<VideoItem> videos, string? excludePath)
        {
            if (videos.Count <= 1 || string.IsNullOrEmpty(excludePath))
                return videos[new Random().Next(videos.Count)];

            var filtered = videos.Where(v => v.FullPath != excludePath).ToList();
            return filtered.Count == 0
                ? videos[new Random().Next(videos.Count)]
                : filtered[new Random().Next(filtered.Count)];
        }

        private async Task StartVideoTimer(AppConfig config)
        {
            _videoTimerCts?.Cancel();
            _videoTimerCts = new CancellationTokenSource();
            
            try
            {
                if (int.TryParse(config.Minutes, out var minutes) && minutes > 0)
                {
                    await Task.Delay(TimeSpan.FromMinutes(minutes), _videoTimerCts.Token);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => NextRandomVideo(config));
                }
            }
            catch (TaskCanceledException) { }
        }

        private void NextRandomVideo(AppConfig config)
        {
            if (config.SavedVideos == null || config.SavedVideos.Count <= 1) return;

            var nextVideo = GetRandomVideoExcluding(config.SavedVideos, config.CurrentVideoPath);
            
            if (!_mpvController.SwitchVideo(nextVideo.FullPath))
            {
                Logger.Log("NextRandomVideo: switch failed, restarting");
                _mpvController.StartWithPlaylist(config.SavedVideos, nextVideo.FullPath);
            }

            _configManager.UpdateCurrentVideo(nextVideo.FullPath);
            _ = StartVideoTimer(config);
            Logger.Log($"NextRandomVideo: {nextVideo.Name}");
        }

        public void PlayVideo(string videoPath)
        {
            var config = _configManager.Config;
            if (config == null) return;

            _mpvController.SetMpvPath(config.MpvPath);

            if (!_mpvController.SwitchVideo(videoPath))
            {
                Logger.Log("PlayVideo: switch failed, restarting");
                _mpvController.StartWithPlaylist(config.SavedVideos, videoPath);
            }

            _configManager.UpdateCurrentVideo(videoPath);
            _ = StartVideoTimer(config);
            Logger.Log($"PlayVideo: {Path.GetFileName(videoPath)}");
        }

        private void OnProximoClick(object? sender, EventArgs e)
        {
            _configManager.Load();
            if (_configManager.Config != null)
            {
                NextRandomVideo(_configManager.Config);
            }
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

            EnsureBackgroundVideoIfNeeded();
        }

        private void EnsureBackgroundVideoIfNeeded()
        {
            _configManager.Load();
            
            if (_configManager.Config == null) return;

            bool hasVideoPlaying = _mpvController.IsRunning;
            Logger.Log($"EnsureBackgroundVideoIfNeeded: hasVideoPlaying={hasVideoPlaying}");

            if (!hasVideoPlaying)
            {
                Logger.Log("EnsureBackgroundVideoIfNeeded: starting video");
                PlayStartupVideo();
            }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            _mainWindow?.ForceClose();
            _mpvController.StopAll();
            System.Threading.Thread.Sleep(1000);
            Shutdown();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            _mpvController.StopAll();
            System.Threading.Thread.Sleep(1000);
        }

        public void UpdateRandomMode(bool isRandom)
        {
            _configManager.UpdateRandomMode(isRandom);
            _menuProximo!.Visible = _configManager.ShouldShowMenuProximo;
        }

        public MpvController? GetMpvController() => _mpvController;
    }
}

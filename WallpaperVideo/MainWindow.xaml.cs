using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WallpaperVideo.Utils;
using WallpaperVideo.Utils.Media;
using WallpaperVideo.Utils.Models;
using WallpaperVideo.Utils.Video;
using WpfApp = System.Windows.Application;

namespace WallpaperVideo
{
    public partial class MainWindow : Window
    {
        private readonly ConfigManager _configManager = new();
        public ObservableCollection<VideoItem> VideoList { get; } = new();
        private string? _currentVideoPath;

        public MainWindow()
        {
            InitializeComponent();
            ItemsVideos.ItemsSource = VideoList;
            SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var hMenu = GetSystemMenu(handle, false);
            if (hMenu != IntPtr.Zero)
            {
                DeleteMenu(hMenu, 0xF030, 0x00000000);
            }
            SetWindowLong(handle, -16, GetWindowLong(handle, -16) & ~0x00010000);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DeleteMenu(IntPtr hMenu, int nPos, int wFlags);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            EnsureBackgroundVideo();
        }

        private void SaveConfig(object? sender, EventArgs? e)
        {
            if (!IsLoaded || txtPath == null || txtMpvPath == null || txtMinutes == null) return;

            var config = new AppConfig
            {
                PosX = Left,
                PosY = Top,
                Width = this.Width,
                Height = this.Height,
                FolderPath = txtPath.Text,
                MpvPath = txtMpvPath.Text,
                IsRandom = chkRandom.IsChecked ?? false,
                Minutes = txtMinutes.Text,
                StartWithWindows = chkStartWindows.IsChecked ?? false,
                SavedVideos = VideoList.ToList(),
                CurrentVideoPath = _currentVideoPath
            };

            try { File.WriteAllText(_configManager.ConfigPath, System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); }
            catch { }
        }

        private void LoadConfig()
        {
            const string defaultMpv = @"D:\programas\executaveis\mpv";
            _configManager.Load();
            var config = _configManager.Config;

            if (config == null)
            {
                txtMpvPath.Text = defaultMpv;
                return;
            }

            Left = config.PosX;
            Top = config.PosY;
            Width = config.Width;
            Height = config.Height;
            txtPath.Text = config.FolderPath;
            txtMpvPath.Text = string.IsNullOrWhiteSpace(config.MpvPath) ? defaultMpv : config.MpvPath;
            chkRandom.IsChecked = config.IsRandom;
            txtMinutes.Text = config.Minutes;
            chkStartWindows.IsChecked = config.StartWithWindows;
            _currentVideoPath = config.CurrentVideoPath;

            VideoList.Clear();
            if (config.SavedVideos != null)
            {
                foreach (var v in config.SavedVideos) VideoList.Add(v);
                ValidateThumbnailsAsync();
            }
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() != true) return;

            txtPath.Text = dialog.FolderName;
            LoadVideosFromFolder(dialog.FolderName);
            SaveConfig(null, null);
            EnsureBackgroundVideo();
        }

        private void BtnSelectMpv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Selecione a pasta do MPV" };
            if (dialog.ShowDialog() != true) return;

            txtMpvPath.Text = dialog.FolderName;
            SaveConfig(null, null);
        }

        private void LoadVideosFromFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            VideoList.Clear();
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => VideoExtensions.IsVideo(f));

            foreach (var file in files)
            {
                VideoList.Add(new VideoItem { Name = Path.GetFileName(file), FullPath = file });
            }

            GenerateThumbnailsAsync();
        }

        private void GenerateThumbnailsAsync()
        {
            Task.Run(() =>
            {
                string mpvPath = "";
                WpfApp.Current.Dispatcher.Invoke(() => mpvPath = txtMpvPath.Text);
                if (string.IsNullOrEmpty(mpvPath)) return;

                var generator = new ThumbnailGenerator(mpvPath, true);

                foreach (var v in VideoList.ToList())
                {
                    var thumb = generator.Generate(v.FullPath);
                    if (thumb == null) continue;

                    WpfApp.Current.Dispatcher.Invoke(() =>
                    {
                        var item = VideoList.FirstOrDefault(x => x.FullPath == v.FullPath);
                        if (item == null) return;
                        var idx = VideoList.IndexOf(item);
                        item.Thumbnail = thumb;
                        VideoList.RemoveAt(idx);
                        VideoList.Insert(idx, item);
                        SaveConfig(null, null);
                    });
                }

                ValidateThumbnailsAsync();
            });
        }

        private void ValidateThumbnailsAsync()
        {
            Task.Run(() =>
            {
                var generator = new ThumbnailGenerator(txtMpvPath?.Text ?? "", true);

                foreach (var v in VideoList.ToList())
                {
                    if (!generator.IsValid(v.Thumbnail))
                    {
                        var thumb = generator.Generate(v.FullPath);
                        if (thumb == null) continue;

                        WpfApp.Current.Dispatcher.Invoke(() =>
                        {
                            var item = VideoList.FirstOrDefault(x => x.FullPath == v.FullPath);
                            if (item == null) return;
                            var idx = VideoList.IndexOf(item);
                            item.Thumbnail = thumb;
                            VideoList.RemoveAt(idx);
                            VideoList.Insert(idx, item);
                            SaveConfig(null, null);
                        });
                    }
                }
            });
        }

        private void RemoveVideo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button) return;
            e.Handled = true;
            if (button.CommandParameter is not VideoItem video) return;

            var result = System.Windows.MessageBox.Show(
                $"Tem certeza que deseja remover o vídeo '{video.Name}' da lista?",
                "Confirmar Remoção",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            VideoList.Remove(video);
            SaveConfig(null, null);
        }

        private void PlayVideo(VideoItem video)
        {
            (WpfApp.Current as App)?.PlayVideo(video.FullPath);
            _currentVideoPath = video.FullPath;
            SaveConfig(null, null);
        }

        private void EnsureBackgroundVideo()
        {
            var candidates = VideoList.Select(v => v.FullPath).ToList();
            if (candidates.Count == 0 && Directory.Exists(txtPath?.Text))
            {
                LoadVideosFromFolder(txtPath.Text);
                candidates = VideoList.Select(v => v.FullPath).ToList();
            }

            string? pick = (!string.IsNullOrEmpty(_currentVideoPath) && File.Exists(_currentVideoPath) && candidates.Contains(_currentVideoPath))
                ? _currentVideoPath
                : (candidates.Count > 0 ? candidates[new Random().Next(candidates.Count)] : null);

            if (pick == null) return;

            var item = VideoList.FirstOrDefault(v => v.FullPath == pick);
            if (item != null) PlayVideo(item);
        }

        private void PlayVideo_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as Border)?.DataContext is VideoItem video) PlayVideo(video);
        }

        private void ChkStartWindows_Click(object sender, RoutedEventArgs e)
        {
            StartupManager.SetStartup(chkStartWindows.IsChecked ?? false);
            SaveConfig(null, null);
        }

        private void ChkRandom_Changed(object sender, RoutedEventArgs e)
        {
            (WpfApp.Current as App)?.UpdateRandomMode(chkRandom.IsChecked ?? false);
            SaveConfig(null, null);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            SaveConfig(null, null);
            Hide();
        }

        public void ForceClose()
        {
            (WpfApp.Current as App)?.GetMpvController()?.StopAll();
            SaveConfig(null, null);
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    }
}

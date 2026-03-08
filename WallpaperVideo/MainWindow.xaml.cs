using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using Wpf = System.Windows;

namespace WallpaperVideo
{
    public partial class MainWindow : Window
    {
        private static readonly bool USAR_THUMB_GIF = true;
        
        private string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        public ObservableCollection<VideoItem> VideoList { get; set; } = new ObservableCollection<VideoItem>();
        private System.Diagnostics.Process? mpvProcess = null;

        public MainWindow()
        {
            InitializeComponent();
            ItemsVideos.ItemsSource = VideoList;
            
            this.SourceInitialized += (s, e) =>
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var hMenu = GetSystemMenu(handle, false);
                if (hMenu != IntPtr.Zero)
                {
                    DeleteMenu(hMenu, 6, 0x00001000);
                }
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DeleteMenu(IntPtr hMenu, int nPos, int wFlags);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
        }

        private void SaveConfig(object? sender, EventArgs? e)
        {
            if (!this.IsLoaded || txtPath == null || txtMpvPath == null || txtMinutes == null) return;

            var config = new AppConfig
            {
                PosX = this.Left,
                PosY = this.Top,
                Width = this.Width,
                Height = this.Height,
                FolderPath = txtPath.Text,
                MpvPath = txtMpvPath.Text,
                IsRandom = chkRandom.IsChecked ?? false,
                Minutes = txtMinutes.Text,
                StartWithWindows = chkStartWindows.IsChecked ?? false,
                SavedVideos = VideoList.ToList()
            };

            try
            {
                File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadConfig()
        {
            const string defaultMpv = @"D:\programas\executaveis\mpv";
            
            if (File.Exists(configPath))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath));
                    if (config != null)
                    {
                        this.Left = config.PosX;
                        this.Top = config.PosY;
                        this.Width = config.Width;
                        this.Height = config.Height;

                        txtPath.Text = config.FolderPath;
                        txtMpvPath.Text = string.IsNullOrWhiteSpace(config.MpvPath) ? defaultMpv : config.MpvPath;
                        chkRandom.IsChecked = config.IsRandom;
                        txtMinutes.Text = config.Minutes;
                        chkStartWindows.IsChecked = config.StartWithWindows;

                        VideoList.Clear();
                        if (config.SavedVideos != null)
                        {
                            foreach (var v in config.SavedVideos) VideoList.Add(v);

                            Task.Run(() =>
                            {
                                bool mudouAlgo = false;
                                var tempList = config.SavedVideos.ToList();

                                foreach (var v in tempList)
                                {
                                    bool precisaGerar = string.IsNullOrEmpty(v.Thumbnail) || 
                                        !File.Exists(v.Thumbnail) ||
                                        (USAR_THUMB_GIF && v.Thumbnail.EndsWith(".jpg")) ||
                                        (!USAR_THUMB_GIF && v.Thumbnail.EndsWith(".gif"));
                                    
                                    if (precisaGerar)
                                    {
                                        var thumbGerada = GetThumbnailWithMpv(v.FullPath);
                                        if (thumbGerada != null)
                                        {
                                            mudouAlgo = true;
                                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                var itemNoGrid = VideoList.FirstOrDefault(x => x.FullPath == v.FullPath);
                                                if (itemNoGrid != null)
                                                {
                                                    int index = VideoList.IndexOf(itemNoGrid);
                                                    VideoList.RemoveAt(index);
                                                    itemNoGrid.Thumbnail = thumbGerada;
                                                    VideoList.Insert(index, itemNoGrid);
                                                }
                                            });
                                        }
                                    }
                                }

                                if (mudouAlgo)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => SaveConfig(null, null));
                                }
                            });
                        }
                        return;
                    }
                }
                catch { }
            }
            
            txtMpvPath.Text = defaultMpv;
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                txtPath.Text = dialog.FolderName;
                LoadVideosFromFolder(dialog.FolderName);
                SaveConfig(null, null);
            }
        }

        private void BtnSelectMpv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Selecione a pasta do MPV" };
            if (dialog.ShowDialog() == true)
            {
                txtMpvPath.Text = dialog.FolderName;
                SaveConfig(null, null);
            }
        }

        private void LoadVideosFromFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            VideoList.Clear();
            string[] extensions = { 
                ".mp4", ".mkv", ".webm", ".mov", ".avi", 
                ".wmv", ".flv", ".ogv", ".m4v", ".mpg", 
                ".mpeg", ".m2ts", ".mts", ".ts", ".3gp" 
            };
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                 .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()));

            foreach (var file in files)
            {
                VideoList.Add(new VideoItem
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    Thumbnail = null
                });
            }

            Task.Run(() =>
            {
                bool mudouAlgo = false;
                var tempList = VideoList.ToList();

                foreach (var v in tempList)
                {
                    if (string.IsNullOrEmpty(v.Thumbnail) || !File.Exists(v.Thumbnail))
                    {
                        var thumbGerada = GetThumbnailWithMpv(v.FullPath);
                        if (thumbGerada != null)
                        {
                            mudouAlgo = true;
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                var itemNoGrid = VideoList.FirstOrDefault(x => x.FullPath == v.FullPath);
                                if (itemNoGrid != null)
                                {
                                    int index = VideoList.IndexOf(itemNoGrid);
                                    VideoList.RemoveAt(index);
                                    itemNoGrid.Thumbnail = thumbGerada;
                                    VideoList.Insert(index, itemNoGrid);
                                }
                            });
                        }
                    }
                }

                if (mudouAlgo)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => SaveConfig(null, null));
                }
            });
        }

        private void RemoveVideo_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            e.Handled = true;
            var video = button?.CommandParameter as VideoItem;

            if (video != null)
            {
                MessageBoxResult result = System.Windows.MessageBox.Show(
                    $"Tem certeza que deseja remover o vídeo '{video.Name}' da lista?",
                    "Confirmar Remoção",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    VideoList.Remove(video);
                    SaveConfig(null, null);
                }
            }
        }

        private void KillAllMpvProcesses()
        {
            if (mpvProcess != null && !mpvProcess.HasExited)
            {
                try { mpvProcess.Kill(); } catch { }
                mpvProcess = null;
            }

            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("mpv"))
            {
                try { proc.Kill(); } catch { }
            }
        }

        private void PlayVideo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            var video = border?.DataContext as VideoItem;

            if (video != null)
            {
                KillAllMpvProcesses();

                string mpvFolder = txtMpvPath.Text;
                string mpvExe = Path.Combine(mpvFolder, "mpv.exe");

                if (!File.Exists(mpvExe))
                {
                    System.Windows.MessageBox.Show("mpv.exe não encontrado!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mpvExe,
                    Arguments = $"--wid=0 --vo=gpu --hwdec=auto --loop-file=inf --no-audio \"{video.FullPath}\"",
                    UseShellExecute = false
                };

                mpvProcess = System.Diagnostics.Process.Start(startInfo);
            }
        }

        private void ChkStartWindows_Click(object sender, RoutedEventArgs e)
        {
            string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath, true))
            {
                if (key != null)
                {
                    if (chkStartWindows.IsChecked == true)
                    {
                        var processPath = Environment.ProcessPath;
                        if (!string.IsNullOrWhiteSpace(processPath))
                        {
                            key.SetValue("WallpaperVideo", processPath);
                        }
                    }
                    else
                    {
                        key.DeleteValue("WallpaperVideo", false);
                    }
                }
            }
            SaveConfig(null, null);
        }


        private string? GetThumbnailWithMpv(string videoPath)
        {
            string mpvFolder = "";
            System.Windows.Application.Current.Dispatcher.Invoke(() => { mpvFolder = txtMpvPath.Text; });

            if (!Directory.Exists(mpvFolder))
            {
                return null;
            }

            string mpvExe = Path.Combine(mpvFolder, "mpv.com");
            if (!File.Exists(mpvExe))
            {
                return null;
            }

            string thumbFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "thumbs");
            if (!Directory.Exists(thumbFolder)) Directory.CreateDirectory(thumbFolder);

            string ext = USAR_THUMB_GIF ? ".gif" : ".jpg";
            string hashName = "thumb_" + Math.Abs(videoPath.GetHashCode()).ToString("X") + ext;
            string thumbPath = Path.Combine(thumbFolder, hashName);

            if (File.Exists(thumbPath)) return thumbPath;

            try
            {
                if (!USAR_THUMB_GIF)
                {
                    var existingFiles = Directory.GetFiles(thumbFolder, "*.jpg")
                        .Select(f =>
                        {
                            int num;
                            return int.TryParse(Path.GetFileNameWithoutExtension(f), out num) ? num : 0;
                        })
                        .ToList();
                    int nextNum = existingFiles.Count > 0 ? existingFiles.Max() + 1 : 1;
                    string tempFile = Path.Combine(thumbFolder, nextNum.ToString("D8") + ".jpg");

                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mpvExe,
                        Arguments = $"\"{videoPath}\" --start=5 --frames=1 --vo=image --vo-image-format=jpg --vo-image-outdir=\"{thumbFolder}\" --no-config --no-audio --vf=scale=320:-1",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };

                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        process?.WaitForExit(10000);
                    }

                    if (File.Exists(tempFile))
                    {
                        if (File.Exists(thumbPath)) File.Delete(thumbPath);
                        File.Move(tempFile, thumbPath);
                        return thumbPath;
                    }
                }
                else
                {
                    string mpvCom = Path.Combine(mpvFolder, "mpv.com");

                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mpvCom,
                        Arguments = $"\"{videoPath}\" --start=5 --length=3 --no-config --no-audio --vf=scale=320:-1 --o=\"{thumbPath}\" --of=gif",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                        WorkingDirectory = mpvFolder
                    };

                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        process?.WaitForExit(30000);
                    }

                    System.Threading.Thread.Sleep(500);

                    if (File.Exists(thumbPath) && new FileInfo(thumbPath).Length > 1000)
                    {
                        return thumbPath;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            SaveConfig(null, null);
            Hide();
        }

        public void ForceClose()
        {
            KillAllMpvProcesses();
            SaveConfig(null, null);
            Close();
        }
    }

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
        public List<VideoItem> SavedVideos { get; set; } = new List<VideoItem>();
    }
}

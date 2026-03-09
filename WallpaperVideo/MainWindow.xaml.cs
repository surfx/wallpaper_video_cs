using System;
using System.Windows.Input;
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
            
            // once the underlying window is created we can tweak its styles
            this.SourceInitialized += (s, e) =>
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

                // remove the maximize button from the system menu as well (optional)
                var hMenu = GetSystemMenu(handle, false);
                if (hMenu != IntPtr.Zero)
                {
                    const int SC_MAXIMIZE = 0xF030;
                    const int MF_BYCOMMAND = 0x00000000;
                    DeleteMenu(hMenu, SC_MAXIMIZE, MF_BYCOMMAND);
                }

                // clear the WS_MAXIMIZEBOX style so no maximize button is shown
                const int GWL_STYLE = -16;
                const int WS_MAXIMIZEBOX = 0x00010000;
                int style = GetWindowLong(handle, GWL_STYLE);
                SetWindowLong(handle, GWL_STYLE, style & ~WS_MAXIMIZEBOX);
            };
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
            // after configuration is restored, make sure a background video plays
            EnsureBackgroundVideo();
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
                SavedVideos = VideoList.ToList(),
                CurrentVideoPath = currentVideoPath
            };

            try
            {
                File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private string? currentVideoPath;

        private bool IsThumbnailValid(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path)) return false;
            try
            {
                // try loading the image to make sure it's not corrupted
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

        private void ValidateThumbnails()
        {
            // run background task that repairs only invalid thumbnails
            Task.Run(() =>
            {
                var tempList = VideoList.ToList();

                foreach (var v in tempList)
                {
                    if (!IsThumbnailValid(v.Thumbnail))
                    {
                        var thumbGerada = GetThumbnailWithMpv(v.FullPath);
                        if (thumbGerada != null)
                        {
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

                                SaveConfig(null, null);
                            });
                        }
                    }
                }
            });
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

                        currentVideoPath = config.CurrentVideoPath;

                        VideoList.Clear();
                        if (config.SavedVideos != null)
                        {
                            foreach (var v in config.SavedVideos) VideoList.Add(v);

                            // validate thumbs after UI is populated
                            ValidateThumbnails();
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

                // if nothing is playing yet, pick one now
                EnsureBackgroundVideo();
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
                var tempList = VideoList.ToList();

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

                                // persist update right away so the JSON never loses the thumbnail
                                SaveConfig(null, null);
                            });
                        }
                    }
                }

                // after filling the folder, also ensure validity of any others
                ValidateThumbnails();
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

        /// <summary>
        /// play a video item and record the choice
        /// </summary>
        private void PlayVideo(VideoItem video)
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

            // remember current video and persist
            currentVideoPath = video.FullPath;
            SaveConfig(null, null);
        }

        /// <summary>
        /// ensure there is a background video playing; choose random if necessary
        /// </summary>
        private void EnsureBackgroundVideo()
        {
            // if something is already running, leave it
            if (mpvProcess != null && !mpvProcess.HasExited) return;

            // compile candidate list
            var candidates = VideoList.Select(v => v.FullPath).ToList();
            if (candidates.Count == 0 && Directory.Exists(txtPath.Text))
            {
                // reload list synchronously if the folder exists and list is empty
                LoadVideosFromFolder(txtPath.Text);
                candidates = VideoList.Select(v => v.FullPath).ToList();
            }

            string? pick = null;
            if (!string.IsNullOrEmpty(currentVideoPath) && File.Exists(currentVideoPath))
            {
                // if the saved path still exists, use it
                pick = currentVideoPath;
            }

            // if the remembered video isn't part of the freshly loaded list, ignore it
            if (pick != null && !candidates.Contains(pick))
            {
                pick = null;
            }

            if (pick == null && candidates.Count > 0)
            {
                var rand = new Random();
                pick = candidates[rand.Next(candidates.Count)];
            }

            if (pick != null)
            {
                var item = VideoList.FirstOrDefault(v => v.FullPath == pick);
                if (item != null)
                {
                    PlayVideo(item);
                }
            }
        }

        private void PlayVideo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            var video = border?.DataContext as VideoItem;
            if (video != null)
            {
                PlayVideo(video);
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

        #region windows btns menu bar
        // Para permitir arrastar a janela
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // Botão Fechar
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Botão Minimizar
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        #endregion

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
        public string? CurrentVideoPath { get; set; }
    }
}

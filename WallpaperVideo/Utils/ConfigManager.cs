using System.IO;
using System.Text.Json;
using WallpaperVideo.Utils.Models;

namespace WallpaperVideo.Utils
{
    public class ConfigManager
    {
        private readonly string _configPath;
        private AppConfig? _config;

        public AppConfig? Config => _config;
        public string ConfigPath => _configPath;

        public ConfigManager()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        }

        public void Load()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<AppConfig>(json);
                }
                catch { }
            }
        }

        public void Save()
        {
            if (_config == null) return;

            try
            {
                File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public void UpdateCurrentVideo(string videoPath)
        {
            if (_config == null) return;
            _config.CurrentVideoPath = videoPath;
            Save();
        }

        public void UpdateRandomMode(bool isRandom)
        {
            if (_config == null) return;
            _config.IsRandom = isRandom;
            Save();
        }

        public bool HasVideos => _config?.SavedVideos != null && _config.SavedVideos.Count > 0;

        public bool ShouldShowMenuProximo => _config?.IsRandom == true && (_config.SavedVideos?.Count > 1);
    }
}

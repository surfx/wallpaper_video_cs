using System.IO;

namespace WallpaperVideo.Utils.Logging
{
    public class Logger
    {
        private static Logger? _instance;
        private static Logger Instance => _instance ??= new Logger();
        
        private string _logPath = "";
        
        public static void Clear()
        {
            try { File.Delete(Instance.GetLogPath()); } catch { }
        }

        public static void Log(string msg)
        {
            Instance.WriteLog(msg);
        }

        private string GetLogPath()
        {
            if (string.IsNullOrEmpty(_logPath))
                _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
            return _logPath;
        }

        private void WriteLog(string msg)
        {
            try
            {
                File.AppendAllText(GetLogPath(), $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }
    }
}

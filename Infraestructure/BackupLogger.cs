#nullable enable
using System;
using System.IO;

namespace SACTIBACKUP.Infrastructure
{
    public static class BackupLogger
    {
        private static readonly string _logPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "backup.log");

        private static readonly object _lock = new();

        public static void Log(string source, string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}{Environment.NewLine}";
                lock (_lock)
                {
                    File.AppendAllText(_logPath, line);
                }
            }
            catch { /* nunca fallar por logging */ }
        }

        public static void LogException(string source, string context, Exception ex)
        {
            Log(source, $"EXCEPCION en {context}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

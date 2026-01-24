#nullable enable
using System;

namespace SACTIBACKUP.Infrastructure
{
    /// <summary>
    /// Representa el progreso actual del respaldo
    /// </summary>
    public class BackupProgressEventArgs : EventArgs
    {
        public BackupStage Stage { get; init; }
        public string Message { get; init; } = "";
        public int PercentComplete { get; init; }
        public string CurrentItem { get; init; } = "";
        public int CurrentItemIndex { get; init; }
        public int TotalItems { get; init; }
    }

    /// <summary>
    /// Etapas del proceso de respaldo
    /// </summary>
    public enum BackupStage
    {
        Initializing,
        BackupSQL,
        BackupFirebird,
        Compressing,
        UploadingToFTP,
        ApplyingRotation,
        Completed,
        Error
    }

    /// <summary>
    /// Servicio estático para reportar progreso del respaldo
    /// </summary>
    public static class BackupProgressReporter
    {
        public static event EventHandler<BackupProgressEventArgs>? ProgressChanged;

        public static void Report(BackupStage stage, string message, int percent, string currentItem = "", int currentIndex = 0, int totalItems = 0)
        {
            ProgressChanged?.Invoke(null, new BackupProgressEventArgs
            {
                Stage = stage,
                Message = message,
                PercentComplete = Math.Clamp(percent, 0, 100),
                CurrentItem = currentItem,
                CurrentItemIndex = currentIndex,
                TotalItems = totalItems
            });
        }

        public static void ReportInitializing(string message)
        {
            Report(BackupStage.Initializing, message, 0);
        }

        public static void ReportSQL(string database, int currentDb, int totalDbs)
        {
            int percent = CalculatePercent(currentDb, totalDbs, 5, 40); // SQL usa 5-40%
            Report(BackupStage.BackupSQL, $"Respaldando SQL [{currentDb}/{totalDbs}]: {database}", percent, database, currentDb, totalDbs);
        }

        public static void ReportFirebird(string database, int currentDb, int totalDbs)
        {
            int percent = CalculatePercent(currentDb, totalDbs, 40, 60); // Firebird usa 40-60%
            Report(BackupStage.BackupFirebird, $"Respaldando Firebird [{currentDb}/{totalDbs}]: {database}", percent, database, currentDb, totalDbs);
        }

        public static void ReportCompressing(string file)
        {
            Report(BackupStage.Compressing, $"Comprimiendo: {file}", 65, file);
        }

        public static void ReportUploading(string file, int currentFile, int totalFiles, int filePercent = 0)
        {
            // FTP usa 70-95%
            int basePercent = CalculatePercent(currentFile - 1, totalFiles, 70, 95);
            int fileContribution = (95 - 70) / Math.Max(totalFiles, 1);
            int percent = basePercent + (fileContribution * filePercent / 100);
            Report(BackupStage.UploadingToFTP, $"Subiendo a nube [{currentFile}/{totalFiles}]: {file} ({filePercent}%)", Math.Min(percent, 95), file, currentFile, totalFiles);
        }

        public static void ReportRotation()
        {
            Report(BackupStage.ApplyingRotation, "Aplicando políticas de rotación...", 97);
        }

        public static void ReportCompleted()
        {
            Report(BackupStage.Completed, "Respaldo completado", 100);
        }

        public static void ReportError(string error)
        {
            Report(BackupStage.Error, $"Error: {error}", 0);
        }

        private static int CalculatePercent(int current, int total, int rangeStart, int rangeEnd)
        {
            if (total <= 0) return rangeStart;
            double progress = (double)current / total;
            return rangeStart + (int)((rangeEnd - rangeStart) * progress);
        }
    }
}

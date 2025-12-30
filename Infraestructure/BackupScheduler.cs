
// Infrastructure/BackupScheduler.cs
#nullable enable
using System;

namespace SACTIBACKUP.Infrastructure
{
    public sealed class BackupScheduler : IDisposable
    {
        private System.Threading.Timer? _timer;
        private readonly string _horaRespaldo;
        private readonly Func<System.Threading.Tasks.Task> _onTick;

        private BackupScheduler(string horaRespaldo, Func<System.Threading.Tasks.Task> onTick)
        {
            _horaRespaldo = horaRespaldo;
            _onTick = onTick;
        }

        public static BackupScheduler Start(string horaRespaldo, Func<System.Threading.Tasks.Task> onTick)
        {
            var s = new BackupScheduler(horaRespaldo, onTick);
            s.ScheduleNext();
            return s;
        }

        private void ScheduleNext()
        {
            var parts = _horaRespaldo.Split(':');
            var h = int.Parse(parts[0]);
            var m = int.Parse(parts[1]);
            var sec = int.Parse(parts.Length > 2 ? parts[2] : "0");

            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, h, m, sec);
            if (target <= now) target = target.AddDays(1);

            var due = target - now;
            _timer?.Dispose();
            _timer = new System.Threading.Timer(async _ =>
            {
                try { await _onTick().ConfigureAwait(false); }
                finally { ScheduleNext(); } 
            }, null, due, System.Threading.Timeout.InfiniteTimeSpan);
        }

        public void Dispose() => _timer?.Dispose();
    }
}

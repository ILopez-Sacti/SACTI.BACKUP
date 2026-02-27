using System.Diagnostics;

namespace SACTIBACKUP.Infrastructure;

public static class StartupTaskService
{
    private const string TaskName = "SACTI Backup";

    public static bool IsRegistered()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/query /tn \"{TaskName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static (bool ok, string error) Register()
    {
        try
        {
            var exePath = Application.ExecutablePath;
            var args = $"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl highest /f";

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = args,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(15000);

            if (proc?.ExitCode == 0)
                return (true, "");

            return (false, $"schtasks terminó con código {proc?.ExitCode}");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // El usuario canceló el UAC
            return (false, "Se canceló la elevación de permisos (UAC)");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static (bool ok, string error) Unregister()
    {
        try
        {
            if (!IsRegistered())
                return (true, "");

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/delete /tn \"{TaskName}\" /f",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(15000);

            if (proc?.ExitCode == 0)
                return (true, "");

            return (false, $"schtasks terminó con código {proc?.ExitCode}");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "Se canceló la elevación de permisos (UAC)");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

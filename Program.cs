using DevExpress.LookAndFeel;
using DevExpress.Skins;
using System.Runtime.InteropServices;

namespace SACTIBACKUP
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        [STAThread]
        static void Main()
        {
            using var mutex = new Mutex(true, "SACTIBACKUP_SINGLE_INSTANCE", out bool isNew);

            if (!isNew)
            {
                // Ya hay una instancia corriendo, traerla al frente
                var current = System.Diagnostics.Process.GetCurrentProcess();
                var processes = System.Diagnostics.Process.GetProcessesByName(current.ProcessName);
                foreach (var proc in processes)
                {
                    if (proc.Id != current.Id && proc.MainWindowHandle != IntPtr.Zero)
                    {
                        if (IsIconic(proc.MainWindowHandle))
                            ShowWindow(proc.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(proc.MainWindowHandle);
                        break;
                    }
                }
                return;
            }

            ApplicationConfiguration.Initialize();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            SkinManager.EnableFormSkins();
            UserLookAndFeel.Default.SetSkinStyle("Office 2019 Colorful");
            Application.Run(new VistaPrincipal());
        }
    }
}
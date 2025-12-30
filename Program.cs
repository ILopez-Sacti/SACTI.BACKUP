using DevExpress.LookAndFeel;
using DevExpress.Skins;

namespace SACTIBACKUP
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            SkinManager.EnableFormSkins();
            UserLookAndFeel.Default.SetSkinStyle("Office 2019 Colorful");
            Application.Run(new VistaPrincipal());
        }
    }
}
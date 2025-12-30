
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using SACTIBACKUP.Infrastructure;

namespace SACTIBACKUP
{
    public partial class VistaPrincipal : Form
    {
        private BackupScheduler? _scheduler;

        public VistaPrincipal()
        {
            InitializeComponent();
            AsignarEventos();
            CargarYProgramarScheduler();

            ValidarRespaldo();
        }

        private async void ValidarRespaldo()
        {
            var cfgObj = ConfigManager.Load();
            var cfg = BackupConfigMapper.FromJObject(cfgObj);
            if (cfg != null && (cfg.CorreoFTP != null && cfg.CorreoFTP != string.Empty))
            {
                LicenseResult lic = await LicenseService.ObtenerPorUsuarioAsync(cfg.CorreoFTP.ToUpperInvariant());
                if (lic != null)
                {
                    if (lic.LicenciaVencida)
                    {
                        return;
                    }
                    if(lic.FechaUltimoRespaldoNube?.AddDays(lic.RespaldarCada) < DateTime.Now)
                    {
                        await BackupRunner.EvaluateAndRunAsync().ConfigureAwait(false);
                    }
                }


            }
        }

        public void ConfigurarVista()
        {

        }


        public void AsignarEventos()
        {
            panelConfiguraciones.MouseEnter += PanelConfiguraciones_MouseEnter;
            panelConfiguraciones.MouseLeave += PanelConfiguraciones_MouseLeave;
            panelConfiguraciones.Click += PanelConfiguraciones_Click;
            notifyIcon1.DoubleClick += NotifyIcon1_DoubleClick;

        }

        private void NotifyIcon1_DoubleClick(object? sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        private async void PanelConfiguraciones_Click(object? sender, EventArgs e)
        {
            using var vistaConfig = new Configuraciones();
            vistaConfig.ShowDialog(this);

            if (vistaConfig.acepto || vistaConfig.RealizarRespaldo)
            {
                CargarYProgramarScheduler();
                await BackupRunner.EvaluateAndRunAsync().ConfigureAwait(false);

            }
        }

        private void PanelConfiguraciones_MouseLeave(object? sender, EventArgs e)
        {
            panelConfiguraciones.Cursor = Cursors.Default;
        }

        private void PanelConfiguraciones_MouseEnter(object? sender, EventArgs e)
        {
            panelConfiguraciones.Cursor = Cursors.Hand;
        }


        private void CargarYProgramarScheduler()
        {
            try
            {
                var obj = ConfigManager.Load();
                var hora = (string?)obj["HoraRespaldo"] ?? "00:00:00";


                if (string.IsNullOrWhiteSpace(hora)) hora = "00:00:00";


                _scheduler?.Dispose();
                _scheduler = BackupScheduler.Start(hora, async () =>
                {

                    await BackupRunner.EvaluateAndRunAsync().ConfigureAwait(false);
                });
            }
            catch (Exception ex)
            {

                MessageBox.Show($"No se pudo programar el respaldo automático: {ex.Message}",
                                "SACTI-BACKUP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }




        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            var obj = ConfigManager.Load();


            string pwd = (string?)obj["Password"] ?? "";
            if (!string.IsNullOrWhiteSpace(pwd))
            {
                PwdConfirm dlgPwd = new PwdConfirm(pwd);
                dlgPwd.ShowDialog();
                if (!dlgPwd.pwdCorrecta)
                {
                    e.Cancel = true;
                    return;
                }
                _scheduler?.Dispose();
                base.OnFormClosing(e);
            }
        }





        private void VistaPrincipal_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }
    }
}

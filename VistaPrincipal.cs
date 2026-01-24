
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SACTIBACKUP.Infrastructure;

namespace SACTIBACKUP
{
    public partial class VistaPrincipal : Form
    {
        private BackupScheduler? _scheduler;
        private bool _respaldoEnProgreso = false;

        public VistaPrincipal()
        {
            InitializeComponent();
            ConfigurarPanelRespaldo();
            SuscribirseAProgreso();
            AsignarEventos();
            CargarYProgramarScheduler();

            ValidarRespaldo();
        }

        private void ConfigurarPanelRespaldo()
        {
            panelRespaldoEnProgreso.Visible = false;
            progressBarRespaldo.Style = ProgressBarStyle.Continuous;
            progressBarRespaldo.Minimum = 0;
            progressBarRespaldo.Maximum = 100;
            progressBarRespaldo.Value = 0;
        }

        private void SuscribirseAProgreso()
        {
            BackupProgressReporter.ProgressChanged += OnBackupProgressChanged;
        }

        private void OnBackupProgressChanged(object? sender, BackupProgressEventArgs e)
        {
            ActualizarProgreso(e.Message, e.PercentComplete, e.Stage);
        }

        private void ActualizarProgreso(string mensaje, int porcentaje, BackupStage etapa)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ActualizarProgreso(mensaje, porcentaje, etapa)));
                return;
            }

            // Asegurar que el panel esté visible durante el proceso
            if (!panelRespaldoEnProgreso.Visible && etapa != BackupStage.Completed)
            {
                panelRespaldoEnProgreso.Visible = true;
                panelConfiguraciones.Enabled = false;
            }

            lblEstadoRespaldo.Text = mensaje;
            progressBarRespaldo.Value = Math.Clamp(porcentaje, 0, 100);

            // Cambiar color según la etapa
            switch (etapa)
            {
                case BackupStage.Initializing:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(52, 73, 94); // Gris oscuro
                    break;
                case BackupStage.BackupSQL:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(41, 128, 185); // Azul
                    break;
                case BackupStage.BackupFirebird:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(142, 68, 173); // Morado
                    break;
                case BackupStage.Compressing:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(243, 156, 18); // Naranja
                    break;
                case BackupStage.UploadingToFTP:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(39, 174, 96); // Verde
                    break;
                case BackupStage.ApplyingRotation:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(26, 188, 156); // Turquesa
                    break;
                case BackupStage.Completed:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(22, 160, 133); // Verde azulado
                    notifyIcon1.ShowBalloonTip(3000, "SACTI Backup", "Respaldo completado exitosamente", ToolTipIcon.Info);
                    break;
                case BackupStage.Error:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(192, 57, 43); // Rojo
                    break;
                default:
                    panelRespaldoEnProgreso.BackColor = Color.FromArgb(41, 128, 185); // Azul por defecto
                    break;
            }

            // Actualizar texto en el notify icon
            var textoIcono = $"SACTI Backup - {porcentaje}%";
            if (textoIcono.Length > 63) textoIcono = textoIcono.Substring(0, 63);
            notifyIcon1.Text = textoIcono;
        }

        private void MostrarRespaldoEnProgreso(bool mostrar)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => MostrarRespaldoEnProgreso(mostrar)));
                return;
            }

            _respaldoEnProgreso = mostrar;
            panelRespaldoEnProgreso.Visible = mostrar;
            panelConfiguraciones.Enabled = !mostrar;

            if (mostrar)
            {
                progressBarRespaldo.Value = 0;
                lblEstadoRespaldo.Text = "Iniciando respaldo...";
                panelRespaldoEnProgreso.BackColor = Color.FromArgb(41, 128, 185);
                notifyIcon1.Text = "SACTI Backup - Respaldando...";
                notifyIcon1.ShowBalloonTip(3000, "SACTI Backup", "Se está realizando el respaldo. Por favor no cierre la aplicación.", ToolTipIcon.Info);
            }
            else
            {
                notifyIcon1.Text = "SACTI Backup";
            }
        }

        private async void ValidarRespaldo()
        {
            var cfgObj = ConfigManager.Load();
            var cfg = BackupConfigMapper.FromJObject(cfgObj);

            if (cfg == null || string.IsNullOrWhiteSpace(cfg.CorreoFTP))
                return;

            try
            {
                LicenseResult? lic = await LicenseService.ObtenerPorUsuarioAsync(cfg.CorreoFTP.ToUpperInvariant());
                if (lic == null || lic.LicenciaVencida)
                    return;

                var hoy = DateTime.Today;

                // Verificar fecha de último respaldo local
                var ultimoRespaldoLocal = cfg.FechaUltimoRespaldo;
                var diasDesdeUltimoLocal = ultimoRespaldoLocal.HasValue
                    ? (hoy - ultimoRespaldoLocal.Value.Date).TotalDays
                    : double.MaxValue;

                // Verificar fecha de último respaldo en nube
                var ultimoRespaldoNube = lic.FechaUltimoRespaldoNube ?? cfg.FechaUltimoRespaldoNube;
                var diasDesdeUltimoNube = ultimoRespaldoNube.HasValue
                    ? (hoy - ultimoRespaldoNube.Value.Date).TotalDays
                    : double.MaxValue;

                // Determinar si ya se hizo respaldo hoy
                bool respaldoHoyLocal = ultimoRespaldoLocal.HasValue && ultimoRespaldoLocal.Value.Date == hoy;
                bool respaldoHoyNube = ultimoRespaldoNube.HasValue && ultimoRespaldoNube.Value.Date == hoy;

                // Si ya se hizo respaldo hoy, no hacer nada
                if (respaldoHoyLocal && (respaldoHoyNube || !cfg.RespaldarNube))
                    return;

                // Verificar si han pasado los días configurados para respaldar
                bool debeRespaldar = diasDesdeUltimoLocal >= lic.RespaldarCada || diasDesdeUltimoNube >= lic.RespaldarCada;

                if (debeRespaldar)
                {
                    await EjecutarRespaldoConIndicador();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ValidarRespaldo: {ex.Message}");
            }
        }

        public void ConfigurarVista()
        {

        }

        private async Task EjecutarRespaldoConIndicador()
        {
            if (_respaldoEnProgreso) return;

            try
            {
                MostrarRespaldoEnProgreso(true);
                await BackupRunner.EvaluateAndRunAsync().ConfigureAwait(false);

                // Esperar un momento para que el usuario vea el resultado
                await Task.Delay(2000).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en EjecutarRespaldoConIndicador: {ex}");
                BackupProgressReporter.ReportError($"Error: {ex.Message}");
                await Task.Delay(3000).ConfigureAwait(false); // Mostrar error más tiempo
            }
            finally
            {
                MostrarRespaldoEnProgreso(false);
            }
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
            if (_respaldoEnProgreso)
            {
                MessageBox.Show("No puede modificar la configuración mientras se realiza un respaldo.",
                    "SACTI-BACKUP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var vistaConfig = new Configuraciones();
            vistaConfig.ShowDialog(this);

            if (vistaConfig.acepto || vistaConfig.RealizarRespaldo)
            {
                CargarYProgramarScheduler();
                await EjecutarRespaldoConIndicador();
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
                    await EjecutarRespaldoConIndicador();
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
            // No permitir cerrar mientras se realiza un respaldo
            if (_respaldoEnProgreso)
            {
                MessageBox.Show("No puede cerrar la aplicación mientras se realiza un respaldo.\nPor favor espere a que termine.",
                    "SACTI-BACKUP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }

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
            }

            _scheduler?.Dispose();
            base.OnFormClosing(e);
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


using Newtonsoft.Json.Linq;

using SACTIBACKUP.Entidades;
using SACTIBACKUP.Infrastructure;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SACTIBACKUP;

public partial class Configuraciones : Form
{
    private Backup _model = new();
    private readonly BindingSource _bs = new();

    private BindingList<RutaFireBirdBD> _fbList = new();
    private BindingList<InstanciasSQL> _sqlList = new();

    public bool acepto { get; private set; } = false;

    public Configuraciones()
    {
        InitializeComponent();

        _model = BackupSettingsRepository.Load();

        _bs.DataSource = _model;

        

        CargarConfiguracion();

        chkRespaldarNube.Checked = true;
        chkUsarRazonSocial.Checked = true;
        chkComprimidas.Checked = true;
        chkIniciarConWindows.Checked = true;
        chkIniciarConWindows.Enabled = false; // siempre activo, no editable

        gcInstanciasSQL.DataSource = _sqlList;
        gcBDDFireBird.DataSource = _fbList;

        btnRutaRespaldos.Click += (_, __) => SeleccionarCarpeta();
        btnAceptar.Click += (_, __) => GuardarYAceptar();
        btnCancelar.Click += (_, __) => { acepto = false; Close(); };


        

        btnMostrarPwdSqL.Click += BtnMostrarPwdSqL_Click;
        btnMostrarPwdArchivos.Click += BtnMostrarPwdArchivos_Click;
        btnMostrarPwdArchivosConfirm.Click += BtnMostrarPwdArchivosConfirm_Click; ;

        btnAgregarRutaFirebird.Click += (_, __) => AgregarRutaFirebird();
        btnEliminarRutaFirebird.Click += (_, __) => EliminarRutaFirebird();

        btnAgregarInstanciaSQL.Click += BtnAgregarInstanciaSQL_Click;
        btnEliminarInstanciaSQL.Click += (_, __) => EliminarInstanciaSql();

        txtUsuario.Validated += TxtUsuario_Validated;

        txtRutaRespaldos.Enabled = false;


    }

    private void BtnMostrarPwdArchivosConfirm_Click(object? sender, EventArgs e)
    {
        txtPwdConfirm.Properties.PasswordChar = txtPwdConfirm.Properties.PasswordChar == '*' ? '\0' : '*';
    }

    private void BtnMostrarPwdArchivos_Click(object? sender, EventArgs e)
    {
        txtPwdArchivos.Properties.PasswordChar = txtPwdArchivos.Properties.PasswordChar == '*' ? '\0' : '*';
    }

    private void BtnMostrarPwdSqL_Click(object? sender, EventArgs e)
    {
        txtContrasenhaSQL.Properties.PasswordChar = txtContrasenhaSQL.Properties.PasswordChar == '*' ? '\0' : '*';
    }

    private void TxtUsuario_Validated(object? sender, EventArgs e)
    {
        txtAliasGlobal.Text = txtUsuario.Text;
    }

    private async void BtnAgregarInstanciaSQL_Click(object? sender, EventArgs e)
    {



        string servidor = txtServidorSQL.Text.Trim();
        string instancia = txtInstanciaSQL.Text.Trim();
        string usuario = txtUsuarioSQL.Text.Trim();
        string contrasenha = txtContrasenhaSQL.Text;

       
        if (string.IsNullOrWhiteSpace(servidor) ||
            string.IsNullOrWhiteSpace(usuario) ||
            string.IsNullOrWhiteSpace(contrasenha))
        {
            MessageBox.Show("Servidor, Usuario y Contraseña son obligatorios para la instancia SQL.");
            return;
        }

       
        btnAgregarInstanciaSQL.Enabled = false;
        MostrarConectando("Conectando a SQL...");
        try
        {

            var ok = await SqlValidationService.ValidarCredencialesSqlAsync(servidor, instancia, usuario, contrasenha);

            if (!ok)
            {
                MessageBox.Show("Las credenciales de esta instancia son incorrectas o no se pudo conectar.");
                return;
            }

           
            var instanciaSQLOBJ = new InstanciasSQL
            {
                Servidor = servidor,
                Instancia = string.IsNullOrWhiteSpace(instancia) ? "DEFAULT" : instancia,
                Usuario = usuario,
                Contrasenha = contrasenha
            };

            _sqlList.Add(instanciaSQLOBJ);   
            gvInstanciasSQL.RefreshData();

            
            txtServidorSQL.Text = "";
            txtInstanciaSQL.Text = "";
            txtUsuarioSQL.Text = "";
            txtContrasenhaSQL.Text = "";
            txtServidorSQL.Focus();
        }
        finally
        {
            OcultarConectando();
            btnAgregarInstanciaSQL.Enabled = true;
        }


    }


    private void MostrarConectando(string mensaje = "Conectando a SQL...")
    {
        statusLabel.Text = mensaje;
        statusProgress.Style = ProgressBarStyle.Marquee;
        statusProgress.Visible = true;
        statusStrip1.Visible = true;       
        UseWaitCursor = true;
        panelProgress.Visible = true;
    }

    private void OcultarConectando()
    {
        statusLabel.Text = string.Empty;
        statusProgress.Visible = false;
        UseWaitCursor = false;
        panelProgress.Visible = false;
    }


    private void SeleccionarCarpeta()
    {
        using var fbd = new FolderBrowserDialog();
        if (fbd.ShowDialog() == DialogResult.OK)
            txtRutaRespaldos.Text = fbd.SelectedPath;
    }

    private async void GuardarYAceptar()
    {
        RealizarRespaldo = false;

        string rutaRespaldos = txtRutaRespaldos.Text?.Trim() ?? "";
        string passwordArchivos = txtPwdArchivos.Text ?? "";
        string passwordArchivosConfirm = txtPwdConfirm.Text ?? "";
        string aliasGlobal = txtAliasGlobal.Text?.Trim() ?? "";
        string correoNotificaciones = txtCorreoNotificaciones.Text?.Trim() ?? "";

        // Obtener hora de respaldo desde el TimeEdit correctamente
        string horaRespaldo;
        try
        {
            // Usar la propiedad Time del TimeEdit de DevExpress
            var timeValue = timeHoraRespaldo.Time;
            horaRespaldo = timeValue.ToString("HH:mm:ss");
        }
        catch
        {
            try
            {
                // Fallback: intentar con EditValue
                if (timeHoraRespaldo.EditValue is DateTime dt)
                {
                    horaRespaldo = dt.ToString("HH:mm:ss");
                }
                else if (timeHoraRespaldo.EditValue is TimeSpan ts)
                {
                    horaRespaldo = ts.ToString(@"hh\:mm\:ss");
                }
                else
                {
                    // Último recurso: parsear el texto con cultura invariante
                    var textoHora = timeHoraRespaldo.Text?.Trim() ?? "00:00:00";
                    if (DateTime.TryParse(textoHora, System.Globalization.CultureInfo.CurrentCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime parsed))
                    {
                        horaRespaldo = parsed.ToString("HH:mm:ss");
                    }
                    else
                    {
                        horaRespaldo = "00:00:00";
                    }
                }
            }
            catch
            {
                horaRespaldo = "00:00:00";
            }
        }

        bool respaldarSQL = chkRespaldarSQL.Checked;
        bool respaldarFireBird = chkRespaldarFirebird.Checked;
        bool respaldarNube = chkRespaldarNube.Checked;
        bool bdComprimidas = chkComprimidas.Checked;
        bool usarRazonSocial = chkUsarRazonSocial.Checked;

        string correoFTP = txtUsuario.Text?.Trim().ToUpperInvariant() ?? "";
        string passwordFTP = txtContrasenha.Text ?? "";

        if (string.IsNullOrWhiteSpace(rutaRespaldos) ||
            string.IsNullOrWhiteSpace(passwordArchivos) ||
            string.IsNullOrWhiteSpace(passwordArchivosConfirm) ||
            string.IsNullOrWhiteSpace(aliasGlobal) ||
            string.IsNullOrWhiteSpace(correoNotificaciones))
        {
            MessageBox.Show("Los campos marcados con '*' son obligatorios");
            return;
        }

        if (!string.Equals(passwordArchivos, passwordArchivosConfirm, StringComparison.Ordinal))
        {
            MessageBox.Show("Las contraseñas no coinciden");
            return;
        }

        // Validar formato de hora (ya normalizado a HH:mm:ss)
        if (!Regex.IsMatch(horaRespaldo, @"^\d{2}:\d{2}:\d{2}$"))
        {
            MessageBox.Show("Hora de respaldo inválida. Use formato HH:MM:SS");
            return;
        }

        
        if (respaldarFireBird)
        {
            foreach (var item in _fbList)
            {
                if (string.IsNullOrWhiteSpace(item.RutaBaseDatos) ||
                    string.IsNullOrWhiteSpace(item.AliasBaseDatos))
                {
                    MessageBox.Show("Agregar un 'ALIAS' a las bases de datos FIREBIRD agregadas");
                    return;
                }
            }
        }

        
        if (respaldarSQL && (_sqlList == null || _sqlList.Count == 0))
        {
            MessageBox.Show("Debes agregar al menos una instancia SQL cuando 'Respaldar SQL' está habilitado.");
            return;
        }

        
        bool credencialesFtpOk = true;
        if (respaldarNube)
        {
            if (string.IsNullOrWhiteSpace(correoFTP) || string.IsNullOrWhiteSpace(passwordFTP))
            {
                MessageBox.Show("Usuario y contraseña son obligatorios cuando 'Respaldar Nube' está habilitado.");
                return;
            }

            MostrarConectando("Validando credenciales...");
            try
            {
                var hostFtp = "sacti.ddns.net"; 
                credencialesFtpOk = await FtpValidationService.ValidarCredencialesAsync(hostFtp, correoFTP, passwordFTP, useSsl: false);
                if (!credencialesFtpOk)
                {
                    MessageBox.Show("Las credenciales no son correctas o el servidor no responde.");
                    return;
                }
            }
            finally { OcultarConectando(); }
        }

        
        string nombreEmpresa = string.Empty;
        string borrarRespaldo = string.Empty;
        string diasRespaldo = string.Empty;
        string numeroRespaldos = string.Empty;
        string correoReceptorCliente = string.Empty;
        DateTime? fechaInicioLic = null;
        DateTime? fechaFinLic = null;
        bool licenciaVencida = false;

        if (!string.Equals(correoFTP, "GENERALSAC", StringComparison.OrdinalIgnoreCase))
        {
            MostrarConectando("Validando licencia...");
            try
            {
                var lic = await LicenseService.ObtenerPorUsuarioAsync(correoFTP);
                if (lic is null)
                {
                    MessageBox.Show("Error al validar licencia (sin datos).");
                    return;
                }

                nombreEmpresa = lic.NombreEmpresa;
                borrarRespaldo = lic.BorrarCada.ToString();
                diasRespaldo = lic.RespaldarCada.ToString();
                numeroRespaldos = lic.MantenerMinimo.ToString();
                correoReceptorCliente = lic.CorreoNotificacion;
                fechaInicioLic = lic.FechaInicio;
                fechaFinLic = lic.FechaFin;
                licenciaVencida = lic.LicenciaVencida;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión SQL (licencia): " + ex.Message);
                return;
            }
            finally { OcultarConectando(); }
        }
        else
        {
            licenciaVencida = false;
        }

        
        var listaRutasJSON = new List<string>();
        var listaInstanciasJSON = new List<string>();

        if (respaldarFireBird)
        {
            foreach (var item in _fbList)
                listaRutasJSON.Add($"{item.RutaBaseDatos}+{item.AliasBaseDatos}");
        }
        if (respaldarSQL)
        {
            foreach (var inst in _sqlList)
                listaInstanciasJSON.Add($"{inst.Servidor}+{inst.Instancia}+{inst.Usuario}+{inst.Contrasenha}");
        }

        
        try
        {
            MostrarConectando("Guardando configuración...");

            dynamic jsonObj;
            if (File.Exists("SactiBackup.json"))
            {
                string jsonBase64 = File.ReadAllText("SactiBackup.json");
                string jsonPlano = Encoding.UTF8.GetString(Convert.FromBase64String(jsonBase64));
                jsonObj = JObject.Parse(jsonPlano);
            }
            else
            {
                jsonObj = new JObject();
            }

            jsonObj.RutaRespaldo = rutaRespaldos;
            jsonObj.Password = passwordArchivos;
            jsonObj.PasswordConfirm = passwordArchivosConfirm;
            jsonObj.CorreoFTP = correoFTP;
            jsonObj.PasswordFTP = passwordFTP;
            jsonObj.HoraRespaldo = horaRespaldo;
            jsonObj.AliasGlobal = aliasGlobal;
            jsonObj.CorreoNotificaciones = correoNotificaciones;

            jsonObj.RespaldarSQL = respaldarSQL ? "SI" : "NO";
            jsonObj.RespaldarFireBird = respaldarFireBird ? "SI" : "NO";
            jsonObj.RespaldarNube = respaldarNube ? "SI" : "NO";
            jsonObj.BDComprimidas = bdComprimidas ? "SI" : "NO";
            jsonObj.UsarRazonSocial = usarRazonSocial ? "SI" : "NO";
            jsonObj.IniciarConWindows = chkIniciarConWindows.Checked ? "SI" : "NO";

            jsonObj.listaRutaFireBird = JToken.FromObject(listaRutasJSON);
            jsonObj.listaInstanciasSQL = JToken.FromObject(listaInstanciasJSON);

           
            jsonObj.NombreEmpresa = nombreEmpresa;
            jsonObj.BorrarRespaldo = borrarRespaldo;
            jsonObj.DiasRespaldo = diasRespaldo;
            jsonObj.NumeroRespaldos = numeroRespaldos;
            jsonObj.CorreoReceptorCliente = correoReceptorCliente;
            jsonObj.FechaInicioLicencia = fechaInicioLic;
            jsonObj.FechaFinLicencia = fechaFinLic;
            jsonObj.LicenciaVencida = licenciaVencida;

            ConfigManager.Save(jsonObj);

            // Iniciar con Windows siempre activo: garantizar que la tarea esté registrada.
            if (!StartupTaskService.IsRegistered())
            {
                var (ok, error) = StartupTaskService.Register();
                if (!ok)
                    MessageBox.Show($"No se pudo registrar el inicio con Windows: {error}", "Aviso",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            acepto = true;
           
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al guardar configuración: " + ex.Message);
            return;
        }
        finally
        {
            OcultarConectando();
        }
        RealizarRespaldo = true;
        //if (Mensaje.Pregunta("¿Desea realizar respaldo en este momento?"))
        //{
            
        //}


    }

    public bool RealizarRespaldo { get; set; }

    private void AgregarRutaFirebird()
    {

        try
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Selecciona base de datos Firebird",
                Filter = "Firebird DB (*.fdb;*.gdb)|*.fdb;*.gdb|Todos los archivos (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string ruta = ofd.FileName;

                string alias = PromptAlias("Alias para la base Firebird", "Ingresa un alias descriptivo:");
                if (string.IsNullOrWhiteSpace(alias))
                {
                    MessageBox.Show("Debes ingresar un alias para la base Firebird.");
                    return;
                }

                var nueva = new RutaFireBirdBD
                {
                    RutaBaseDatos = ruta,
                    AliasBaseDatos = alias.Trim()
                };

                _fbList.Add(nueva);
                gvBDFireBird.RefreshData();
            }
        }
        catch
        {
            MessageBox.Show("Error al Agregar Ruta");
        }

    }


    private string PromptAlias(string titulo, string mensaje)
    {
        using var form = new Form { Text = titulo, Width = 420, Height = 160, StartPosition = FormStartPosition.CenterParent };
        var lbl = new Label { Left = 12, Top = 12, Text = mensaje, AutoSize = true };
        var txt = new TextBox { Left = 12, Top = 40, Width = 380 };
        var ok = new Button { Text = "Aceptar", Left = 230, Width = 80, Top = 80, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", Left = 312, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog(this) == DialogResult.OK ? txt.Text : string.Empty;
    }


    private void EliminarRutaFirebird()
    {
        try
        {
            var alias = gvBDFireBird.FocusedValue?.ToString();
            if (string.IsNullOrEmpty(alias)) throw new InvalidOperationException();
            var item = _fbList.FirstOrDefault(x => x.AliasBaseDatos == alias);
            if (item is not null) _fbList.Remove(item);
        }
        catch
        {
            MessageBox.Show("Debe seleccionar una ruta a eliminar");
        }
    }


    private void EliminarInstanciaSql()
    {
        try
        {
            var instancia = gvInstanciasSQL.FocusedValue?.ToString();
            if (string.IsNullOrEmpty(instancia)) throw new InvalidOperationException();
            var item = _sqlList.FirstOrDefault(x => x.Instancia == instancia);
            if (item is not null) _sqlList.Remove(item);
        }
        catch
        {
            MessageBox.Show("Debe seleccionar una instancia a eliminar");
        }
    }


    private void CargarConfiguracion()
    {
        var obj = ConfigManager.Load();

        txtRutaRespaldos.Text = (string?)obj["RutaRespaldo"] ?? "";
        txtPwdArchivos.Text = (string?)obj["Password"] ?? "";
        txtPwdConfirm.Text = (string?)obj["PasswordConfirm"] ?? "";
        txtAliasGlobal.Text = (string?)obj["AliasGlobal"] ?? "";
        txtCorreoNotificaciones.Text = (string?)obj["CorreoNotificaciones"] ?? "";

        // Cargar hora de respaldo correctamente
        var horaStr = (string?)obj["HoraRespaldo"] ?? "00:00:00";
        try
        {
            if (TimeSpan.TryParse(horaStr, out TimeSpan ts))
            {
                timeHoraRespaldo.EditValue = DateTime.Today.Add(ts);
            }
            else
            {
                timeHoraRespaldo.EditValue = DateTime.Today;
            }
        }
        catch
        {
            timeHoraRespaldo.EditValue = DateTime.Today;
        }

        txtUsuario.Text = (string?)obj["CorreoFTP"] ?? "";
        txtContrasenha.Text = (string?)obj["PasswordFTP"] ?? "";

        chkRespaldarSQL.Checked = ((string?)obj["RespaldarSQL"]) == "SI";
        chkRespaldarFirebird.Checked = ((string?)obj["RespaldarFireBird"]) == "SI";
        chkRespaldarNube.Checked = ((string?)obj["RespaldarNube"]) == "SI";
        chkComprimidas.Checked = ((string?)obj["BDComprimidas"]) == "SI";
        chkUsarRazonSocial.Checked = ((string?)obj["UsarRazonSocial"]) == "SI";

        // Iniciar con Windows siempre activo (no se permite desactivar).
        chkIniciarConWindows.Checked = true;
        chkIniciarConWindows.Enabled = false;

        // Cargar listas en grids
        _fbList.Clear();
        foreach (var s in obj["listaRutaFireBird"] ?? new JArray())
        {
            var parts = ((string)s).Split('+');
            _fbList.Add(new RutaFireBirdBD { RutaBaseDatos = parts[0], AliasBaseDatos = parts[1] });
        }

        _sqlList.Clear();
        foreach (var s in obj["listaInstanciasSQL"] ?? new JArray())
        {
            var p = ((string)s).Split('+');
            _sqlList.Add(new InstanciasSQL { Servidor = p[0], Instancia = p[1], Usuario = p[2], Contrasenha = p[3] });
        }
    }

}

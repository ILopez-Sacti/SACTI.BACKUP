
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using SACTIBACKUP.Domain;
using ICSharpCode.SharpZipLib.Zip;
using FirebirdSql.Data.Services;
using FirebirdSql.Data.FirebirdClient;

namespace SACTIBACKUP.Infrastructure
{
    public static class BackupRunner
    {
        private static readonly List<string> _erroresSql = new();
        private static readonly List<string> _erroresFb = new();
        private static bool _errorFtp;

       
        public static async Task EvaluateAndRunAsync()
        {
            _erroresSql.Clear();
            _erroresFb.Clear();
            _errorFtp = false;

            var cfgObj = ConfigManager.Load();
            var cfg = BackupConfigMapper.FromJObject(cfgObj);

        
            var lic = await LicenseService.ObtenerPorUsuarioAsync(cfg.CorreoFTP.ToUpperInvariant());
            if (lic is null)
            {
                EmailService.SendBackupResult(
                    error: true,
                    mensaje: "No se pudo validar licencia (sin datos).",
                    nota: "Ponerse en contacto con su proveedor de servicio de respaldo.",
                    nombreEmpresa: cfg.CorreoFTP.Equals("GENERALSAC", StringComparison.OrdinalIgnoreCase) ? "General SACTI" : (cfg.AliasGlobal ?? "SACTI"),
                    correoEmisor: "backup@sacti.mx",
                    passwordElEmisor: "K0fxKheC{hV*",
                    correoReceptorCliente: cfg.CorreoNotificaciones ?? ""
                );
                return;
            }
            if (lic.LicenciaVencida)
            {
                EmailService.SendBackupResult(
                    error: true,
                    mensaje: "No se puede realizar el respaldo de su información debido a que su licencia ha caducado.",
                    nota: "En caso de renovación ponerse en contacto con su proveedor de servicio de respaldo.",
                    nombreEmpresa: cfg.CorreoFTP.Equals("GENERALSAC", StringComparison.OrdinalIgnoreCase) ? "General SACTI" : (cfg.AliasGlobal ?? "SACTI"),
                    correoEmisor: "backup@sacti.mx",
                    passwordElEmisor: "K0fxKheC{hV*",
                    correoReceptorCliente: lic.CorreoNotificacion
                );
                return;
            }

          
            var hoy = DateTime.Today;
            var ultimoLocal = cfg.FechaUltimoRespaldo;
            var diff = ultimoLocal.HasValue ? (hoy - ultimoLocal.Value.Date).TotalDays : double.MaxValue;
            if (diff < lic.RespaldarCada) return;

         
            await ExecuteBackupAsync(cfg).ConfigureAwait(false);

         
            if (cfg.RespaldarNube)
            {
                await FtpRotationService.ApplyPoliciesAsync(
                    host: "sacti.ddns.net",
                    usuario: cfg.CorreoFTP,
                    password: cfg.PasswordFTP,
                    mantenerMinimo: lic.MantenerMinimo,
                    borrarCadaDias: lic.BorrarCada
                ).ConfigureAwait(false);
            }

       
            var nowDate = DateTime.Now.Date;
            cfgObj["FechaUltimoRespaldo"] = nowDate;
            if (cfg.RespaldarNube) cfgObj["FechaUltimoRespaldoNube"] = nowDate;
            ConfigManager.Save(cfgObj);

            await LicenseUpdates.UpdateLastBackupDatesAsync(cfg.CorreoFTP.ToUpperInvariant(), nowDate, cfg.RespaldarNube ? nowDate : (DateTime?)null)
                .ConfigureAwait(false);

         
            var nombreEmpresa = cfg.CorreoFTP.Equals("GENERALSAC", StringComparison.OrdinalIgnoreCase)
                ? "General SACTI"
                : (lic?.Usuario ?? cfg.AliasGlobal ?? "SACTI");

            var mensajeCorreo = "Se realizó el respaldo de su información correctamente.";
            var notaCorreo = "";

            bool huboErrorSql = _erroresSql.Count > 0;
            bool huboErrorFirebird = _erroresFb.Count > 0;

            if (huboErrorSql && huboErrorFirebird)
            {
                mensajeCorreo = "Hubo un error al respaldar las bases de datos SQL y Firebird.";
                notaCorreo = "Es importante que le haga llegar este correo a su proveedor de servicio de respaldo.";
            }
            else if (huboErrorSql)
            {
                mensajeCorreo = "Se respaldaron correctamente sus bases de datos Firebird pero hubo un error al respaldar las bases de datos SQL.";
                notaCorreo = "Es importante que le haga llegar este correo a su proveedor de servicio de respaldo.";
            }
            else if (huboErrorFirebird)
            {
                mensajeCorreo = "Se respaldaron correctamente sus bases de datos SQL pero hubo un error al respaldar las bases de datos Firebird.";
                notaCorreo = "Es importante que le haga llegar este correo a su proveedor de servicio de respaldo.";
            }

            if (_erroresSql.Count > 0 || _erroresFb.Count > 0)
            {
                var lista = new List<string>();
                lista.AddRange(_erroresSql);
                lista.AddRange(_erroresFb);
                var texto = "Hubo un error al respaldar las siguientes bases de datos:<br/>";
                int tot = lista.Count;
                for (int i = 0; i < tot; i++)
                {
                    texto += (i == tot - 1) ? $"<br/>'{lista[i]}'." : $"<br/>'{lista[i]}'";
                }
                mensajeCorreo = texto;
                notaCorreo = "Es importante que le haga llegar este correo a su proveedor de servicio de respaldo.";
            }

            if (_errorFtp)
            {
                mensajeCorreo = "Se realizó el respaldo de su información correctamente. Pero hubo un error al subir sus respaldos a la nube";
                notaCorreo = "Es importante que le haga llegar este correo a su proveedor de servicio de respaldo.";
            }

            EmailService.SendBackupResult(
                error: (huboErrorSql || huboErrorFirebird || _errorFtp),
                mensaje: mensajeCorreo,
                nota: notaCorreo,
                nombreEmpresa: nombreEmpresa,
                correoEmisor: "backup@sacti.mx",
                passwordElEmisor: "K0fxKheC{hV*",
                correoReceptorCliente: lic?.CorreoNotificacion ?? cfg.CorreoNotificaciones ?? ""
            );
        }

     
        public static async Task ExecuteBackupAsync(BackupConfig cfg)
        {
            await Task.Run(async () =>
            {
                var today = DateTime.Now;
                var fechaCarpeta = $"{today.Year}-{today.Month:00}-{today.Day:00}";
                var pathBase = Path.Combine(cfg.RutaRespaldo, fechaCarpeta);
                Directory.CreateDirectory(pathBase);

             
                if (cfg.RespaldarSQL)
                {
                    foreach (var raw in cfg.ListaInstanciasSQL)
                    {
                        var p = raw.Split('+');
                        var servidor = p.ElementAtOrDefault(0) ?? "";
                        var instancia = p.ElementAtOrDefault(1) ?? "DEFAULT";
                        var usuario = p.ElementAtOrDefault(2) ?? "";
                        var contrasenha = p.ElementAtOrDefault(3) ?? "";

                        await BackupSqlInstanceAsync(servidor, instancia, usuario, contrasenha, pathBase, cfg).ConfigureAwait(false);
                    }
                }

            
                if (cfg.RespaldarFireBird)
                {
                    foreach (var fb in cfg.ListaRutaFireBird)
                    {
                        var parts = fb.Split('+');
                        var ruta = parts.ElementAtOrDefault(0) ?? "";
                        var alias = parts.ElementAtOrDefault(1) ?? "";

                        await BackupFirebirdAsync(ruta, alias, pathBase, cfg).ConfigureAwait(false);
                    }
                }

              
                if (cfg.RespaldarNube)
                {
                    var ok = await UploadRarsToFtpAsync(pathBase, cfg).ConfigureAwait(false);
                    if (!ok) _errorFtp = true;
                }

                var obj = ConfigManager.Load();
                var fLocal = new DateTime(today.Year, today.Month, today.Day);
                obj["FechaUltimoRespaldo"] = fLocal;
                if (cfg.RespaldarNube) obj["FechaUltimoRespaldoNube"] = fLocal;
                ConfigManager.Save(obj);

                await LicenseUpdates.UpdateLastBackupDatesAsync(cfg.CorreoFTP.ToUpperInvariant(), fLocal, cfg.RespaldarNube ? fLocal : (DateTime?)null)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

     
        private static async Task BackupSqlInstanceAsync(string servidor, string instancia, string usuario, string pwd, string pathBase, BackupConfig cfg)
        {
            var dataSource = (string.IsNullOrWhiteSpace(instancia) || instancia.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
                                ? servidor
                                : $"{servidor}\\{instancia}";

            var cs = new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                InitialCatalog = "master",
                UserID = usuario,
                Password = pwd,
                Encrypt = false,
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };

            await using var con = new SqlConnection(cs.ConnectionString);
            try { await con.OpenAsync().ConfigureAwait(false); }
            catch
            {
                _erroresSql.Add($"INSTANCIA:{instancia}");
                return;
            }

          
            var cmdEdition = con.CreateCommand();
            cmdEdition.CommandText = "SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(128))";
            var edition = (string?)await cmdEdition.ExecuteScalarAsync().ConfigureAwait(false) ?? "";
            bool express = edition.IndexOf("Express", StringComparison.OrdinalIgnoreCase) >= 0;

           
            var cmdDb = con.CreateCommand();
            cmdDb.CommandText = @"
                SELECT name 
                FROM sys.databases
                WHERE name NOT IN ('master','model','msdb','tempdb')
                  AND name NOT LIKE 'document%'
                  AND name NOT LIKE 'other%'
                  AND name NOT IN ('Predeterminada','ctPrueba_Irving_CONT','ctRUIZ_SOTO_DORA_NOM-ASIM')
                ORDER BY name";
            var dbs = new List<string>();
            await using (var rd = await cmdDb.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await rd.ReadAsync().ConfigureAwait(false))
                    dbs.Add(rd.GetString(0));
            }

            foreach (var db in dbs)
            {
                try
                {
               
                    var bakPath = Path.Combine(pathBase, $"{db}.bak");
                    var cmdBak = con.CreateCommand();
                    cmdBak.CommandTimeout = 0;

                    var comp = (cfg.RespaldarNube && !express) ? ", COMPRESSION" : "";
                    cmdBak.CommandText = $"BACKUP DATABASE [{db}] TO DISK=@p WITH FORMAT, INIT{comp}, SKIP, NOREWIND, NOUNLOAD, MEDIADESCRIPTION=@d";
                    cmdBak.Parameters.AddWithValue("@p", bakPath);
                    cmdBak.Parameters.AddWithValue("@d", $"Backup de la base de datos: {db} {DateTime.Now:yyyyMMdd}");
                   


                    await cmdBak.ExecuteNonQueryAsync().ConfigureAwait(false);

                  
                    var tablas = await GetTablesAsync(con, db).ConfigureAwait(false);

                    if (tablas.Contains("Parametros"))
                    {
                    
                        var pData = await GetParametrosContpaqAsync(con, db).ConfigureAwait(false);
                        if (pData.HasGuidDsl)
                        {
                            var special = await BackupSpecialDatabasesAsync(con, pathBase, instancia, cfg, pData.GuidDsl)
                                .ConfigureAwait(false);

                        
                            special.InnerZipPath = Path.Combine(pathBase, $"{db}.zip");

                            await GenerateControlDocumentJsonContpaqAsync(pathBase, pData, special)
                                .ConfigureAwait(false);

                            var rarName = $"{cfg.AliasGlobal}-{instancia}{db}{DateTime.Now:yyyyMMdd}.rar";
                            await CompressSingleAsync(pathBase, rarName,
                                new[] { bakPath, special.InnerZipPath },
                                cfg.PasswordArchivos).ConfigureAwait(false);

                            SafeDelete(bakPath);
                            SafeDelete(special.InnerZipPath);
                        }
                        else
                        {
                            await CompressSingleAsync(pathBase, $"{cfg.AliasGlobal}-{instancia}{db}{DateTime.Now:yyyyMMdd}.rar",
                                                      new[] { bakPath }, cfg.PasswordArchivos).ConfigureAwait(false);
                            SafeDelete(bakPath);
                        }
                    }
                    else if (tablas.Contains("NOM10000"))
                    {
                     
                        var nData = await GetParametrosNominaAsync(con, db).ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(nData.GuidDsl))
                        {
                            var special = await BackupSpecialDatabasesAsync(con, pathBase, instancia, cfg, nData.GuidDsl)
                                .ConfigureAwait(false);

                           
                            special.InnerZipPath = Path.Combine(pathBase, $"{db}.zip");

                            await GenerateControlDocumentJsonNominaAsync(pathBase, nData, special)
                                .ConfigureAwait(false);

                            var rarName = $"{cfg.AliasGlobal}-{instancia}{db}{DateTime.Now:yyyyMMdd}.rar";
                            await CompressSingleAsync(pathBase, rarName,
                                new[] { bakPath, special.InnerZipPath },
                                cfg.PasswordArchivos).ConfigureAwait(false);

                            SafeDelete(bakPath);
                            SafeDelete(special.InnerZipPath);
                        }
                        else
                        {
                            await CompressSingleAsync(pathBase, $"{cfg.AliasGlobal}-{instancia}{db}{DateTime.Now:yyyyMMdd}.rar",
                                                      new[] { bakPath }, cfg.PasswordArchivos).ConfigureAwait(false);
                            SafeDelete(bakPath);
                        }
                    }
                    else if (tablas.Contains("admParametros"))
                    {
                  
                        var cData = await GetParametrosComercialAsync(con, db).ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(cData.CGuidDSL))
                        {
                            var special = await BackupSpecialDatabasesAsync(con, pathBase, instancia, cfg, cData.CGuidDSL)
                                .ConfigureAwait(false);

                            special.InnerZipPath = Path.Combine(pathBase, $"{db}.zip");

                            await GenerateControlDocumentJsonComercialAsync(pathBase, cData, special)
                                .ConfigureAwait(false);

                            var rarName = $"{cfg.AliasGlobal}-{instancia}{db}{DateTime.Now:yyyyMMdd}.rar";
                            await CompressSingleAsync(pathBase, rarName,
                                new[] { bakPath, special.InnerZipPath },
                                cfg.PasswordArchivos).ConfigureAwait(false);

                            SafeDelete(bakPath);
                            SafeDelete(special.InnerZipPath);
                        }
                        else
                        {
                            await CompressSingleAsync(pathBase, $"{cfg.AliasGlobal}-{instancia}{db}{DateTime.Now:yyyyMMdd}.rar",
                                                      new[] { bakPath }, cfg.PasswordArchivos).ConfigureAwait(false);
                            SafeDelete(bakPath);
                        }
                    }
                    else
                    {
                       
                        await CompressSingleAsync(pathBase, $"{cfg.AliasGlobal}-{instancia}{db}{DateTime.Now:yyyyMMdd}.rar",
                                                  new[] { bakPath }, cfg.PasswordArchivos).ConfigureAwait(false);
                        SafeDelete(bakPath);
                    }
                }
                catch (Exception ex)
                {
                    _erroresSql.Add(string.Concat(db, ": ", ex.Message.ToString()));
                }
            }

   
        }

        private static async Task<HashSet<string>> GetTablesAsync(SqlConnection con, string db)
        {
            var tablas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cmd = con.CreateCommand();
            cmd.CommandText = $"USE [{db}]; SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES";
            await using var rd = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await rd.ReadAsync().ConfigureAwait(false))
                tablas.Add(rd.GetString(0));
            return tablas;
        }

   
        private static async Task BackupFirebirdAsync(string rutaDb, string alias, string pathBase, BackupConfig cfg)
        {
            try
            {
                var fbCs = new FbConnectionStringBuilder
                {
                    Database = rutaDb,
                    DataSource = "127.0.0.1",
                    ServerType = FbServerType.Default,
                    UserID = "SYSDBA",
                    Password = "masterkey"
                }.ToString();

                var fbBackup = new FbBackup
                {
                    ConnectionString = fbCs,
                    Verbose = true,
                    Options = FbBackupFlags.IgnoreLimbo
                };
                var fbkPath = Path.Combine(pathBase, $"{alias}.fbk");
                fbBackup.BackupFiles.Add(new FbBackupFile(fbkPath));
                fbBackup.Execute();

                await CompressSingleAsync(pathBase, $"{cfg.AliasGlobal}-{alias}{DateTime.Now:yyyyMMdd}.rar",
                                          new[] { fbkPath }, cfg.PasswordArchivos).ConfigureAwait(false);
                SafeDelete(fbkPath);
            }
            catch
            {
                _erroresFb.Add(alias);
            }
        }

    
        private static async Task CompressSingleAsync(string targetDir, string rarFileName, IEnumerable<string> files, string? password)
        {
            try
            {
                await Task.Run(() =>
                {
                    // Nombres en UTF-8
                    ZipStrings.UseUnicode = true;

                    var rarPath = Path.Combine(targetDir, rarFileName);
                    using var fsOut = File.Create(rarPath);
                    using var zipStream = new ZipOutputStream(fsOut);

                    if (!string.IsNullOrEmpty(password))
                        zipStream.Password = password;

                    zipStream.SetLevel(9);
                    var buffer = new byte[4096];

                    foreach (var file in files)
                    {
                        if (!File.Exists(file)) continue;
                        var entry = new ZipEntry(Path.GetFileName(file)) { DateTime = DateTime.Now };
                        zipStream.PutNextEntry(entry);

                        using var fs = File.OpenRead(file);
                        int read;
                        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                            zipStream.Write(buffer, 0, read);
                    }

                    zipStream.Finish();
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _erroresSql.Add(string.Concat(rarFileName, ": ", ex.Message.ToString()));
            }
            
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }


        private static async Task<bool> UploadRarsToFtpAsync(string pathBase, BackupConfig cfg)
        {
            try
            {
                var rarFiles = Directory.EnumerateFiles(pathBase, "*.rar", SearchOption.TopDirectoryOnly).ToList();
                foreach (var archivo in rarFiles)
                {
                    var nombre = Path.GetFileName(archivo);
                    var folder = $"ftp://sacti.ddns.net//{cfg.CorreoFTP.ToUpperInvariant()}";
                    var fileUri = $"{folder}//{nombre}";

                    var request = (FtpWebRequest)WebRequest.Create(fileUri);
                    request.Method = WebRequestMethods.Ftp.UploadFile;
                    request.Credentials = new NetworkCredential(cfg.CorreoFTP, cfg.PasswordFTP);
                    request.UsePassive = true;
                    request.UseBinary = true;
                    request.KeepAlive = false;
                    request.Timeout = 10 * 60 * 1000;        // 10 minutos
                    request.ReadWriteTimeout = 10 * 60 * 1000;
                    //request.ReadWriteTimeout = 200000;

                    using var fileStream = File.OpenRead(archivo);
                    
                    using (var reqStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                    {
                        await fileStream.CopyToAsync(reqStream).ConfigureAwait(false);
                        reqStream.Close();
                    }
                    using var resp = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                    resp.Close();
                }

          
                foreach (var archivo in rarFiles)
                {
                    try
                    {
                        var nombre = Path.GetFileName(archivo);
                        var url = $"ftp://sacti.ddns.net//{cfg.CorreoFTP.ToUpperInvariant()}//{nombre}";

                        var reqDate = (FtpWebRequest)WebRequest.Create(url);
                        reqDate.Credentials = new NetworkCredential(cfg.CorreoFTP, cfg.PasswordFTP);
                        reqDate.Method = WebRequestMethods.Ftp.GetDateTimestamp;

                        using var resp = (FtpWebResponse)await reqDate.GetResponseAsync().ConfigureAwait(false);
                        if (resp.LastModified != default) SafeDelete(archivo);
                    }
                    catch { /* conserva local si no se puede verificar */ }
                }

                // Si no quedan .rar, borrar carpeta del día
                var quedanRars = Directory.EnumerateFiles(pathBase, "*.rar").Any();
                if (!quedanRars)
                {
                    foreach (var f in Directory.EnumerateFiles(pathBase, "*.*"))
                        SafeDelete(f);
                    try { Directory.Delete(pathBase); } catch { /* ignore */ }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static class LicenseUpdates
        {
            public static async Task UpdateLastBackupDatesAsync(string usuarioFtp, DateTime fechaLocal, DateTime? fechaNube)
            {
                try
                {
                    var cs = new SqlConnectionStringBuilder
                    {
                        DataSource = @"sacti.ddns.net\sacti,2014",
                        InitialCatalog = "SACTIBACKUP",
                        UserID = "sa",
                        Password = "MasterkeySac123",
                        Encrypt = false,
                        TrustServerCertificate = true,
                        ConnectTimeout = 5
                    };

                    await using var con = new SqlConnection(cs.ConnectionString);
                    await con.OpenAsync().ConfigureAwait(false);

                    var cmd = con.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE CONFIGURACIONES
                           SET FECHAULTIMORESPALDO = @FLocal,
                               FECHAULTIMORESPALDONUBE = COALESCE(@FNube, FECHAULTIMORESPALDONUBE)
                         WHERE USUARIO = @USUARIO";
                    cmd.Parameters.AddWithValue("@USUARIO", usuarioFtp);
                    cmd.Parameters.AddWithValue("@FLocal", fechaLocal);
                    cmd.Parameters.AddWithValue("@FNube", (object?)fechaNube ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                catch { /* no romper */ }
            }
        }

        
        private sealed class ContpaqData
        {
            public string GuidDsl { get; set; } = "";
            public string GuidEmpresa { get; set; } = "";
            public string RFC { get; set; } = "";
            public string RazonSocial { get; set; } = "";
            public bool HasGuidDsl => !string.IsNullOrWhiteSpace(GuidDsl);
        }

        private sealed class NominaData
        {
            public string GuidDsl { get; set; } = "";
            public string GuidEmpresa { get; set; } = "";
            public string RFC { get; set; } = "";
            public string NombreCorto { get; set; } = "";
        }

        private sealed class ComercialData
        {
            public string CNombreEmpresa { get; set; } = "";
            public string CGuidDSL { get; set; } = "";
            public string CGuidEmpresa { get; set; } = "";
            public string CRFCEmpresa { get; set; } = "";
        }

        private sealed class SpecialPack
        {
            public string DocumentMetadataBak { get; set; } = "";
            public string DocumentContentBak { get; set; } = "";
            public string OtherMetadataBak { get; set; } = "";
            public string OtherContentBak { get; set; } = "";
            public string ControlJsonPath { get; set; } = "";
            public string InnerZipPath { get; set; } = ""; // 
            public double SizeDocumentMetadataGB { get; set; }
            public double SizeDocumentContentGB { get; set; }
            public double SizeOtherMetadataGB { get; set; }
            public double SizeOtherContentGB { get; set; }
        }

 
        private static async Task<ContpaqData> GetParametrosContpaqAsync(SqlConnection con, string db)
        {
            var res = new ContpaqData();
            var cmd = con.CreateCommand();
            cmd.CommandText = $"USE [{db}]; SELECT GUIDDSL, RFC, GUIDEMPRESA FROM PARAMETROS";
            try
            {
                await using var rd = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                if (await rd.ReadAsync().ConfigureAwait(false))
                {
                    res.GuidDsl = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    res.RFC = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    res.GuidEmpresa = rd.IsDBNull(2) ? "" : rd.GetString(2);
                    res.RazonSocial = db; // nombre BD como razón social cuando aplica
                }
            }
            catch { /* si no existe, se devuelve vacío */ }
            return res;
        }

     
        private static async Task<NominaData> GetParametrosNominaAsync(SqlConnection con, string db)
        {
            var res = new NominaData();
            var cmd = con.CreateCommand();
            cmd.CommandText = $@"USE [{db}];
                SELECT TOP 1 A.CNOMBREEMPRESA, A.GUIDDSL, A.GUIDEMPRESA, A.RFC
                FROM (
                    SELECT
                        (SELECT TOP 1 CNOMBREEMPRESA FROM NOM10000) AS CNOMBREEMPRESA,
                        (SELECT TOP 1 GUIDDSL        FROM NOM10000) AS GUIDDSL,
                        (SELECT TOP 1 GUIDEMPRESA    FROM NOM10000) AS GUIDEMPRESA,
                        (SELECT TOP 1 RFC            FROM NOM10000) AS RFC
                ) A";
            try
            {
                await using var rd = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                if (await rd.ReadAsync().ConfigureAwait(false))
                {
                    res.NombreCorto = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    res.GuidDsl = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    res.GuidEmpresa = rd.IsDBNull(2) ? "" : rd.GetString(2);
                    res.RFC = rd.IsDBNull(3) ? "" : rd.GetString(3);
                }
            }
            catch { }
            return res;
        }


        private static async Task<ComercialData> GetParametrosComercialAsync(SqlConnection con, string db)
        {
            var res = new ComercialData();
            var cmd = con.CreateCommand();
            cmd.CommandText = $"USE [{db}]; SELECT CNOMBREEMPRESA, CGUIDDSL, CGUIDEMPRESA, CRFCEMPRESA FROM ADMPARAMETROS";
            try
            {
                await using var rd = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                if (await rd.ReadAsync().ConfigureAwait(false))
                {
                    res.CNombreEmpresa = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    res.CGuidDSL = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    res.CGuidEmpresa = rd.IsDBNull(2) ? "" : rd.GetString(2);
                    res.CRFCEmpresa = rd.IsDBNull(3) ? "" : rd.GetString(3);
                }
            }
            catch { }
            return res;
        }

        private static async Task<SpecialPack> BackupSpecialDatabasesAsync(SqlConnection con, string pathBase, string instancia, BackupConfig cfg, string guidDsl)
        {
            var guid = guidDsl.ToLowerInvariant();

            var docMeta = $"document_{guid}_metadata";
            var docCont = $"document_{guid}_content";
            var othMeta = $"other_{guid}_metadata";
            var othCont = $"other_{guid}_content";

            var special = new SpecialPack
            {
                DocumentMetadataBak = Path.Combine(pathBase, $"{docMeta}.bak"),
                DocumentContentBak = Path.Combine(pathBase, $"{docCont}.bak"),
                OtherMetadataBak = Path.Combine(pathBase, $"{othMeta}.bak"),
                OtherContentBak = Path.Combine(pathBase, $"{othCont}.bak"),
                ControlJsonPath = Path.Combine(pathBase, "controlDocument.json"),
                InnerZipPath = "" // se asigna fuera a {db}.zip
            };

         
            async Task<bool> DbExistsAsync(string name)
            {
                var cmd = con.CreateCommand();
                cmd.CommandText = $"SELECT CASE WHEN DB_ID(@n) IS NULL THEN 0 ELSE 1 END";
                cmd.Parameters.AddWithValue("@n", name);
                var val = (int)(await cmd.ExecuteScalarAsync().ConfigureAwait(false) ?? 0);
                return val == 1;
            }

          
            async Task BackupDbAsync(string name, string targetBak)
            {
                if (!await DbExistsAsync(name).ConfigureAwait(false)) return;
                var cmdBak = con.CreateCommand();
                var comp = (cfg.RespaldarNube) ? ", COMPRESSION" : ""; // si es Express, SQL ignora COMPRESSION
                cmdBak.CommandText = $"BACKUP DATABASE [{name}] TO DISK=@p WITH FORMAT, INIT{comp}";
                cmdBak.Parameters.AddWithValue("@p", targetBak);
                await cmdBak.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await BackupDbAsync(docMeta, special.DocumentMetadataBak).ConfigureAwait(false);
            await BackupDbAsync(docCont, special.DocumentContentBak).ConfigureAwait(false);
            await BackupDbAsync(othMeta, special.OtherMetadataBak).ConfigureAwait(false);
            await BackupDbAsync(othCont, special.OtherContentBak).ConfigureAwait(false);

        
            static double SizeGb(string p)
            {
                if (!File.Exists(p)) return 0d;
                var len = new FileInfo(p).Length; // bytes
                var gb = len / 1024d / 1024d / 1024d;
                return Math.Round(gb, 3);
            }

            special.SizeDocumentMetadataGB = SizeGb(special.DocumentMetadataBak);
            special.SizeDocumentContentGB = SizeGb(special.DocumentContentBak);
            special.SizeOtherMetadataGB = SizeGb(special.OtherMetadataBak);
            special.SizeOtherContentGB = SizeGb(special.OtherContentBak);

            return special;
        }

      
        private static async Task GenerateControlDocumentJsonContpaqAsync(string pathBase, ContpaqData data, SpecialPack sp)
        {
            var fecha = DateTime.Now;
            string json = new System.Text.StringBuilder(@"
{
  ""DateBackUp"": ""@FECHA"",
  ""GuidCompany"": ""@GUIDDSL"",
  ""Version"": ""1.0.3.0"",
  ""NombreEmpresa"": ""@RAZONSOCIAL"",
  ""Syncronized"": ""@FECHA"",
  ""Accesibility"": ""PRIVATE"",
  ""TimeStamp"": ""@FECHA 00:00:00"",
  ""Alias"": ""@RAZONSOCIAL"",
  ""RFC"": ""@RFC"",
  ""CompanyPath"": """",
  ""StorageType"": ""SQL"",
  ""ProductVersion"": ""10.50.1600.1"",
  ""Owners"": [
    {
      ""guidOwner"": ""@GUIDEMPRESA"",
      ""applicationType"": ""CONTABILIDAD"",
      ""memberType"": ""GROUP"",
      ""read"": ""1"",
      ""write"": ""1""
    }
  ],
  ""BackupOptions"": [
    { ""backup"": ""document_@GUIDDSL_metadata.bak"", ""storageType"": ""document"", ""dataType"": ""metadata"", ""backupType"": ""database"", ""TotalReg"": 24200, ""TotalDisk"": ""@TAM1gb"", ""VersionSchema"": ""3.70"" },
    { ""backup"": ""document_@GUIDDSL_content.bak"",  ""storageType"": ""document"", ""dataType"": ""content"",  ""backupType"": ""database"", ""TotalReg"": 24200, ""TotalDisk"": ""@TAM2gb"", ""VersionSchema"": ""1.20"" },
    { ""backup"": ""other_@GUIDDSL_metadata.bak"",    ""storageType"": ""other"",   ""dataType"": ""metadata"", ""backupType"": ""database"", ""TotalReg"": 21031, ""TotalDisk"": ""@TAM3gb"", ""VersionSchema"": ""3.70"" },
    { ""backup"": ""other_@GUIDDSL_content.bak"",     ""storageType"": ""other"",   ""dataType"": ""content"",  ""backupType"": ""database"", ""TotalReg"": 8958,  ""TotalDisk"": ""@TAM4gb"", ""VersionSchema"": ""1.20"" }
  ]
}
").ToString();

            json = json.Replace("@FECHA", $"{fecha:yyyy-MM-dd}")
                       .Replace("@GUIDDSL", data.GuidDsl.ToLowerInvariant())
                       .Replace("@RAZONSOCIAL", data.RazonSocial)
                       .Replace("@RFC", data.RFC)
                       .Replace("@GUIDEMPRESA", data.GuidEmpresa.ToLowerInvariant())
                       .Replace("@TAM1", sp.SizeDocumentMetadataGB.ToString("0.000"))
                       .Replace("@TAM2", sp.SizeDocumentContentGB.ToString("0.000"))
                       .Replace("@TAM3", sp.SizeOtherMetadataGB.ToString("0.000"))
                       .Replace("@TAM4", sp.SizeOtherContentGB.ToString("0.000"));

            await File.WriteAllTextAsync(sp.ControlJsonPath, json).ConfigureAwait(false);

        
            await CreateInnerZipAsync(sp.InnerZipPath,
                new[]
                {
                    sp.DocumentMetadataBak,
                    sp.DocumentContentBak,
                    sp.OtherMetadataBak,
                    sp.OtherContentBak,
                    sp.ControlJsonPath
                }).ConfigureAwait(false);

       
            SafeDelete(sp.DocumentMetadataBak);
            SafeDelete(sp.DocumentContentBak);
            SafeDelete(sp.OtherMetadataBak);
            SafeDelete(sp.OtherContentBak);
            SafeDelete(sp.ControlJsonPath);
        }

        private static async Task GenerateControlDocumentJsonNominaAsync(string pathBase, NominaData data, SpecialPack sp)
        {
            var fecha = DateTime.Now;
            string json = new System.Text.StringBuilder(@"
{
  ""DateBackUp"": ""@FECHA"",
  ""GuidCompany"": ""@GUIDDSL"",
  ""Version"": ""1.0.3.0"",
  ""NombreEmpresa"": ""@RAZONSOCIAL"",
  ""Syncronized"": ""@FECHA"",
  ""Accesibility"": ""PRIVATE"",
  ""TimeStamp"": ""@FECHA 00:00:00"",
  ""Alias"": ""@RAZONSOCIAL"",
  ""RFC"": ""@RFC"",
  ""CompanyPath"": """",
  ""StorageType"": ""SQL"",
  ""ProductVersion"": ""10.50.1600.1"",
  ""Owners"": [
    {
      ""guidOwner"": ""@GUIDEMPRESA"",
      ""applicationType"": ""NOMINAS"",
      ""memberType"": ""GROUP"",
      ""read"": ""1"",
      ""write"": ""1""
    }
  ],
  ""BackupOptions"": [
    { ""backup"": ""document_@GUIDDSL_metadata.bak"", ""storageType"": ""document"", ""dataType"": ""metadata"", ""backupType"": ""database"", ""TotalReg"": 24200, ""TotalDisk"": ""@TAM1gb"", ""VersionSchema"": ""3.70"" },
    { ""backup"": ""document_@GUIDDSL_content.bak"",  ""storageType"": ""document"", ""dataType"": ""content"",  ""backupType"": ""database"", ""TotalReg"": 24200, ""TotalDisk"": ""@TAM2gb"", ""VersionSchema"": ""1.20"" },
    { ""backup"": ""other_@GUIDDSL_metadata.bak"",    ""storageType"": ""other"",   ""dataType"": ""metadata"", ""backupType"": ""database"", ""TotalReg"": 21031, ""TotalDisk"": ""@TAM3gb"", ""VersionSchema"": ""3.70"" },
    { ""backup"": ""other_@GUIDDSL_content.bak"",     ""storageType"": ""other"",   ""dataType"": ""content"",  ""backupType"": ""database"", ""TotalReg"": 8958,  ""TotalDisk"": ""@TAM4gb"", ""VersionSchema"": ""1.20"" }
  ]
}
").ToString();

            json = json.Replace("@FECHA", $"{fecha:yyyy-MM-dd}")
                       .Replace("@GUIDDSL", data.GuidDsl.ToLowerInvariant())
                       .Replace("@RAZONSOCIAL", data.NombreCorto)
                       .Replace("@RFC", data.RFC)
                       .Replace("@GUIDEMPRESA", data.GuidEmpresa.ToLowerInvariant())
                       .Replace("@TAM1", sp.SizeDocumentMetadataGB.ToString("0.000"))
                       .Replace("@TAM2", sp.SizeDocumentContentGB.ToString("0.000"))
                       .Replace("@TAM3", sp.SizeOtherMetadataGB.ToString("0.000"))
                       .Replace("@TAM4", sp.SizeOtherContentGB.ToString("0.000"));

            await File.WriteAllTextAsync(sp.ControlJsonPath, json).ConfigureAwait(false);

            await CreateInnerZipAsync(sp.InnerZipPath,
                new[]
                {
                    sp.DocumentMetadataBak,
                    sp.DocumentContentBak,
                    sp.OtherMetadataBak,
                    sp.OtherContentBak,
                    sp.ControlJsonPath
                }).ConfigureAwait(false);

            SafeDelete(sp.DocumentMetadataBak);
            SafeDelete(sp.DocumentContentBak);
            SafeDelete(sp.OtherMetadataBak);
            SafeDelete(sp.OtherContentBak);
            SafeDelete(sp.ControlJsonPath);
        }


        private static async Task GenerateControlDocumentJsonComercialAsync(string pathBase, ComercialData data, SpecialPack sp)
        {
            var fecha = DateTime.Now;
            string json = new System.Text.StringBuilder(@"
{
  ""DateBackUp"": ""@FECHA"",
  ""GuidCompany"": ""@GUIDDSL"",
  ""Version"": ""1.0.3.0"",
  ""NombreEmpresa"": ""@RAZONSOCIAL"",
  ""Syncronized"": ""@FECHA"",
  ""Accesibility"": ""PRIVATE"",
  ""TimeStamp"": ""@FECHA 00:00:00"",
  ""Alias"": ""@RAZONSOCIAL"",
  ""RFC"": ""@RFC"",
  ""CompanyPath"": """",
  ""StorageType"": ""SQL"",
  ""ProductVersion"": ""10.50.1600.1"",
  ""Owners"": [
    {
      ""guidOwner"": ""@GUIDEMPRESA"",
      ""applicationType"": ""COMERCIAL"",
      ""memberType"": ""GROUP"",
      ""read"": ""1"",
      ""write"": ""1""
    }
  ],
  ""BackupOptions"": [
    { ""backup"": ""document_@GUIDDSL_metadata.bak"", ""storageType"": ""document"", ""dataType"": ""metadata"", ""backupType"": ""database"", ""TotalReg"": 24200, ""TotalDisk"": ""@TAM1gb"", ""VersionSchema"": ""3.70"" },
    { ""backup"": ""document_@GUIDDSL_content.bak"",  ""storageType"": ""document"", ""dataType"": ""content"",  ""backupType"": ""database"", ""TotalReg"": 24200, ""TotalDisk"": ""@TAM2gb"", ""VersionSchema"": ""1.20"" },
    { ""backup"": ""other_@GUIDDSL_metadata.bak"",    ""storageType"": ""other"",   ""dataType"": ""metadata"", ""backupType"": ""database"", ""TotalReg"": 21031, ""TotalDisk"": ""@TAM3gb"", ""VersionSchema"": ""3.70"" },
    { ""backup"": ""other_@GUIDDSL_content.bak"",     ""storageType"": ""other"",   ""dataType"": ""content"",  ""backupType"": ""database"", ""TotalReg"": 8958,  ""TotalDisk"": ""@TAM4gb"", ""VersionSchema"": ""1.20"" }
  ]
}
").ToString();

            json = json.Replace("@FECHA", $"{fecha:yyyy-MM-dd}")
                       .Replace("@GUIDDSL", data.CGuidDSL.ToLowerInvariant())
                       .Replace("@RAZONSOCIAL", data.CNombreEmpresa)
                       .Replace("@RFC", data.CRFCEmpresa)
                       .Replace("@GUIDEMPRESA", data.CGuidEmpresa.ToLowerInvariant())
                       .Replace("@TAM1", sp.SizeDocumentMetadataGB.ToString("0.000"))
                       .Replace("@TAM2", sp.SizeDocumentContentGB.ToString("0.000"))
                       .Replace("@TAM3", sp.SizeOtherMetadataGB.ToString("0.000"))
                       .Replace("@TAM4", sp.SizeOtherContentGB.ToString("0.000"));

            await File.WriteAllTextAsync(sp.ControlJsonPath, json).ConfigureAwait(false);

            await CreateInnerZipAsync(sp.InnerZipPath,
                new[]
                {
                    sp.DocumentMetadataBak,
                    sp.DocumentContentBak,
                    sp.OtherMetadataBak,
                    sp.OtherContentBak,
                    sp.ControlJsonPath
                }).ConfigureAwait(false);

            SafeDelete(sp.DocumentMetadataBak);
            SafeDelete(sp.DocumentContentBak);
            SafeDelete(sp.OtherMetadataBak);
            SafeDelete(sp.OtherContentBak);
            SafeDelete(sp.ControlJsonPath);
        }

        private static async Task CreateInnerZipAsync(string zipPath, IEnumerable<string> files)
        {
            await Task.Run(() =>
            {
                ZipStrings.UseUnicode = true;

                using var fsOut = File.Create(zipPath);
                using var zipStream = new ZipOutputStream(fsOut);
                zipStream.SetLevel(9); // máximo

                var buffer = new byte[4096];
                foreach (var f in files)
                {
                    if (!File.Exists(f)) continue;
                    var entry = new ZipEntry(Path.GetFileName(f)) { DateTime = DateTime.Now };
                    zipStream.PutNextEntry(entry);

                    using var fs = File.OpenRead(f);
                    int read;
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                        zipStream.Write(buffer, 0, read);
                }
                zipStream.Finish();
            }).ConfigureAwait(false);
        }
    }
}

// Infrastructure/EmailService.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace SACTIBACKUP.Infrastructure
{
    /// <summary>
    /// Resultado del intento de envio de correo. Permite al llamador saber
    /// si se envio, a quienes y por que fallo cuando no se envio.
    /// </summary>
    public sealed class EmailSendResult
    {
        public bool Enviado { get; init; }
        public List<string> Destinatarios { get; init; } = new();
        public List<string> Invalidos { get; init; } = new();
        /// <summary>Destinatarios que el servidor SMTP rechazo individualmente.</summary>
        public List<string> Rechazados { get; init; } = new();
        public string? Error { get; init; }

        public string Resumen()
        {
            var sb = new StringBuilder();
            sb.Append(Enviado ? "Correo ENVIADO" : "Correo NO ENVIADO");
            sb.Append(" | destinatarios=");
            sb.Append(Destinatarios.Count > 0 ? string.Join(";", Destinatarios) : "(ninguno)");
            if (Invalidos.Count > 0)
                sb.Append(" | invalidos=").Append(string.Join(";", Invalidos));
            if (Rechazados.Count > 0)
                sb.Append(" | rechazados=").Append(string.Join(" ", Rechazados));
            if (!string.IsNullOrWhiteSpace(Error))
                sb.Append(" | error=").Append(Error);
            return sb.ToString();
        }
    }

    public static class EmailService
    {
        private static readonly char[] Separadores = { ',', ';', '\r', '\n', '\t', ' ' };

        /// <summary>
        /// Separa una cadena de correos por ',' o ';' (tambien espacios y saltos de linea),
        /// elimina duplicados y clasifica cada uno en valido / invalido.
        /// </summary>
        public static (List<string> Validos, List<string> Invalidos) ParseDestinatarios(string? correos)
        {
            var validos = new List<string>();
            var invalidos = new List<string>();
            if (string.IsNullOrWhiteSpace(correos))
                return (validos, invalidos);

            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in correos.Split(Separadores, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidato = raw.Trim();
                if (candidato.Length == 0) continue;
                if (!vistos.Add(candidato)) continue;   // duplicado

                if (EsCorreoValido(candidato))
                    validos.Add(candidato);
                else
                    invalidos.Add(candidato);
            }

            return (validos, invalidos);
        }

        /// <summary>Valida un correo usando el mismo parser que usara MailMessage.</summary>
        public static bool EsCorreoValido(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo)) return false;
            var arroba = correo.IndexOf('@');
            if (arroba <= 0) return false;
            if (correo.IndexOf('.', arroba) < 0) return false;   // exige dominio con punto
            if (correo.EndsWith(".", StringComparison.Ordinal)) return false;
            try
            {
                // MailAddress acepta "Nombre <a@b.com>"; aqui exigimos la direccion sola.
                var addr = new MailAddress(correo);
                return string.Equals(addr.Address, correo, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Normaliza la lista para guardarla en el JSON de configuracion.</summary>
        public static string NormalizarLista(IEnumerable<string> correos)
            => string.Join("; ", correos);

        /// <summary>Une varias cadenas de correos en una sola lista sin duplicados.</summary>
        public static string CombinarListas(params string?[] listas)
        {
            var acumulado = new List<string>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lista in listas)
            {
                var (validos, _) = ParseDestinatarios(lista);
                foreach (var c in validos)
                    if (vistos.Add(c)) acumulado.Add(c);
            }
            return NormalizarLista(acumulado);
        }

        public static EmailSendResult SendBackupResult(
            bool error,
            string mensaje,
            string nota,
            string nombreEmpresa,
            string correoEmisor,
            string passwordElEmisor,
            string correoReceptorCliente,
            string correoReceptorSacti = "backup@sacti.mx",
            string plantillaHtmlPath = "PlantillaCorreoSactiBackUp.html"
        )
        {
            var (destinatarios, invalidos) = ParseDestinatarios(correoReceptorCliente);

            if (invalidos.Count > 0)
                BackupLogger.Log("EmailService",
                    $"ADVERTENCIA: correos con formato invalido, se omiten: {string.Join(";", invalidos)}");

            if (destinatarios.Count == 0)
            {
                var sinDestino = new EmailSendResult
                {
                    Enviado = false,
                    Invalidos = invalidos,
                    Error = $"No hay ningun correo destinatario valido configurado (valor recibido: '{correoReceptorCliente}')."
                };
                BackupLogger.Log("EmailService", sinDestino.Resumen());
                LogCorreoDetalle(null, correoEmisor, correoReceptorCliente, sinDestino);
                return sinDestino;
            }

            BackupLogger.Log("EmailService",
                $"Enviando correo a {destinatarios.Count} destinatario(s): {string.Join(";", destinatarios)}" +
                (string.IsNullOrWhiteSpace(correoReceptorSacti) ? "" : $" (BCC: {correoReceptorSacti})"));

            try
            {
                var cuerpoCorreo = BuildHtmlFromTemplate(plantillaHtmlPath, mensaje, nota, nombreEmpresa);

                using var client = new SmtpClient("mail.sacti.mx", 587)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(correoEmisor, passwordElEmisor),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 20000
                };

                using var mail = new MailMessage
                {
                    IsBodyHtml = true,
                    Body = cuerpoCorreo,
                    Priority = MailPriority.High,
                    From = new MailAddress(correoEmisor, "SACTI-BACKUP"),
                    Subject = error
                        ? $"{nombreEmpresa} Hubo un error al respaldar sus datos"
                        : $"{nombreEmpresa} Su respaldo fue realizado con éxito!!",
                };

                if (mensaje == "No se puede realizar el respaldo de su información debido a que su licencia ha caducado.")
                    mail.Subject = "Error en licencia";

                // Destinatarios: uno o varios, separados por ',' o ';'
                foreach (var d in destinatarios)
                    mail.To.Add(new MailAddress(d));

                if (!string.IsNullOrWhiteSpace(correoReceptorSacti))
                    mail.Bcc.Add(new MailAddress(correoReceptorSacti));

                var plainView = AlternateView.CreateAlternateViewFromString(cuerpoCorreo, null, "text/plain");
                var htmlView = AlternateView.CreateAlternateViewFromString(cuerpoCorreo, Encoding.UTF8, "text/html");
                mail.AlternateViews.Add(plainView);
                mail.AlternateViews.Add(htmlView);

                client.Send(mail);

                var ok = new EmailSendResult
                {
                    Enviado = true,
                    Destinatarios = destinatarios,
                    Invalidos = invalidos
                };
                BackupLogger.Log("EmailService", ok.Resumen());
                return ok;
            }
            catch (SmtpFailedRecipientsException exRecs)
            {
                // El servidor acepto la conexion pero rechazo uno o varios destinatarios.
                var rechazados = exRecs.InnerExceptions
                    .Select(ie => $"[{ie.FailedRecipient} -> {ie.StatusCode}: {DescribirStatus(ie.StatusCode)}]")
                    .ToList();

                var res = new EmailSendResult
                {
                    Enviado = false,
                    Destinatarios = destinatarios,
                    Invalidos = invalidos,
                    Rechazados = rechazados,
                    Error = $"SmtpFailedRecipientsException: {exRecs.Message}"
                };
                BackupLogger.Log("EmailService", res.Resumen());
                LogCorreoDetalle(exRecs, correoEmisor, correoReceptorCliente, res);
                return res;
            }
            catch (SmtpException exSmtp)
            {
                var res = new EmailSendResult
                {
                    Enviado = false,
                    Destinatarios = destinatarios,
                    Invalidos = invalidos,
                    Error = $"SmtpException ({exSmtp.StatusCode}): {DescribirStatus(exSmtp.StatusCode)} - {exSmtp.Message}" +
                            (exSmtp.InnerException is null ? "" : $" | Inner: {exSmtp.InnerException.Message}")
                };
                BackupLogger.Log("EmailService", res.Resumen());
                LogCorreoDetalle(exSmtp, correoEmisor, correoReceptorCliente, res);
                return res;
            }
            catch (Exception ex)
            {
                var res = new EmailSendResult
                {
                    Enviado = false,
                    Destinatarios = destinatarios,
                    Invalidos = invalidos,
                    Error = $"{ex.GetType().Name}: {ex.Message}"
                };
                BackupLogger.Log("EmailService", res.Resumen());
                LogCorreoDetalle(ex, correoEmisor, correoReceptorCliente, res);
                return res;
            }
        }

        /// <summary>Traduce los codigos SMTP mas comunes a algo que un humano pueda leer.</summary>
        private static string DescribirStatus(SmtpStatusCode code) => code switch
        {
            SmtpStatusCode.Ok => "OK",
            SmtpStatusCode.MailboxBusy => "Buzon ocupado, intente mas tarde",
            SmtpStatusCode.MailboxUnavailable => "Buzon no disponible: la cuenta no existe o rechazo el mensaje",
            SmtpStatusCode.MailboxNameNotAllowed => "Nombre de buzon no permitido",
            SmtpStatusCode.ClientNotPermitted => "Cliente no autorizado por el servidor",
            SmtpStatusCode.MustIssueStartTlsFirst => "El servidor exige STARTTLS",
            SmtpStatusCode.TransactionFailed => "Transaccion rechazada (posible filtro antispam o cuota excedida)",
            SmtpStatusCode.GeneralFailure => "Falla general de conexion o autenticacion",
            SmtpStatusCode.ServiceNotAvailable => "Servicio SMTP no disponible",
            SmtpStatusCode.InsufficientStorage => "Buzon lleno",
            SmtpStatusCode.ExceededStorageAllocation => "Cuota de almacenamiento excedida",
            _ => code.ToString()
        };

        private static void LogCorreoDetalle(Exception? ex, string correoEmisor, string correoReceptor, EmailSendResult res)
        {
            try
            {
                var logDir = @"C:\Logs\Correo";
                Directory.CreateDirectory(logDir);

                var logFile = Path.Combine(
                    logDir,
                    $"CorreoError_{DateTime.Now:yyyyMMdd}.txt"
                );

                var sb = new StringBuilder();
                sb.AppendLine("======================================");
                sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Emisor: {correoEmisor}");
                sb.AppendLine($"Receptor (configurado): {correoReceptor}");
                sb.AppendLine($"Destinatarios validos: {(res.Destinatarios.Count > 0 ? string.Join("; ", res.Destinatarios) : "(ninguno)")}");
                if (res.Invalidos.Count > 0)
                    sb.AppendLine($"Correos invalidos omitidos: {string.Join("; ", res.Invalidos)}");
                if (res.Rechazados.Count > 0)
                    sb.AppendLine($"Rechazados por el servidor: {string.Join(" ", res.Rechazados)}");
                sb.AppendLine($"Resultado: {(res.Enviado ? "ENVIADO" : "NO ENVIADO")}");
                if (!string.IsNullOrWhiteSpace(res.Error))
                    sb.AppendLine($"Motivo: {res.Error}");
                if (ex is not null)
                {
                    sb.AppendLine("Excepción:");
                    sb.AppendLine(ex.ToString());
                }
                sb.AppendLine();

                File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Nunca lanzar excepción desde el logger
            }
        }

        private static string BuildHtmlFromTemplate(string plantillaHtmlPath, string mensaje, string nota, string nombreEmpresa)
        {
            try
            {
                // La app se lanza por tarea programada: el CurrentDirectory NO es la carpeta del exe.
                var ruta = Path.IsPathRooted(plantillaHtmlPath)
                    ? plantillaHtmlPath
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, plantillaHtmlPath);

                if (!File.Exists(ruta))
                {
                    BackupLogger.Log("EmailService", $"ADVERTENCIA: plantilla no encontrada en '{ruta}'. Se envia cuerpo simple.");
                    return $"<html><body><h3>{nombreEmpresa}</h3><p>{mensaje}</p><p>{nota}</p></body></html>";
                }

                var sb = new StringBuilder();
                var allLines = File.ReadAllLines(ruta);
                foreach (var line in allLines)
                {
                    var renglon = line
                        .Replace("@MensajeCorreo", $" {nombreEmpresa} {mensaje}")
                        .Replace("@NotaCorreo", nota);
                    sb.AppendLine(renglon);
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                BackupLogger.LogException("EmailService", "BuildHtmlFromTemplate", ex);
                // Fallback simple si falta la plantilla
                return $"<html><body><h3>{nombreEmpresa}</h3><p>{mensaje}</p><p>{nota}</p></body></html>";
            }
        }
    }
}


// Infrastructure/EmailService.cs
#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace SACTIBACKUP.Infrastructure
{
   
    public static class EmailService
    {
      
        public static void SendBackupResult(
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

               ///
                if (mensaje == "No se puede realizar el respaldo de su información debido a que su licencia ha caducado.")
                    mail.Subject = "Error en licencia";

                // Destinatarios
                if (!string.IsNullOrWhiteSpace(correoReceptorCliente))
                    mail.To.Add(new MailAddress(correoReceptorCliente));
                if (!string.IsNullOrWhiteSpace(correoReceptorSacti))
                    mail.Bcc.Add(new MailAddress(correoReceptorSacti));

            
                var plainView = AlternateView.CreateAlternateViewFromString(cuerpoCorreo, null, "text/plain");
                var htmlView = AlternateView.CreateAlternateViewFromString(cuerpoCorreo, Encoding.UTF8, "text/html");
                mail.AlternateViews.Add(plainView);
                mail.AlternateViews.Add(htmlView);

            
                client.Send(mail);
            }
            catch (Exception ex)
            {
                LogCorreoError(ex, correoEmisor, correoReceptorCliente);
            }
        }

        private static void LogCorreoError(Exception ex, string correoEmisor, string correoReceptor)
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
                sb.AppendLine($"Receptor: {correoReceptor}");
                sb.AppendLine("Excepción:");
                sb.AppendLine(ex.ToString());
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
                var sb = new StringBuilder();
                var allLines = File.ReadAllLines(plantillaHtmlPath);
                foreach (var line in allLines)
                {
                    var renglon = line
                        .Replace("@MensajeCorreo", $" {nombreEmpresa} {mensaje}")
                        .Replace("@NotaCorreo", nota);
                    sb.AppendLine(renglon);
                }
                return sb.ToString();
            }
            catch
            {
                // Fallback simple si falta la plantilla
                return $"<html><body><h3>{nombreEmpresa}</h3><p>{mensaje}</p><p>{nota}</p></body></html>";
            }
        }
    }
}

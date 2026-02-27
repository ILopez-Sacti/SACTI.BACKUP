
#nullable enable
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SACTIBACKUP.Infrastructure
{
    /// <summary>
    /// Valida credenciales FTP haciendo un PrintWorkingDirectory al host indicado.
    /// </summary>
    public static class FtpValidationService
    {
        /// <param name="host">Ej. "sacti.ddns.net" o "ftp://sacti.ddns.net"</param>
        public static async Task<bool> ValidarCredencialesAsync(
            string host, string usuario, string password, bool useSsl = false, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
                return false;

            var uriString = host.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
                            || host.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase)
                            ? host
                            : $"ftp://{host}";

            var request = (FtpWebRequest)WebRequest.Create(new Uri(uriString));
            request.Method = WebRequestMethods.Ftp.PrintWorkingDirectory;
            request.Credentials = new NetworkCredential(usuario, password);
            request.UsePassive = true;
            request.EnableSsl = useSsl;   
            //request.KeepAlive = false;
            //request.ReadWriteTimeout = 5000; 
            //request.Timeout = 5000;        

            try
            {
                using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                response.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

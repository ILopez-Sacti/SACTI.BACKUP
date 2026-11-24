#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SACTIBACKUP.Infrastructure
{
    /// <summary>Resumen de lo que hizo la rotación de respaldos en el FTP.</summary>
    public sealed class FtpRotationResult
    {
        public int ArchivosEncontrados { get; set; }
        public int BorradosPorCantidad { get; set; }
        public int BorradosPorAntiguedad { get; set; }
        public List<string> Errores { get; } = new();

        public string Resumen()
        {
            var sb = new StringBuilder();
            sb.Append($"encontrados={ArchivosEncontrados}, ");
            sb.Append($"borrados_por_cantidad={BorradosPorCantidad}, ");
            sb.Append($"borrados_por_antiguedad={BorradosPorAntiguedad}");
            if (Errores.Count > 0)
                sb.Append($", errores={Errores.Count} -> {string.Join(" | ", Errores.Take(5))}");
            return sb.ToString();
        }
    }

    public static class FtpRotationService
    {
        /// <summary>
        /// Aplica las políticas de retención en el FTP. Es housekeeping: nunca lanza excepción,
        /// los problemas se acumulan en el resultado para que el llamador los registre.
        /// </summary>
        /// <param name="archivosProtegidos">
        /// Archivos subidos y verificados en esta corrida: nunca se borran, sin importar politica.
        /// </param>
        public static async Task<FtpRotationResult> ApplyPoliciesAsync(
            string host, string usuario, string password, int mantenerMinimo, int borrarCadaDias,
            IEnumerable<string>? archivosProtegidos = null)
        {
            var result = new FtpRotationResult();
            var protegidos = new HashSet<string>(archivosProtegidos ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            // Siempre se conserva al menos el respaldo mas reciente de cada BD, aunque la
            // licencia traiga MantenerMinimo en 0: la rotacion jamas debe dejar la nube vacia.
            var minimoGarantizado = Math.Max(1, mantenerMinimo);
            var baseUri = $"ftp://{host}";

            List<string> files;
            try
            {
                files = await ListFilesAsync(baseUri, usuario, password).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result.Errores.Add($"No se pudo listar el FTP: {Describir(ex)}");
                return result;
            }

            files = files
                .Select(f => f.Trim())
                .Where(f => f.Length > 0 && !f.StartsWith("."))
                .ToList();

            result.ArchivosEncontrados = files.Count;

            // Lleva registro de lo borrado para no volver a consultarlo despues:
            // pedir la fecha de un archivo ya borrado devuelve 550 y antes eso
            // tiraba una WebException que abortaba todo el respaldo.
            var borrados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Los N mas recientes de cada grupo (misma BD, distinta fecha) quedan protegidos
            // para AMBAS politicas. Antes la politica por antiguedad los ignoraba y, si la
            // corrida no subia nada nuevo, borraba todo y dejaba la nube vacia.
            var groups = files
                .GroupBy(BaseName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase).ToList());

            foreach (var list in groups.Values)
                foreach (var file in list.Take(minimoGarantizado))
                    protegidos.Add(file);

            // --- Politica 1: conservar solo los N mas recientes de cada grupo ---
            if (mantenerMinimo > 0)
            {
                foreach (var kv in groups)
                {
                    var list = kv.Value;
                    if (list.Count <= mantenerMinimo) continue;

                    foreach (var file in list.Skip(mantenerMinimo))
                    {
                        if (protegidos.Contains(file)) continue;
                        if (await TryDeleteAsync($"{baseUri}/{file}", usuario, password, file, result).ConfigureAwait(false))
                        {
                            borrados.Add(file);
                            result.BorradosPorCantidad++;
                        }
                    }
                }
            }

            // --- Politica 2: borrar los mas viejos que el corte ---
            if (borrarCadaDias > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-borrarCadaDias);

                foreach (var file in files)
                {
                    if (borrados.Contains(file)) continue;   // ya no existe
                    if (protegidos.Contains(file)) continue; // mas reciente de su BD o subido en esta corrida

                    DateTime? lastMod;
                    try
                    {
                        lastMod = await GetLastModifiedAsync($"{baseUri}/{file}", usuario, password).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        result.Errores.Add($"fecha de '{file}': {Describir(ex)}");
                        continue;   // un archivo problematico no debe abortar la rotacion
                    }

                    if (lastMod.HasValue && lastMod.Value.ToUniversalTime() <= cutoff)
                    {
                        if (await TryDeleteAsync($"{baseUri}/{file}", usuario, password, file, result).ConfigureAwait(false))
                        {
                            borrados.Add(file);
                            result.BorradosPorAntiguedad++;
                        }
                    }
                }
            }

            return result;
        }

        private static async Task<bool> TryDeleteAsync(
            string fileUri, string user, string pass, string nombre, FtpRotationResult result)
        {
            try
            {
                await DeleteFileAsync(fileUri, user, pass).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                result.Errores.Add($"borrar '{nombre}': {Describir(ex)}");
                return false;
            }
        }

        private static string Describir(Exception ex)
        {
            if (ex is WebException wex && wex.Response is FtpWebResponse resp)
                return $"{resp.StatusCode} - {resp.StatusDescription?.Trim()}";
            return $"{ex.GetType().Name}: {ex.Message}";
        }

        private static string BaseName(string fileName)
        {
            var name = fileName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)
                        ? fileName[..^4] : fileName;

            if (name.Length > 8 && int.TryParse(name[^8..], out _))
                return name[..^8]; // base sin fecha
            return name;
        }

        private static async Task<List<string>> ListFilesAsync(string folderUri, string user, string pass)
        {
            var request = (FtpWebRequest)WebRequest.Create(folderUri);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.Credentials = new NetworkCredential(user, pass);
            request.UsePassive = true;
            request.UseBinary = true;
            request.KeepAlive = false;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream!);
            var list = new List<string>();
            while (!reader.EndOfStream)
                list.Add(reader.ReadLine() ?? "");
            return list;
        }

        private static async Task<DateTime?> GetLastModifiedAsync(string fileUri, string user, string pass)
        {
            var request = (FtpWebRequest)WebRequest.Create(fileUri);
            request.Method = WebRequestMethods.Ftp.GetDateTimestamp;
            request.Credentials = new NetworkCredential(user, pass);
            request.KeepAlive = false;
            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return response.LastModified;
        }

        private static async Task DeleteFileAsync(string fileUri, string user, string pass)
        {
            var request = (FtpWebRequest)WebRequest.Create(fileUri);
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(user, pass);
            request.KeepAlive = false;
            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
        }
    }
}


#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SACTIBACKUP.Infrastructure
{
    
    public static class FtpRotationService
    {
       
        public static async Task ApplyPoliciesAsync(string host, string usuario, string password, int mantenerMinimo, int borrarCadaDias)
        {
            //var baseUri = $"ftp://{host}/{usuario.ToUpperInvariant()}";
            var baseUri = $"ftp://{host}";
            var files = await ListFilesAsync(baseUri, usuario, password); // nombres de archivo en carpeta

            
            files = files.Where(f => !f.StartsWith(".")).ToList();

            
            var groups = files
                .GroupBy(f => BaseName(f)) // e.g., "Alias-InstanciaBD"
                .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f).ToList());

            
            foreach (var kv in groups)
            {
                var list = kv.Value;
                if (list.Count <= mantenerMinimo) continue;

                var toDelete = list.Skip(mantenerMinimo).ToList();
                foreach (var file in toDelete)
                    await DeleteFileAsync($"{baseUri}/{file}", usuario, password);
            }

            
            var cutoff = DateTime.UtcNow.AddDays(-borrarCadaDias);
            foreach (var file in files)
            {
                var lastMod = await GetLastModifiedAsync($"{baseUri}/{file}", usuario, password);
                if (lastMod.HasValue && lastMod.Value.ToUniversalTime() <= cutoff)
                    await DeleteFileAsync($"{baseUri}/{file}", usuario, password);
            }
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
            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return response.LastModified;
        }

        private static async Task DeleteFileAsync(string fileUri, string user, string pass)
        {
            var request = (FtpWebRequest)WebRequest.Create(fileUri);
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(user, pass);
            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
        }
    }
}

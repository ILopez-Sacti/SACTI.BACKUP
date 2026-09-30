
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace SACTIBACKUP.Infrastructure
{
    public static class ConfigManager
    {
        private const string FileName = "SactiBackup.json";

        /// <summary>
        /// Ruta absoluta del archivo de configuración, siempre junto al ejecutable.
        /// La app se lanza por tarea programada (schtasks /sc onlogon) y en ese caso el
        /// CurrentDirectory del proceso es C:\Windows\System32, NO la carpeta del exe;
        /// con path relativo la configuración se leía vacía y el respaldo nunca corría.
        /// </summary>
        public static string FullPath => Path.Combine(AppContext.BaseDirectory, FileName);

        public static JObject Load()
        {
            if (!File.Exists(FullPath))
            {
                BackupLogger.Log("ConfigManager", $"ADVERTENCIA: no existe '{FullPath}'. Se devuelve configuración vacía.");
                return new JObject();
            }

            var base64 = File.ReadAllText(FullPath);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return JObject.Parse(json);
        }

        public static void Save(JObject obj)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            File.WriteAllText(FullPath, base64);
        }
    }
}

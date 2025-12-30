
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace SACTIBACKUP.Infrastructure
{
    public static class ConfigManager
    {
        private const string FileName = "SactiBackup.json";

        public static JObject Load()
        {
            if (!File.Exists(FileName))
                return new JObject();

            var base64 = File.ReadAllText(FileName);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return JObject.Parse(json);
        }

        public static void Save(JObject obj)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            File.WriteAllText(FileName, base64);
        }
    }
}

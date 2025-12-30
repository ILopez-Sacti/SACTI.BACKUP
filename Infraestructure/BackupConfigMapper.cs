
// Infrastructure/BackupConfigMapper.cs
#nullable enable
using System.Linq;
using Newtonsoft.Json.Linq;
using SACTIBACKUP.Domain;

namespace SACTIBACKUP.Infrastructure
{
    public static class BackupConfigMapper
    {
        public static BackupConfig FromJObject(JObject obj)
        {
            return new BackupConfig
            {
                RutaRespaldo = (string?)obj["RutaRespaldo"] ?? "",
                PasswordArchivos = (string?)obj["Password"] ?? "",
                PasswordConfirm = (string?)obj["PasswordConfirm"] ?? "",
                CorreoFTP = (string?)obj["CorreoFTP"] ?? "",
                PasswordFTP = (string?)obj["PasswordFTP"] ?? "",
                HoraRespaldo = (string?)obj["HoraRespaldo"] ?? "00:00:00",
                AliasGlobal = (string?)obj["AliasGlobal"] ?? "",
                CorreoNotificaciones = (string?)obj["CorreoNotificaciones"] ?? "",

                RespaldarSQL = string.Equals((string?)obj["RespaldarSQL"], "SI", System.StringComparison.OrdinalIgnoreCase),
                RespaldarFireBird = string.Equals((string?)obj["RespaldarFireBird"], "SI", System.StringComparison.OrdinalIgnoreCase),
                RespaldarNube = string.Equals((string?)obj["RespaldarNube"], "SI", System.StringComparison.OrdinalIgnoreCase),
                BDComprimidas = string.Equals((string?)obj["BDComprimidas"], "SI", System.StringComparison.OrdinalIgnoreCase),
                UsarRazonSocial = string.Equals((string?)obj["UsarRazonSocial"], "SI", System.StringComparison.OrdinalIgnoreCase),

                ListaRutaFireBird = obj["listaRutaFireBird"] is JArray fr ? fr.Select(x => (string?)x ?? "").ToList() : new(),
                ListaInstanciasSQL = obj["listaInstanciasSQL"] is JArray si ? si.Select(x => (string?)x ?? "").ToList() : new(),

                FechaUltimoRespaldo = (DateTime?)obj["FechaUltimoRespaldo"],
                FechaUltimoRespaldoNube = (DateTime?)obj["FechaUltimoRespaldoNube"],

                NombreEmpresa = (string?)obj["NombreEmpresa"] ?? "",
                BorrarRespaldo = (string?)obj["BorrarRespaldo"] ?? "",
                DiasRespaldo = (string?)obj["DiasRespaldo"] ?? "",
                NumeroRespaldos = (string?)obj["NumeroRespaldos"] ?? "",
                CorreoReceptorCliente = (string?)obj["CorreoReceptorCliente"] ?? "",
                FechaInicioLicencia = (DateTime?)obj["FechaInicioLicencia"],
                FechaFinLicencia = (DateTime?)obj["FechaFinLicencia"],
                LicenciaVencida = (bool?)obj["LicenciaVencida"] ?? false
            };
        }

        public static JObject ToJObject(BackupConfig cfg)
        {
            var obj = new JObject
            {
                ["RutaRespaldo"] = cfg.RutaRespaldo,
                ["Password"] = cfg.PasswordArchivos,
                ["PasswordConfirm"] = cfg.PasswordConfirm,
                ["CorreoFTP"] = cfg.CorreoFTP,
                ["PasswordFTP"] = cfg.PasswordFTP,
                ["HoraRespaldo"] = cfg.HoraRespaldo,
                ["AliasGlobal"] = cfg.AliasGlobal,
                ["CorreoNotificaciones"] = cfg.CorreoNotificaciones,

                ["RespaldarSQL"] = cfg.RespaldarSQL ? "SI" : "NO",
                ["RespaldarFireBird"] = cfg.RespaldarFireBird ? "SI" : "NO",
                ["RespaldarNube"] = cfg.RespaldarNube ? "SI" : "NO",
                ["BDComprimidas"] = cfg.BDComprimidas ? "SI" : "NO",
                ["UsarRazonSocial"] = cfg.UsarRazonSocial ? "SI" : "NO",

                ["listaRutaFireBird"] = JToken.FromObject(cfg.ListaRutaFireBird),
                ["listaInstanciasSQL"] = JToken.FromObject(cfg.ListaInstanciasSQL),

                ["FechaUltimoRespaldo"] = cfg.FechaUltimoRespaldo,
                ["FechaUltimoRespaldoNube"] = cfg.FechaUltimoRespaldoNube,

                ["NombreEmpresa"] = cfg.NombreEmpresa,
                ["BorrarRespaldo"] = cfg.BorrarRespaldo,
                ["DiasRespaldo"] = cfg.DiasRespaldo,
                ["NumeroRespaldos"] = cfg.NumeroRespaldos,
                ["CorreoReceptorCliente"] = cfg.CorreoReceptorCliente,
                ["FechaInicioLicencia"] = cfg.FechaInicioLicencia,
                ["FechaFinLicencia"] = cfg.FechaFinLicencia,
                ["LicenciaVencida"] = cfg.LicenciaVencida
            };
            return obj;
        }
    }
}

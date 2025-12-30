
// BackupSettingsRepository.cs (en SACTIBACKUP.Infrastructure)
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SACTIBACKUP.Entidades;

namespace SACTIBACKUP.Infrastructure;

public static class BackupSettingsRepository
{
    private static string NewPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "SACTIBACKUP", "backupsettings.json");

    private static string LegacyPath => Path.Combine(AppContext.BaseDirectory, "SactiBackup.json");

    public static Backup Load()
    {
        Backup model;

        if (File.Exists(LegacyPath))
        {
            try
            {
                var base64 = File.ReadAllText(LegacyPath, Encoding.UTF8); 
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64)); 
                using var doc = JsonDocument.Parse(json);
                model = FromLegacy(doc);
                
                Save(model);
                return model;
            }
            catch { /* si falla importación, seguimos leyendo new path */ }
        }

        
        if (!File.Exists(NewPath)) return new Backup();
        var text = File.ReadAllText(NewPath, Encoding.UTF8);
        model = JsonSerializer.Deserialize<Backup>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new Backup();

        model.Password = Unprotect(model.Password);
        model.PasswordArchivos = Unprotect(model.PasswordArchivos);

        
        model.ListaRutasFirebird ??= new();
        model.ListaInstanciasSQL ??= new();

        return model;
    }

    public static void Save(Backup model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NewPath)!);

        var copy = new Backup
        {
            Usuario = model.Usuario,
            Password = Protect(model.Password),
            RespaldarSQL = model.RespaldarSQL,
            RespaldarFireBird = model.RespaldarFireBird,
            RutaRespaldo = model.RutaRespaldo,
            PasswordArchivos = Protect(model.PasswordArchivos),
            ConfirmacionPasswordArchivos = model.ConfirmacionPasswordArchivos, 
            AliasGlobal = model.AliasGlobal,
            Correo = model.Correo,
            HoraRespaldo = model.HoraRespaldo,
            RespaldarNube = model.RespaldarNube,
            ListaRutasFirebird = model.ListaRutasFirebird ?? new(),
            ListaInstanciasSQL = model.ListaInstanciasSQL ?? new()
        };

        var json = JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(NewPath, json, Encoding.UTF8);
    }

    private static string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return plain;
        var bytes = Encoding.UTF8.GetBytes(plain);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string? Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return cipher;
        try
        {
            var bytes = Convert.FromBase64String(cipher);
            var unprotected = ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unprotected);
        }
        catch { return null; } 
    }

    
    private static Backup FromLegacy(JsonDocument doc)
    {
        var root = doc.RootElement;
        var m = new Backup
        {
            RutaRespaldo = root.GetPropertyOrDefault("RutaRespaldo"),
            Password = root.GetPropertyOrDefault("Password"),
            PasswordArchivos = root.GetPropertyOrDefault("Password"), 
            ConfirmacionPasswordArchivos = root.GetPropertyOrDefault("PasswordConfirm"),
            AliasGlobal = root.GetPropertyOrDefault("AliasGlobal"),
            Correo = root.GetPropertyOrDefault("CorreoNotificaciones"), 
            HoraRespaldo = root.GetPropertyOrDefault("HoraRespaldo"),
            RespaldarSQL = root.GetPropertyOrDefault("RespaldarSQL").Equals("SI", StringComparison.OrdinalIgnoreCase),
            RespaldarFireBird = root.GetPropertyOrDefault("RespaldarFireBird").Equals("SI", StringComparison.OrdinalIgnoreCase),
            RespaldarNube = root.GetPropertyOrDefault("RespaldarNube").Equals("SI", StringComparison.OrdinalIgnoreCase),
            ListaRutasFirebird = root.TryGetProperty("listaRutaFireBird", out var rf)
                                ? rf.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                                : new(),
            ListaInstanciasSQL = root.TryGetProperty("listaInstanciasSQL", out var si)
                                ? si.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                                : new()
        };
        return m;
    }
}

file static class JsonElementExtensions
{
    public static string GetPropertyOrDefault(this JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
           ? p.GetString() ?? string.Empty
           : string.Empty;
}

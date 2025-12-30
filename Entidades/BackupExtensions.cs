
// BackupExtensions.cs (en SACTIBACKUP.Win o SACTIBACKUP.Domain)
using System;
using System.Collections.Generic;
using System.Linq;

namespace SACTIBACKUP.Entidades;

public sealed class RutaFireBirdBD
{
    public string RutaBaseDatos { get; set; } = string.Empty;
    public string AliasBaseDatos { get; set; } = string.Empty;
}

public sealed class InstanciasSQL
{
    public string Servidor { get; set; } = string.Empty;
    public string Instancia { get; set; } = string.Empty; // "DEFAULT" si vacía
    public string Usuario { get; set; } = string.Empty;
    public string Contrasenha { get; set; } = string.Empty;
}

public static class BackupExtensions
{
    // Firebird: "ruta+alias"
    public static List<RutaFireBirdBD> ToFirebirdList(this List<string>? raw)
        => (raw ?? new()).Select(s => {
            var parts = s.Split('+');
            return new RutaFireBirdBD
            {
                RutaBaseDatos = parts.ElementAtOrDefault(0) ?? string.Empty,
                AliasBaseDatos = parts.ElementAtOrDefault(1) ?? string.Empty
            };
        }).ToList();

    public static List<string> ToRaw(this IEnumerable<RutaFireBirdBD> list)
        => list.Select(x => $"{x.RutaBaseDatos}+{x.AliasBaseDatos}").ToList();

    // SQL: "Servidor+Instancia+Usuario+Contrasenha"
    public static List<InstanciasSQL> ToSqlList(this List<string>? raw)
        => (raw ?? new()).Select(s => {
            var p = s.Split('+');
            return new InstanciasSQL
            {
                Servidor = p.ElementAtOrDefault(0) ?? string.Empty,
                Instancia = p.ElementAtOrDefault(1) ?? "DEFAULT",
                Usuario = p.ElementAtOrDefault(2) ?? string.Empty,
                Contrasenha = p.ElementAtOrDefault(3) ?? string.Empty
            };
        }).ToList();

    public static List<string> ToRaw(this IEnumerable<InstanciasSQL> list)
        => list.Select(x => $"{x.Servidor}+{x.Instancia}+{x.Usuario}+{x.Contrasenha}").ToList();
}

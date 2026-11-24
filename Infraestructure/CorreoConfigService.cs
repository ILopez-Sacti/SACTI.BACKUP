#nullable enable
using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SACTIBACKUP.Infrastructure
{
    /// <summary>Datos SMTP del emisor. Viven en la BD SACTIBACKUP (no en el exe).</summary>
    public sealed class CorreoConfig
    {
        public string Correo { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Host { get; init; } = "mail.sacti.mx";
        public int Puerto { get; init; } = 587;
        public bool EnableSsl { get; init; } = true;
    }

    /// <summary>
    /// Lee la configuracion del correo emisor desde la tabla PARAMETROSCORREO de la BD SACTIBACKUP.
    /// Antes estas credenciales estaban hardcodeadas en el exe; ahora se sirven desde el servidor
    /// para poder rotarlas sin recompilar y no distribuirlas en el instalador.
    /// </summary>
    public static class CorreoConfigService
    {
        public static string? LastError { get; private set; }

        public static async Task<CorreoConfig?> ObtenerAsync(CancellationToken ct = default)
        {
            LastError = null;

            var cs = new SqlConnectionStringBuilder
            {
                DataSource = @"sacti.ddns.net\sacti,2014",
                InitialCatalog = "SACTIBACKUP",
                UserID = "sactibackup_cli",
                Password = "hsGgqWwO1ggK3XKe8QqQs1vy",
                Encrypt = false,
                TrustServerCertificate = true,
                ConnectTimeout = 10
            };

            const string sql = @"SELECT TOP 1 Correo, Password, Host, Puerto, EnableSsl
                                 FROM PARAMETROSCORREO ORDER BY Id";

            try
            {
                await using var con = new SqlConnection(cs.ConnectionString);
                await con.OpenAsync(ct).ConfigureAwait(false);
                await using var cmd = new SqlCommand(sql, con);
                await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

                if (!await rd.ReadAsync(ct).ConfigureAwait(false))
                {
                    LastError = "PARAMETROSCORREO sin registros";
                    return null;
                }

                return new CorreoConfig
                {
                    Correo = rd.IsDBNull(0) ? string.Empty : rd.GetString(0),
                    Password = rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                    Host = rd.IsDBNull(2) ? "mail.sacti.mx" : rd.GetString(2),
                    Puerto = rd.IsDBNull(3) ? 587 : rd.GetInt32(3),
                    EnableSsl = rd.IsDBNull(4) ? true : rd.GetBoolean(4),
                };
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                BackupLogger.Log("CorreoConfigService", $"No se pudo leer PARAMETROSCORREO: {ex.Message}");
                return null;
            }
        }
    }
}

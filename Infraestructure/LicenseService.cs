


using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SACTIBACKUP.Infrastructure
{
    
    public class LicenseResult
    {
        public string? Usuario { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public int RespaldarCada { get; init; }
        public int BorrarCada { get; init; }
        public int MantenerMinimo { get; init; }
        public string CorreoNotificacion { get; init; } = string.Empty;
        public DateTime? FechaInicio { get; init; }
        public DateTime? FechaFin { get; init; }
        public string NombreEmpresa { get; init; } = string.Empty;
        public DateTime? FechaUltimoRespaldoNube { get; init; }

        public bool LicenciaVencida =>
            FechaFin.HasValue && FechaFin.Value.Date < DateTime.Now.Date;
    }

    public static class LicenseService
    {
        // Almacena el último error para diagnóstico
        public static string? LastError { get; private set; }

        public static async Task<LicenseResult?> ObtenerPorUsuarioAsync(string usuarioFtp, CancellationToken ct = default)
        {
            LastError = null;

            var cs = new SqlConnectionStringBuilder
            {
                DataSource = @"sacti.ddns.net\sacti,2014",
                InitialCatalog = "SACTIBACKUP",
                UserID = "sa",
                Password = "MasterkeySac123",
                Encrypt = false,
                TrustServerCertificate = true,
                ConnectTimeout = 10
            };

            const string sql = @"
                SELECT A.USUARIO, A.PASSWORD, A.RESPALDARCADA, A.BORRARCADA, A.MANTENERMINIMO, A.CORREONOTIFICACION,
                       A.FECHAINICIO, A.FECHAFIN, A.NOMBREEMPRESA, FECHAULTIMORESPALDONUBE
                FROM CONFIGURACIONES A
                WHERE A.USUARIO = @USUARIO";

            try
            {
                System.Diagnostics.Debug.WriteLine($"LicenseService: Conectando a {cs.DataSource} para usuario '{usuarioFtp}'");

                await using var con = new SqlConnection(cs.ConnectionString);
                await con.OpenAsync(ct).ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine("LicenseService: Conexión exitosa, ejecutando consulta...");

                await using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@USUARIO", usuarioFtp);

                await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await rd.ReadAsync(ct).ConfigureAwait(false))
                {
                    LastError = $"No se encontró licencia para el usuario '{usuarioFtp}'";
                    System.Diagnostics.Debug.WriteLine($"LicenseService: {LastError}");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine("LicenseService: Registro encontrado, leyendo datos...");

                var res = new LicenseResult
                {
                    Usuario = rd.IsDBNull(0) ? string.Empty : rd.GetString(0),
                    Password = rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                    RespaldarCada = rd.IsDBNull(2) ? 1 : rd.GetInt32(2),
                    BorrarCada = rd.IsDBNull(3) ? 30 : rd.GetInt32(3),
                    MantenerMinimo = rd.IsDBNull(4) ? 3 : rd.GetInt32(4),
                    CorreoNotificacion = rd.IsDBNull(5) ? string.Empty : rd.GetString(5),
                    FechaInicio = rd.IsDBNull(6) ? null : rd.GetDateTime(6),
                    FechaFin = rd.IsDBNull(7) ? null : rd.GetDateTime(7),
                    NombreEmpresa = rd.IsDBNull(8) ? string.Empty : rd.GetString(8),
                    FechaUltimoRespaldoNube = rd.IsDBNull(9) ? null : rd.GetDateTime(9),
                };

                System.Diagnostics.Debug.WriteLine($"LicenseService: Licencia cargada para '{res.NombreEmpresa}', vence: {res.FechaFin}");
                return res;
            }
            catch (SqlException sqlEx)
            {
                LastError = $"Error SQL ({sqlEx.Number}): {sqlEx.Message}";
                System.Diagnostics.Debug.WriteLine($"LicenseService: {LastError}");
                return null;
            }
            catch (Exception ex)
            {
                LastError = $"Error de conexión: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"LicenseService: {LastError}");
                return null;
            }
        }
    }
}

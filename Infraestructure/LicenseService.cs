


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
        
        public static async Task<LicenseResult?> ObtenerPorUsuarioAsync(string usuarioFtp, CancellationToken ct = default)
        {
           
            var cs = new SqlConnectionStringBuilder
            {
                DataSource = @"sacti.ddns.net\sacti,2014",
                InitialCatalog = "SACTIBACKUP",
                UserID = "sa",
                Password = "MasterkeySac123",
                Encrypt = false,
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };

            const string sql = @"
                SELECT A.USUARIO, A.PASSWORD, A.RESPALDARCADA, A.BORRARCADA, A.MANTENERMINIMO, A.CORREONOTIFICACION,
                       A.FECHAINICIO, A.FECHAFIN, A.NOMBREEMPRESA, FECHAULTIMORESPALDONUBE
                FROM CONFIGURACIONES A
                WHERE A.USUARIO = @USUARIO";

            try
            {
                await using var con = new SqlConnection(cs.ConnectionString);
                await con.OpenAsync(ct).ConfigureAwait(false);

                await using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@USUARIO", usuarioFtp);

                await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await rd.ReadAsync(ct).ConfigureAwait(false)) return null;

                int i = 0;
                


                var res = new LicenseResult
                {
                    Usuario = rd.IsDBNull(i) ? string.Empty : rd.GetString(0),
                    Password = rd.IsDBNull(i) ? string.Empty : rd.GetString(1),
                    RespaldarCada = rd.IsDBNull(i) ? 0 : rd.GetInt32(2),
                    BorrarCada = rd.IsDBNull(i) ? 0 : rd.GetInt32(3),
                    MantenerMinimo = rd.IsDBNull(i) ? 0 : rd.GetInt32(4),
                    CorreoNotificacion = rd.IsDBNull(i) ? string.Empty : rd.GetString(5),
                    FechaInicio = rd.IsDBNull(i) ? null : rd.GetDateTime(6),
                    FechaFin = rd.IsDBNull(i) ? null : rd.GetDateTime(7),
                    NombreEmpresa = rd.IsDBNull(i) ? string.Empty : rd.GetString(8),
                    FechaUltimoRespaldoNube = rd.IsDBNull(i) ? null : rd.GetDateTime(9),
                };
                return res;

            }
            catch
            {
                
                return null;
            }
        }
    }
}

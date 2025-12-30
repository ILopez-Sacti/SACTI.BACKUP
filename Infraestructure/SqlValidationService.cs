

using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SACTIBACKUP.Infrastructure;

public static class SqlValidationService
{
    public static async Task<bool> ValidarCredencialesSqlAsync(
        string servidor, string instancia, string usuario, string contrasenha, CancellationToken ct = default)
    {
        var dataSource = string.IsNullOrWhiteSpace(instancia) ? servidor : $"{servidor}\\{instancia}";

        var cs = new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = "master",
            UserID = usuario,
            Password = contrasenha,
            Encrypt = false,             
            TrustServerCertificate = true, 
            ConnectTimeout = 5
        };

        try
        {
            using var con = new SqlConnection(cs.ConnectionString);
            await con.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }
}

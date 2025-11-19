// Data/SqlConnectionFactory.cs
using Microsoft.Data.SqlClient;

namespace TonyTI_Web.Data
{
    // Retornaremos SqlConnection concreto para facilitar uso de APIs assíncronas.
    public interface ISqlConnectionFactory
    {
        SqlConnection CreateConnection();
    }

    public class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;
        public SqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}

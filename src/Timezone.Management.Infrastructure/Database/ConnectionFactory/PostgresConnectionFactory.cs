using System.Data;
using Npgsql;
using Timezone.Management.Infrastructure.Database.Contracts;

namespace Timezone.Management.Infrastructure.Database.ConnectionFactory;

public class PostgresConnectionFactory(string connectionString) : IDbConnectionFactory
{
	public IDbConnection CreateConnection() => new NpgsqlConnection(connectionString);
}
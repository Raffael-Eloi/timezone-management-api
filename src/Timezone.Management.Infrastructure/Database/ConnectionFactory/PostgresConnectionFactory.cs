using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Timezone.Management.Infrastructure.Database.Contracts;

namespace Timezone.Management.Infrastructure.Database.ConnectionFactory;

public class PostgresConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
	private readonly string _connectionString = configuration.GetConnectionString("Postgres")!;

	public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
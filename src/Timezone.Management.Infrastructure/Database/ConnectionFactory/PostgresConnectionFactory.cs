using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Timezone.Management.Infrastructure.Database.Contracts;

namespace Timezone.Management.Infrastructure.Database.ConnectionFactory;

public class PostgresConnectionFactory : IDbConnectionFactory
{
	private readonly string _connectionString;
#pragma warning disable IDE0021 // Use expression body for constructor
#pragma warning disable IDE0290 // Use primary constructor
	public PostgresConnectionFactory(IConfiguration configuration)
#pragma warning restore IDE0290 // Use primary constructor
	{
		_connectionString = configuration.GetConnectionString("Postgres")!;
	}
#pragma warning restore IDE0021 // Use expression body for constructor

	public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
using DbUp;
using DbUp.Engine;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Timezone.Management.Infrastructure.Database.ConnectionFactory;
using Timezone.Management.Infrastructure.Database.Contracts;

namespace Timezone.Management.IoC;

public static class DatabaseExtensions
{
	extension(IServiceCollection services)
	{
		public void AddDbConfig() =>
			services.AddSingleton<IDbConnectionFactory>(sp =>
			{
				string? connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres");

				if (connectionString is null)
					throw new ArgumentNullException(nameof(connectionString), "Postgres connection string is not configured. Please check your configuration settings.");

				return new PostgresConnectionFactory(connectionString);
			});
	}

	extension(WebApplication app)
	{
		public void RunDbMigrations()
		{
			string? connectionString = app.Configuration.GetConnectionString("Postgres");

			if (connectionString is null)
				throw new ArgumentNullException(nameof(connectionString), "Postgres connection string is not configured. Please check your configuration settings.");

			UpgradeEngine upgrader = DeployChanges.To
				.PostgresqlDatabase(connectionString)
				.WithScriptsEmbeddedInAssembly(typeof(PostgresConnectionFactory).Assembly)
				.LogToConsole()
				.Build();

			DatabaseUpgradeResult result = upgrader.PerformUpgrade();

			if (!result.Successful)
				throw new Exception("Database upgrade failed. See the error details for more information", result.Error);
		}
	}
}

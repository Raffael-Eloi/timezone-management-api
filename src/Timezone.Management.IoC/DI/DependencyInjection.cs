using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.UseCases;
using Timezone.Management.Application.Validators;
using Timezone.Management.Infrastructure.Database.ConnectionFactory;
using Timezone.Management.Infrastructure.Database.Contracts;
using Timezone.Management.Infrastructure.Repositories;

namespace Timezone.Management.IoC.DI;

public static class DependencyInjection
{
	public static void AddAzureAppConfig(this IConfigurationManager configurationManager, IConfiguration configuration)
	{
		string? azureAppConfig = configuration.GetConnectionString("AzureAppConfig");

		if (azureAppConfig is null)
			throw new ArgumentNullException(nameof(azureAppConfig), "Azure AppConfig is not configured. Please check your configuration settings.");

		configurationManager.AddAzureAppConfiguration(options => {
			options.Connect(azureAppConfig);
		});
	}

	public static void AddDBConfig(this IServiceCollection services, IConfiguration configuration)
	{
		string? connectionString = configuration["DatabaseConnectionString"];

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


		services.AddSingleton<IDbConnectionFactory, PostgresConnectionFactory>();
	}
	

	public static void InjectServices(this IServiceCollection services)
	{
		services.AddScoped<IUserValidator, UserValidator>();
		services.AddScoped<IUserUseCase, UserUseCase>();
		services.AddScoped<IUserRepository, UserRepository>();
	}
	
}
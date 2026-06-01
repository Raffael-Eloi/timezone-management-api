using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Timezone.Management.Integration.Tests;

public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureAppConfiguration((_, config) =>
			config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Postgres"] = connectionString
			}));

		builder.ConfigureTestServices(services =>
			services.AddTransient<IStartupFilter, DetailedExceptionStartupFilter>());
	}

	public HttpClient CreateLoggingClient() =>
		CreateDefaultClient(new ErrorBodyLoggingHandler());
}

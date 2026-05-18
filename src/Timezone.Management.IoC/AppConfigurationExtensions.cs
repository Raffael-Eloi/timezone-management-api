using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Timezone.Management.IoC;

public static class AppConfigurationExtensions
{
	extension(IConfigurationManager configurationManager)
	{
		public void AddAzureAppConfig(IConfiguration configuration)
		{
			string? endpoint = configuration["AZURE_APPCONFIGURATION_ENDPOINT"];

			if (endpoint is null)
				return;

			DefaultAzureCredential credential = new();

			configurationManager.AddAzureAppConfiguration(options =>
			{
				options
					.Connect(new Uri(endpoint), credential)
					.ConfigureKeyVault(kv => kv.SetCredential(credential));
			});
		}
	}
}

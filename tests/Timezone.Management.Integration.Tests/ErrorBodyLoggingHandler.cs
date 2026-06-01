using System.Text;
using NUnit.Framework;

namespace Timezone.Management.Integration.Tests;

public sealed class ErrorBodyLoggingHandler : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

		if ((int)response.StatusCode >= 400)
		{
			string body = await response.Content.ReadAsStringAsync(cancellationToken);
			TestContext.Out.WriteLine($"[{(int)response.StatusCode}] {request.Method} {request.RequestUri}");
			TestContext.Out.WriteLine(body);
			response.Content = new StringContent(body, Encoding.UTF8, "application/json");
		}

		return response;
	}
}

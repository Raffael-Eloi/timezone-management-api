using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using Timezone.Management.API.Endpoints;
using Timezone.Management.IoC;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Configuration.AddAzureAppConfig(builder.Configuration);

builder.Services.AddDbConfig();

builder.Services.InjectServices();

WebApplication app = builder.Build();

app.RunDbMigrations();

app.MapOpenApi();
app.MapScalarApiReference();

// Azure Container Apps terminates TLS at the ingress; port 8080 is not directly internet-accessible, so trusting X-Forwarded-Proto from any source is safe.
ForwardedHeadersOptions forwardedOptions = new()
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.UseHttpsRedirection();

UserEndpoints.Map(app);

app.Run();

public partial class Program { }
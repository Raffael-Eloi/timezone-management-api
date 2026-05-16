using Scalar.AspNetCore;
using Timezone.Management.API.Endpoints;
using Timezone.Management.IoC.DI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Configuration.AddAzureAppConfig(builder.Configuration);

builder.Services.AddDBConfig(builder.Configuration);

builder.Services.InjectServices();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

UserEndpoints.Map(app);

app.Run();
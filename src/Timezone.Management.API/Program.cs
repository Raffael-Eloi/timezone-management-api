using Timezone.Management.API.Endpoints;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.UseCases;
using Timezone.Management.Application.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddScoped<IUserValidator, UserValidator>();
builder.Services.AddScoped<IUserUseCase, UserUseCase>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

UserEndpoints.Map(app);

app.Run();

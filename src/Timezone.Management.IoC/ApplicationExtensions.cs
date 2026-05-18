using Microsoft.Extensions.DependencyInjection;
using Timezone.Management.Application.Contracts.Repositories;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Contracts.Validators;
using Timezone.Management.Application.UseCases;
using Timezone.Management.Application.Validators;
using Timezone.Management.Infrastructure.Repositories;

namespace Timezone.Management.IoC;

public static class ApplicationExtensions
{
	extension(IServiceCollection services)
	{
		public void InjectServices()
		{
			services.AddScoped<IUserValidator, UserValidator>();
			services.AddScoped<IUserUseCase, UserUseCase>();
			services.AddScoped<IUserRepository, UserRepository>();
		}
	}
}

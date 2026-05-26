using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Timezone.Management.API.Endpoints;
using Timezone.Management.Application.UseCases;
using Timezone.Management.Infrastructure.Repositories;
using Timezone.Management.IoC;

namespace Timezone.Management.Architecture.Tests;

internal class ApplicationLayerTests
{
	private Assembly applicationAssembly;

	[SetUp]
	public void Setup() => applicationAssembly = typeof(UserUseCase).Assembly;

	[Test]
	public void ApplicationLayer_ShouldNotHaveDependencyOnInfrastructureLayer()
	{
		// Arrange
		string? infrastructureNamespace = typeof(UserRepository).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly(applicationAssembly)
			.Should()
			.NotHaveDependencyOn(infrastructureNamespace)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}

	[Test]
	public void ApplicationLayer_ShouldNotHaveDependencyOnAPILayer()
	{
		// Arrange
		string? apiNamespace = typeof(UserEndpoints).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly(applicationAssembly)
			.Should()
			.NotHaveDependencyOn(apiNamespace)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}

	[Test]
	public void ApplicationLayer_ShouldNotHaveDependencyOnIoCLayer()
	{
		// Arrange
		string? iocNamespace = typeof(AppConfigurationExtensions).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly(applicationAssembly)
			.Should()
			.NotHaveDependencyOn(iocNamespace)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}
}

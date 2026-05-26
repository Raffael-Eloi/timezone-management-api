using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Timezone.Management.API.Endpoints;
using Timezone.Management.Application.UseCases;
using Timezone.Management.Infrastructure.Repositories;
using Timezone.Management.IoC;

namespace Timezone.Management.Architecture.Tests;

internal class InfrastructureLayerTests
{
	private Assembly infrastructureAssembly;

	[SetUp]
	public void Setup() => infrastructureAssembly = typeof(UserRepository).Assembly;

	[Test]
	public void InfrastructureLayer_ShouldNotHaveDependencyOnApplicationLayer()
	{
		// Arrange
		string? applicationNamespace = typeof(UserUseCase).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly(infrastructureAssembly)
			.Should()
			.NotHaveDependencyOn(applicationNamespace)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}

	[Test]
	public void InfrastructureLayer_ShouldNotHaveDependencyOnAPILayer()
	{
		// Arrange
		string? apiNamespace = typeof(UserEndpoints).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly(infrastructureAssembly)
			.Should()
			.NotHaveDependencyOn(apiNamespace)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}

	[Test]
	public void InfrastructureLayer_ShouldNotHaveDependencyOnIoCLayer()
	{
		// Arrange
		string? iocNamespace = typeof(AppConfigurationExtensions).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly(infrastructureAssembly)
			.Should()
			.NotHaveDependencyOn(iocNamespace)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}
}

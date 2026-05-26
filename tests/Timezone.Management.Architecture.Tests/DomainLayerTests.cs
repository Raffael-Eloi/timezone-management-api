using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Timezone.Management.API.Endpoints;
using Timezone.Management.Application.UseCases;
using Timezone.Management.Domain.Entities;
using Timezone.Management.Infrastructure.Repositories;
using Timezone.Management.IoC;

namespace Timezone.Management.Architecture.Tests;

internal class DomainLayerTests
{
	private Assembly domainAssembly;

	[SetUp]
	public void Setup() => domainAssembly = typeof(User).Assembly;

	[Test]
	public void DomainLayer_ShouldNotHaveDependencyOnApplicationLayer()
	{
		// Arrange
		string? applicationNamespace = typeof(UserUseCase).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly((domainAssembly))
			.Should()
			.NotHaveDependencyOn(applicationNamespace)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}
	
	[Test]
	public void DomainLayer_ShouldNotHaveDependencyOnInfrastructureLayer()
	{
		// Arrange
		string? infrastructureNamespace = typeof(UserRepository).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly((domainAssembly))
			.Should()
			.NotHaveDependencyOn(infrastructureNamespace)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}
	
	[Test]
	public void DomainLayer_ShouldNotHaveDependencyOnAPILayer()
	{
		// Arrange
		string? apiLayer = typeof(UserEndpoints).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly((domainAssembly))
			.Should()
			.NotHaveDependencyOn(apiLayer)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}
	
	[Test]
	public void DomainLayer_ShouldNotHaveDependencyOnIoCLayer()
	{
		// Arrange
		string? iocLayer = typeof(AppConfigurationExtensions).Assembly.GetName().Name;

		// Act
		TestResult result = Types.InAssembly((domainAssembly))
			.Should()
			.NotHaveDependencyOn(iocLayer)
			.GetResult();

		// Assert
		result.IsSuccessful.Should().BeTrue();
	}
}
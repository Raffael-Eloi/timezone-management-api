using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Testcontainers.PostgreSql;
using Timezone.Management.Application.Models;

namespace Timezone.Management.Integration.Tests;

[TestFixture]
public class UserTests
{
	private PostgreSqlContainer _postgresContainer = null!;
	private ApiFactory _factory = null!;
	private HttpClient _client = null!;

	[OneTimeSetUp]
	public async Task OneTimeSetUp()
	{
		_postgresContainer = new PostgreSqlBuilder("postgres:17-alpine").Build();
		await _postgresContainer.StartAsync();
		_factory = new ApiFactory(_postgresContainer.GetConnectionString());
		_client = _factory.CreateLoggingClient();
	}

	[OneTimeTearDown]
	public async Task OneTimeTearDown()
	{
		_client.Dispose();
		await _factory.DisposeAsync();
		await _postgresContainer.DisposeAsync();
	}

	[Test]
	public async Task GivenValidUser_WhenCreate_ThenShouldReturn201()
	{
		// Arrange
		AddOrUpdateUserModel request = new()
		{
			Name = "Alice",
			Email = $"{Guid.NewGuid()}@test.com"
		};

		// Act
		HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1.0/users", request);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		AddUserResponse? addUserResponse = await response.Content.ReadFromJsonAsync<AddUserResponse>();
		addUserResponse!.IsValid.Should().BeTrue();
		addUserResponse!.UserUid.Should().NotBeNull();
	}

	[Test]
	public async Task GivenInvalidUser_WhenCreate_ThenShouldReturn400()
	{
		// Arrange
		AddOrUpdateUserModel request = new()
		{
			Name = "Ab",
			Email = $"{Guid.NewGuid()}@test.com"
		};

		// Act
		HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1.0/users", request);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Test]
	public async Task GivenExistingUser_WhenGetByUid_ThenShouldReturn200()
	{
		// Arrange
		Guid uid = await CreateUserAsync("Bob");

		// Act
		HttpResponseMessage response = await _client.GetAsync($"/api/v1.0/users/{uid}");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		UserModel? userResponse = await response.Content.ReadFromJsonAsync<UserModel>();
		userResponse!.Uid.Should().Be(uid);
		userResponse.Name.Should().Be("Bob");
	}

	[Test]
	public async Task GivenNonExistentUid_WhenGetByUid_ThenShouldReturn404()
	{
		// Act
		HttpResponseMessage response = await _client.GetAsync($"/api/v1.0/users/{Guid.NewGuid()}");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Test]
	public async Task GivenExistingUser_WhenUpdate_ThenShouldReturn204()
	{
		// Arrange
		Guid uid = await CreateUserAsync("Charlie");
		AddOrUpdateUserModel request = new()
		{
			Name = "Charlie Updated",
			Email = $"{Guid.NewGuid()}@test.com"
		};

		// Act
		HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/v1.0/users/{uid}", request);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	[Test]
	public async Task GivenExistingUser_WhenDelete_ThenShouldReturn204()
	{
		// Arrange
		Guid uid = await CreateUserAsync("Diana");

		// Act
		HttpResponseMessage response = await _client.DeleteAsync($"/api/v1.0/users/{uid}");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	[Test]
	public async Task GivenNonExistentUid_WhenDelete_ThenShouldReturn404()
	{
		// Act
		HttpResponseMessage response = await _client.DeleteAsync($"/api/v1.0/users/{Guid.NewGuid()}");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	private async Task<Guid> CreateUserAsync(string name)
	{
		AddOrUpdateUserModel request = new()
		{
			Name = name,
			Email = $"{Guid.NewGuid()}@test.com"
		};

		HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1.0/users", request);
		response.EnsureSuccessStatusCode();
		AddUserResponse body = (await response.Content.ReadFromJsonAsync<AddUserResponse>())!;
		return body.UserUid!.Value;
	}
}

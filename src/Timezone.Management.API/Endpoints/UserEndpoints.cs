using FluentValidation.Results;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Entities;
using Timezone.Management.Application.Models;

namespace Timezone.Management.API.Endpoints;

public sealed class UserEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1.0/users", async (
            IUserUseCase userUseCase,
            User user) =>
        {
            AddUserResponse response = await userUseCase.AddUser(user);

            if (!response.IsValid)
                return Results.BadRequest(response.Errors);

            return Results.Created($"/api/v1.0/users/{response.UserUid}", response);
        })
            .Produces<AddUserResponse>(StatusCodes.Status201Created)
            .Produces<List<ValidationFailure>>(StatusCodes.Status400BadRequest)
            .WithDescription("Create a new user")
            .WithSummary("Add User")
            .WithTags("Users");

        app.MapGet("/api/v1.0/users/{userUid:guid}", async (
            IUserUseCase userUseCase,
            Guid userUid) =>
        {
            User? user = await userUseCase.GetUserByUid(userUid);

            if (user is null)
                return Results.NotFound();

            return Results.Ok(user);
        })
            .Produces<User>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithDescription("Get a single user by UID")
            .WithSummary("Get User")
            .WithTags("Users");

        app.MapPut("/api/v1.0/users/{userUid:guid}", async (
            IUserUseCase userUseCase,
            Guid userUid,
            User user) =>
        {
            UpdateOrDeleteUserResponse response = await userUseCase.UpdateUser(userUid, user);

            if (!response.IsValid)
                return Results.BadRequest(response.Errors);

            return Results.NoContent();
        })
            .Produces(StatusCodes.Status204NoContent)
            .Produces<List<ValidationFailure>>(StatusCodes.Status400BadRequest)
            .WithDescription("Update an existing user")
            .WithSummary("Update User")
            .WithTags("Users");

        app.MapDelete("/api/v1.0/users/{userUid:guid}", async (
            IUserUseCase userUseCase,
            Guid userUid) =>
        {
            UpdateOrDeleteUserResponse response = await userUseCase.DeleteUser(userUid);

            if (!response.IsValid)
                return Results.NotFound(response.Errors);

            return Results.NoContent();
        })
            .Produces(StatusCodes.Status204NoContent)
            .Produces<List<ValidationFailure>>(StatusCodes.Status404NotFound)
            .WithDescription("Delete an existing user")
            .WithSummary("Delete User")
            .WithTags("Users");
    }
}
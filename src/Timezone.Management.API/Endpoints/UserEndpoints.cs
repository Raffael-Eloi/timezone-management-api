using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Timezone.Management.Application.Contracts.UseCases;
using Timezone.Management.Application.Models;
using Timezone.Management.Domain.Models;

namespace Timezone.Management.API.Endpoints;

public sealed class UserEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1.0/users", async (
            IUserUseCase userUseCase,
            AddOrUpdateUserModel userRequest) =>
        {
            AddUserResponse response = await userUseCase.AddUser(userRequest);

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
            UserModel? user = await userUseCase.GetUserByUid(userUid);

            if (user is null)
                return Results.NotFound();

            return Results.Ok(user);
        })
            .Produces<UserModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithDescription("Get a single user by UID")
            .WithSummary("Get User")
            .WithTags("Users");
        
        app.MapGet("/api/v1.0/users", async (
		    [AsParameters] UsersFilter filter,
            IUserUseCase userUseCase) =>
        {
            IEnumerable<UserModel> users = await userUseCase.GetUsers(filter);
            return Results.Ok(users);
        })
            .Produces<IEnumerable<UserModel>>(StatusCodes.Status200OK)
            .WithDescription("Get filtered users")
            .WithSummary("Get Users")
            .WithTags("Users");

        app.MapPut("/api/v1.0/users/{userUid:guid}", async (
            IUserUseCase userUseCase,
            Guid userUid,
            AddOrUpdateUserModel userRequest) =>
        {
            UpdateOrDeleteUserResponse response = await userUseCase.UpdateUser(userUid, userRequest);

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
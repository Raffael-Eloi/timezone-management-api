# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Rules

Always read and follow `.claude/rules/behavior.md` before responding to any request.

## Commands

```bash
# Build the solution
dotnet build

# Run the API
dotnet run --project src/Timezone.Management.API

# Run all tests
dotnet test tests/Timezone.Management.Application.Tests

# Run a single test class or method
dotnet test tests/Timezone.Management.Application.Tests --filter "FullyQualifiedName~UserUseCaseShould"
dotnet test tests/Timezone.Management.Application.Tests --filter "FullyQualifiedName~GivenUser_WhenCreate_ThenTheUserShouldBeCreated"
```

The API exposes Scalar UI at `/scalar/v1` in development.

## Architecture

The solution follows clean architecture with four layers:

- **`Timezone.Management.API`** — ASP.NET Core Minimal API host. Endpoints live in `Endpoints/` and are registered by calling `Map(app)` on a static class. No business logic here.
- **`Timezone.Management.Application`** — Business logic, entities, validators, use case interfaces, and repository interfaces (contracts). Has no dependency on infrastructure.
- **`Timezone.Management.Infrastructure`** — PostgreSQL access via Dapper. Implements the repository contracts. Database migrations are SQL scripts in `Database/Scripts/` named `NNNN_description.sql` and embedded as assembly resources.
- **`Timezone.Management.IoC`** — Dependency injection wiring only. Contains `DependencyInjection.cs` with two extension methods: `AddDBConfig` (runs DbUp migrations on startup + registers `IDbConnectionFactory`) and `InjectServices` (registers use cases, validators, repositories). Both are called from `Program.cs`.

**Data flow:** Endpoint → `IUserUseCase` → `IUserValidator` (FluentValidation) → `IUserRepository` → Dapper/PostgreSQL.

**Database migrations** are run automatically on startup via DbUp. Add new scripts to `src/Timezone.Management.Infrastructure/Database/Scripts/` following the `NNNN_name.sql` naming convention. They must be set as embedded resources in the Infrastructure project.

**Connection string** is configured under `ConnectionStrings:Postgres` in `appsettings.json`. For local dev, use user secrets to avoid committing credentials.

## Code Style

The `.editorconfig` enforces these rules as warnings/errors during build:

- **File-scoped namespaces** — always use `namespace Foo.Bar;` not the block form.
- **No `var`** — always use explicit types everywhere.
- **Expression-bodied members** — prefer `=>` for methods, properties, accessors, constructors, and local functions.
- **Primary constructors** — preferred over field injection (`IDE0290`).
- **Tab indentation**, Allman braces (opening brace on new line for types, methods, control blocks).
- **No `this.`** qualification for fields, methods, or events.
- **`System.*` usings first**, sorted alphabetically.

## Testing Conventions

Tests use NUnit + Moq + FluentAssertions. Only the Application layer is unit-tested; infrastructure is not mocked at the database level — all application tests mock `IUserRepository` and `IUserValidator`.

Test class naming: `<Subject>Should` (e.g., `UserUseCaseShould`).  
Test method naming: `Given<context>_When<action>_Then<expectation>`.

Each test class uses `[SetUp]` to initialize mocks and the subject under test. The validator mock defaults to returning a valid `ValidationResult` so individual tests only need to override it when testing the invalid path.

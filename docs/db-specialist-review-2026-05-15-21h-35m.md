# Database & Infrastructure Specialist Review
**Date:** 2026-05-15  
**Reviewer:** Senior DB & Infrastructure Specialist (AI)  
**Stack:** PostgreSQL · Dapper · DbUp · ASP.NET Core

---

## Executive Summary

The infrastructure layer is a functional early-stage implementation that correctly uses DbUp for migration management, Dapper for lightweight data access, and Npgsql for PostgreSQL connectivity. However, three critical issues demand immediate attention before this system handles any real workload or reaches production: (1) a hard-coded database password committed to `appsettings.json` in version control represents a direct security breach; (2) the `GetUserByUid` repository method contains a broken SQL query that ignores its parameter entirely and would return the same row for every call; and (3) the DDL uses PostgreSQL-invalid syntax (`NONCLUSTERED INDEX`, `SERIAL` with a redundant `uid UUID` column), a split identity model that has no write path for `uid`, and missing audit-column defaults that make the table uninsertable as designed. The rest of the findings are High or lower but collectively indicate the project needs a focused hardening pass before it is production-ready.

---

## Findings

---

### [Security] Plaintext Credentials Committed to Version Control — Critical

**File:** `src/Timezone.Management.API/appsettings.json` (line 10)  
**Issue:** The Postgres connection string `postgresql://postgres:05102025@localhost:5432` contains the database password in plaintext and is committed to the git repository.  
**Why it matters:** Any person with read access to the repository (including future CI runners, GitHub forks, or a leaked git bundle) obtains full database credentials. The password cannot be rotated without also updating the committed file, which leaves the old value in git history.  
**Recommendation:** Remove the connection string from `appsettings.json` immediately. Use .NET User Secrets for local development (the `UserSecretsId` is already configured in the API `.csproj`) and environment variables or a secrets manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) in deployed environments. The `appsettings.json` entry should be replaced with a placeholder or removed entirely:

```json
"ConnectionStrings": {
  "Postgres": ""
}
```

Then set the real value via:
```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Database=timezone;Username=appuser;Password=..."
```

---

### [PostgreSQL Schema] Invalid DDL Syntax Will Fail on PostgreSQL — Critical

**File:** `src/Timezone.Management.Infrastructure/Database/Scripts/0001_create_users_table.sql` (lines 12–13)  
**Issue:** The index creation statements use `CREATE NONCLUSTERED INDEX` and uppercase unquoted column names (`UID`, `EMAIL`). `NONCLUSTERED` is a SQL Server keyword; PostgreSQL does not recognise it and will throw a syntax error. All indexes in PostgreSQL are non-clustered by default; `CREATE INDEX` is the correct syntax.  
**Why it matters:** The migration script will fail at runtime the moment DbUp attempts to run it, preventing the application from starting entirely. The `IF NOT EXISTS` guard on the `CREATE TABLE` will succeed, but the index statements will abort with `ERROR: syntax error at or near "NONCLUSTERED"`.  
**Recommendation:**

```sql
CREATE INDEX IF NOT EXISTS ix_users_uid   ON users (uid);
CREATE INDEX IF NOT EXISTS ix_users_email ON users (email);
```

Note: the `UNIQUE` constraint on `email` already creates an implicit B-tree index, so `ix_users_email` is redundant and can be omitted (see the separate finding on over-indexing).

---

### [Repository] Broken `GetUserByUid` Query Returns Wrong Row — Critical

**File:** `src/Timezone.Management.Infrastructure/Repositories/UserRepository.cs` (line 28)  
**Issue:** The query `SELECT * FROM users WHERE id = id` compares the column `id` with itself — a tautology that always evaluates to true — rather than filtering by the provided `userUid` parameter. Every call returns the first row Dapper happens to materialise (or null if the table is empty), regardless of which UID was requested.  
**Why it matters:** The `GET /api/v1.0/users/{userUid}` endpoint and the `DELETE` use-case (which calls `GetUserByUid` to check existence before deleting) both produce silently incorrect results. A delete request for a non-existent user could delete the wrong user.  
**Recommendation:** The query should filter on the `uid` column (the public-facing identifier) and bind the parameter correctly:

```csharp
public async Task<User?> GetUserByUid(Guid userUid)
{
    using NpgsqlConnection connection = dbConnectionfactory.CreateConnection();
    return await connection.QueryFirstOrDefaultAsync<User>(
        "SELECT id, uid, name, email FROM users WHERE uid = @Uid",
        new { Uid = userUid });
}
```

---

### [PostgreSQL Schema] Split Identity Model — `SERIAL id` + `uid UUID` With No Write Path for `uid` — High

**File:** `src/Timezone.Management.Infrastructure/Database/Scripts/0001_create_users_table.sql` (lines 2–3)  
**Issue:** The table has both a `SERIAL` surrogate key (`id`) and a `uuid` natural key (`uid`), but `uid` has no `DEFAULT` clause and no `NOT NULL` constraint is enforced by the insert path. The `INSERT` in `UserRepository.AddUser` only provides `(name, email)`, so `uid` will be inserted as `NULL` in every row. The `RETURNING Uid` clause then returns `NULL`, meaning `AddUser` always returns `Guid.Empty`.  
**Why it matters:** The `uid` column is the public API identifier (exposed in `/api/v1.0/users/{userUid}`). Returning `Guid.Empty` to callers is semantically wrong. Querying by `uid` will never match any row because the stored value is always `NULL`.  
**Recommendation:** Add a server-side default to generate the UUID automatically and add a `NOT NULL` constraint. Use `gen_random_uuid()` (available without extensions in PostgreSQL 13+):

```sql
uid UUID NOT NULL DEFAULT gen_random_uuid(),
```

Alternatively, generate the UUID in the application before the insert and include it explicitly in the `VALUES` clause. Either way, the `User` entity's `Uid` property should be set before the repository call in `UserUseCase.AddUser`.

---

### [PostgreSQL Schema] `created_at` / `updated_at` Have No Default — High

**File:** `src/Timezone.Management.Infrastructure/Database/Scripts/0001_create_users_table.sql` (lines 6, 8)  
**Issue:** Both `created_at TIMESTAMPTZ NOT NULL` and `updated_at TIMESTAMPTZ NOT NULL` are declared `NOT NULL` but have no `DEFAULT` clause, and the application's `INSERT` statement does not provide values for them. PostgreSQL will reject the insert with `ERROR: null value in column "created_at" violates not-null constraint`.  
**Why it matters:** `AddUser` will always fail with a database error, making the create endpoint non-functional.  
**Recommendation:**

```sql
created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
```

For `updated_at`, also add a trigger to keep it current on updates:

```sql
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_users_updated_at
BEFORE UPDATE ON users
FOR EACH ROW EXECUTE FUNCTION set_updated_at();
```

---

### [PostgreSQL Schema] `SERIAL` Primary Key vs. UUID — High

**File:** `src/Timezone.Management.Infrastructure/Database/Scripts/0001_create_users_table.sql` (line 2)  
**Issue:** The table uses `SERIAL` (sequential integer) as the physical primary key while exposing a UUID as the public identifier. This is not inherently wrong, but the current implementation leaks the internal integer (`id`) via the `User` entity's public `Id` property and the `SELECT *` query.  
**Why it matters:** Sequential integer PKs are predictable (enumerable), which makes them a security liability when exposed. The current `SELECT *` in `GetUserByUid` will return `id` to API callers. Additionally, `SERIAL` is a PostgreSQL-specific pseudo-type that has been soft-deprecated since PostgreSQL 10 in favour of `GENERATED ALWAYS AS IDENTITY`.  
**Recommendation:** If `uuid` is the intended public key, either make it the primary key directly (`id UUID PRIMARY KEY DEFAULT gen_random_uuid()`) and drop the integer column, or keep the integer as a private surrogate and ensure it is never serialised in API responses. Replace `SERIAL` with the standard SQL syntax:

```sql
id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
```

---

### [Connection Management] `IDbConnectionFactory` Leaks Npgsql — High

**File:** `src/Timezone.Management.Infrastructure/Database/Contracts/IDbConnectionFactory.cs` (line 7)  
**Issue:** The interface return type is `NpgsqlConnection` (a concrete Npgsql type) rather than `System.Data.IDbConnection`. This means any code that depends on `IDbConnectionFactory` is implicitly coupled to Npgsql, defeating the purpose of the abstraction.  
**Why it matters:** Swapping the underlying database driver (e.g., to a test double or a different Postgres library) requires changing every consumer. Unit-testing repository code in isolation becomes harder because you cannot substitute a lightweight `IDbConnection` mock.  
**Recommendation:**

```csharp
// IDbConnectionFactory.cs
using System.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

// PostgresConnectionFactory.cs
public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
```

---

### [Connection Management] `IDbConnectionFactory` Registered as Scoped Without Opened Connection — Medium

**File:** `src/Timezone.Management.IoC/DI/DependencyInjection.cs` (line 38)  
**Issue:** `IDbConnectionFactory` is registered as `Scoped`. The factory itself is stateless (it holds only a connection string), so `Transient` or `Singleton` would be more appropriate. More importantly, `CreateConnection()` returns an unopened `NpgsqlConnection`. Each repository method must open it separately (Dapper does this automatically, but the lifecycle is implicit).  
**Why it matters:** Because the factory is scoped, a new factory instance is created per request, which is harmless but misleading. The real risk is that callers could forget that the returned connection is not open, leading to subtle bugs if Dapper's auto-open behaviour is ever bypassed.  
**Recommendation:** Register the factory as `Singleton` (the connection string never changes at runtime). Document on the interface that `CreateConnection()` returns a closed connection and that callers are responsible for opening and disposing it. Alternatively, provide a separate `OpenConnection()` helper that returns an already-opened connection.

---

### [DbUp Migration Strategy] DbUp Scans the Wrong Assembly — High

**File:** `src/Timezone.Management.IoC/DI/DependencyInjection.cs` (line 28)  
**Issue:** `WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())` is called from the IoC project. `GetExecutingAssembly()` returns the IoC assembly, but the SQL scripts are embedded as resources in the Infrastructure assembly. DbUp will find zero scripts and silently skip all migrations.  
**Why it matters:** On a fresh database the `users` table will never be created, causing every repository call to fail with a "relation does not exist" error. The bug is masked locally only if the database was created by other means.  
**Recommendation:** Pass the Infrastructure assembly explicitly:

```csharp
using Timezone.Management.Infrastructure.Database.ConnectionFactory; // any type in that assembly

UpgradeEngine upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(typeof(PostgresConnectionFactory).Assembly)
    .LogToConsole()
    .Build();
```

---

### [DbUp Migration Strategy] No Rollback Strategy — Medium

**File:** `src/Timezone.Management.IoC/DI/DependencyInjection.cs` (lines 26–35)  
**Issue:** DbUp applies migrations in a forward-only, append-only manner with no rollback mechanism. There are no down-scripts, no compensating transactions documented, and no process defined for reverting a bad migration.  
**Why it matters:** A defective migration (e.g., a `DROP COLUMN` or a bad `ALTER TYPE`) in production will corrupt data with no automated recovery path. The application will fail to start on every subsequent deployment attempt.  
**Recommendation:** Adopt a forward-only rollback pattern: every destructive migration must be paired with a corresponding "undo" migration script that is applied manually when needed. Document this convention in `Database/Scripts/README.md`. Consider using transactional DDL: wrap each script in `BEGIN; ... COMMIT;` so that a failed script does not leave the schema in a partially applied state. DbUp applies each script in its own implicit transaction by default on PostgreSQL, which is correct, but this should be verified and made explicit.

---

### [DbUp Migration Strategy] `CREATE TABLE IF NOT EXISTS` Undermines DbUp's Journal — Low

**File:** `src/Timezone.Management.Infrastructure/Database/Scripts/0001_create_users_table.sql` (line 1)  
**Issue:** The `IF NOT EXISTS` guard makes the DDL statement idempotent in isolation, but DbUp already tracks applied scripts in its journal table (`schemaversions`). Using `IF NOT EXISTS` implies the script might be run more than once, which contradicts DbUp's design principle that each script runs exactly once.  
**Why it matters:** The guard is harmless but indicates a conceptual mismatch. More dangerously, if the index creation lines are added without `IF NOT EXISTS`, they will fail on re-run with a "relation already exists" error — which could happen if the journal table is lost and migrations are re-applied.  
**Recommendation:** Rely on DbUp's journal for idempotency at the script level. For index creation, add `IF NOT EXISTS` guards as defensive programming (`CREATE INDEX IF NOT EXISTS`), which PostgreSQL 9.5+ supports. Remove `IF NOT EXISTS` from `CREATE TABLE` unless you have a specific reason to run scripts outside of DbUp.

---

### [Repository] `SELECT *` Instead of Explicit Columns — Medium

**File:** `src/Timezone.Management.Infrastructure/Repositories/UserRepository.cs` (line 28)  
**Issue:** `SELECT * FROM users` retrieves all columns, including the internal `id` integer and the audit columns (`created_at`, `updated_at`, `created_by`, `updated_by`).  
**Why it matters:** `SELECT *` is fragile: adding a column to the table changes what is returned and may break Dapper's mapping silently (new columns with no matching property) or accidentally expose internal data. It also leaks `id` to API callers via the `User` entity.  
**Recommendation:** Always name columns explicitly:

```sql
SELECT uid, name, email FROM users WHERE uid = @Uid
```

---

### [Repository] `AddUser` INSERT Omits Required Columns — High

**File:** `src/Timezone.Management.Infrastructure/Repositories/UserRepository.cs` (lines 15–19)  
**Issue:** The `INSERT INTO users (name, email) VALUES (@Name, @Email)` statement omits `uid`, `created_at`, and `updated_at`, all of which are declared `NOT NULL` in the DDL (or will be once the schema is fixed). Dapper maps `@Name` and `@Email` from the `User` entity, but the entity has no `CreatedAt`, `UpdatedAt`, `CreatedBy`, or `UpdatedBy` properties.  
**Why it matters:** The insert will fail with a PostgreSQL not-null constraint violation for `created_at` and `updated_at` (unless server-side defaults are added, as recommended above). The audit trail (`created_by`, `updated_by`) is never populated.  
**Recommendation:** Either add `DEFAULT now()` to the audit timestamp columns (recommended) and generate `uid` server-side, or extend the `User` entity and the insert statement to supply all required values. The `created_by` / `updated_by` columns should receive the authenticated user's identifier from the request context, not be left nullable and empty.

---

### [Repository] Redundant Index on `email` — Low

**File:** `src/Timezone.Management.Infrastructure/Database/Scripts/0001_create_users_table.sql` (lines 6, 13)  
**Issue:** The `email` column has a `UNIQUE` constraint (line 6), which PostgreSQL automatically backs with a unique B-tree index. Line 13 then creates a second, non-unique index on the same column.  
**Why it matters:** The duplicate index consumes additional disk space and write overhead (every insert/update must maintain both indexes), while providing zero read benefit over the unique index that is already present.  
**Recommendation:** Remove the explicit `CREATE INDEX` on `email`. The `UNIQUE` constraint's implicit index already covers all lookup patterns for email.

---

### [Repository] `UpdateUser` and `DeleteUser` Not Implemented — High

**File:** `src/Timezone.Management.Infrastructure/Repositories/UserRepository.cs` (lines 22–23, 31–32)  
**Issue:** Both methods throw `NotImplementedException`. The API endpoints for `PUT /api/v1.0/users/{userUid}` and `DELETE /api/v1.0/users/{userUid}` are registered and reachable but will always return HTTP 500.  
**Why it matters:** Two of the four CRUD operations are completely broken at the infrastructure layer. Any user or test that calls these endpoints receives an unhandled exception.  
**Recommendation:** Implement both methods. For `UpdateUser`:

```csharp
public async Task UpdateUser(Guid userUid, User user)
{
    using NpgsqlConnection connection = dbConnectionfactory.CreateConnection();
    await connection.ExecuteAsync(
        "UPDATE users SET name = @Name, email = @Email, updated_at = now() WHERE uid = @Uid",
        new { user.Name, user.Email, Uid = userUid });
}
```

For `DeleteUser`:

```csharp
public async Task DeleteUser(Guid userUid)
{
    using NpgsqlConnection connection = dbConnectionfactory.CreateConnection();
    await connection.ExecuteAsync(
        "DELETE FROM users WHERE uid = @Uid",
        new { Uid = userUid });
}
```

---

### [Code Style] Suppressed Analyzer Warnings in `PostgresConnectionFactory` — Low

**File:** `src/Timezone.Management.Infrastructure/Database/ConnectionFactory/PostgresConnectionFactory.cs` (lines 10–16)  
**Issue:** `#pragma warning disable IDE0021` and `IDE0290` are used to suppress the "use expression body for constructor" and "use primary constructor" rules that the project's own `.editorconfig` declares as required style.  
**Why it matters:** The suppressions create an inconsistency with the rest of the codebase (e.g., `UserRepository` correctly uses a primary constructor). The likely reason for the suppression is that a primary constructor with an expression body for the field assignment is syntactically awkward, but the solution is a primary constructor with a single field initializer, not a suppression.  
**Recommendation:** Rewrite using a primary constructor and an expression-bodied `CreateConnection`:

```csharp
public class PostgresConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("Postgres")!;

    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
```

---

### [Infrastructure Resilience] No Retry Policy for Transient Database Failures — Medium

**File:** All repository methods in `src/Timezone.Management.Infrastructure/Repositories/UserRepository.cs`  
**Issue:** Repository calls have no retry logic for transient PostgreSQL errors (e.g., `53300 too_many_connections`, `08006 connection_failure`, or connection pool exhaustion). A single transient error causes an unhandled exception that propagates to the API caller as HTTP 500.  
**Why it matters:** Transient connectivity failures are normal in cloud-hosted PostgreSQL (RDS, Cloud SQL, Supabase, Azure Database for PostgreSQL). Without retries, a single blip causes visible user-facing errors.  
**Recommendation:** Add Polly (or the built-in `Microsoft.Extensions.Http.Resilience` for .NET 8+) with an exponential back-off retry policy scoped to known transient Npgsql error codes:

```csharp
// Example with Polly
ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder().Handle<NpgsqlException>(ex => ex.IsTransient),
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(200),
        BackoffType = DelayBackoffType.Exponential
    })
    .Build();
```

---

### [Infrastructure Resilience] No Database Health Check — Medium

**File:** `src/Timezone.Management.API/Program.cs`  
**Issue:** No health check endpoint is registered. There is no `/health` or `/ready` route that probes the PostgreSQL connection.  
**Why it matters:** Kubernetes liveness/readiness probes, load balancers, and monitoring tools have no reliable signal for database availability. A pod with a broken DB connection will continue to receive traffic.  
**Recommendation:** Add the built-in ASP.NET Core health check with an Npgsql probe:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString, name: "postgres");

app.MapHealthChecks("/health");
```

(`AspNetCore.HealthChecks.Npgsql` NuGet package provides the Npgsql probe.)

---

### [Infrastructure Resilience] No Structured Logging or Observability — Medium

**File:** `src/Timezone.Management.IoC/DI/DependencyInjection.cs` (line 29); `src/Timezone.Management.Infrastructure/Repositories/UserRepository.cs`  
**Issue:** DbUp is configured to `LogToConsole()` (unstructured). Repository methods have no logging at all. There is no OpenTelemetry or distributed tracing instrumentation.  
**Why it matters:** Slow queries, failed inserts, and connection errors are invisible in production. Diagnosing performance regressions requires attaching a query analyser manually.  
**Recommendation:** Replace `LogToConsole()` with `LogTo(new DbUpLogger(logger))` using `ILogger<T>` from the DI container, so DbUp output appears in the structured log pipeline. In repositories, inject `ILogger<UserRepository>` and log at `Warning` or `Error` level on exception. For Dapper-level tracing, consider `MiniProfiler` or the Npgsql OpenTelemetry integration (`Npgsql.OpenTelemetry`).

---

### [Repository Pattern] No Unit of Work / Transaction Management — Medium

**File:** `src/Timezone.Management.Application/UseCases/UserUseCase.cs` (lines 44–53); `src/Timezone.Management.Infrastructure/Repositories/UserRepository.cs`  
**Issue:** `DeleteUser` in `UserUseCase` performs two separate repository calls (`GetUserByUid` then `DeleteUser`) with no wrapping transaction. Each call opens and closes its own independent connection.  
**Why it matters:** Between the `GET` and the `DELETE`, another request could delete or modify the same user, producing a time-of-check / time-of-use (TOCTOU) race condition. The delete could target a user that no longer exists, or a concurrent insert could satisfy the existence check for the wrong record.  
**Recommendation:** Introduce a Unit of Work abstraction that passes a shared `IDbTransaction` across repository calls, or perform the existence check inside the `DELETE` statement itself using `DELETE ... WHERE uid = @Uid RETURNING uid` and checking whether any row was returned:

```csharp
public async Task<bool> DeleteUser(Guid userUid)
{
    using NpgsqlConnection connection = dbConnectionfactory.CreateConnection();
    Guid? deleted = await connection.ExecuteScalarAsync<Guid?>(
        "DELETE FROM users WHERE uid = @Uid RETURNING uid",
        new { Uid = userUid });
    return deleted.HasValue;
}
```

---

### [Repository Pattern] `IUserRepository` Return Types Are Weakly Typed — Low

**File:** `src/Timezone.Management.Application/Contracts/Repositories/IUserRepository.cs`  
**Issue:** `UpdateUser` and `DeleteUser` return `Task` (void-equivalent). There is no way for callers to know whether the operation actually affected a row or silently did nothing (e.g., when updating a non-existent UID).  
**Why it matters:** `UserUseCase.UpdateUser` calls `repository.UpdateUser(...)` and immediately returns success, even if zero rows were updated. The API will return `204 No Content` for an update against a non-existent user.  
**Recommendation:** Return `Task<bool>` (or `Task<int>` for affected row count) so callers can detect the no-op case:

```csharp
Task<bool> UpdateUser(Guid userUid, User user);
Task<bool> DeleteUser(Guid userUid);
```

---

### [Security] No Email Validation Format Check — Low

**File:** `src/Timezone.Management.Application/Validators/UserValidator.cs` (lines 17–20)  
**Issue:** The `UserValidator` checks only that `Email` is non-empty and between 3–100 characters. It does not validate that the value is a valid email address format.  
**Why it matters:** Arbitrary strings will be stored in the `email` column. The `UNIQUE` constraint ensures no duplicates, but the application has no protection against storing `"x"` or `"notanemail"` as a user's email address.  
**Recommendation:** Add FluentValidation's built-in email rule:

```csharp
RuleFor(user => user.Email)
    .NotEmpty()
    .EmailAddress()
    .MaximumLength(100);
```

---

### [PostgreSQL Schema] No `COMMENT ON` Column Documentation — Informational

**File:** `src/Timezone.Management.Infrastructure/Database/Scripts/0001_create_users_table.sql`  
**Issue:** No `COMMENT ON COLUMN` or `COMMENT ON TABLE` statements are present. The purpose of `uid` vs. `id`, the format expected in `created_by`/`updated_by`, and any domain invariants are undocumented at the schema level.  
**Why it matters:** New developers and DBAs must infer column semantics from application code. Tools like `psql \d+ users` and schema browsers (pgAdmin, DBeaver) show comments inline, making schema intent immediately visible.  
**Recommendation:** Add comments to the migration script:

```sql
COMMENT ON TABLE users IS 'Application users managed by the Timezone Management API.';
COMMENT ON COLUMN users.uid IS 'Public UUID exposed in API responses; generated server-side.';
COMMENT ON COLUMN users.id  IS 'Internal surrogate key; never exposed externally.';
```

---

### [Performance] No Pagination on List Endpoints — Informational

**File:** `src/Timezone.Management.Application/Contracts/Repositories/IUserRepository.cs`; `src/Timezone.Management.API/Endpoints/UserEndpoints.cs`  
**Issue:** There is no `GetUsers` (list) endpoint or repository method yet, but the pattern is worth establishing now. When it is added, returning an unbounded result set without `LIMIT`/`OFFSET` or keyset pagination would be a scalability risk.  
**Why it matters:** A table with millions of rows would cause the application to OOM or time out on a list query.  
**Recommendation:** When adding a list endpoint, design the repository signature with pagination from the start:

```csharp
Task<IReadOnlyList<User>> GetUsers(int pageSize, Guid? afterUid = null);
```

Use keyset pagination (`WHERE uid > @AfterUid ORDER BY uid LIMIT @PageSize`) rather than `OFFSET` for consistent performance at scale.

---

### [Performance] No Npgsql Connection Pool Tuning — Low

**File:** `src/Timezone.Management.API/appsettings.json` (line 10); `src/Timezone.Management.Infrastructure/Database/ConnectionFactory/PostgresConnectionFactory.cs`  
**Issue:** The connection string `postgresql://postgres:05102025@localhost:5432` specifies no database name and no pool parameters. Npgsql defaults (`MinPoolSize=0`, `MaxPoolSize=100`) are applied silently.  
**Why it matters:** The missing database name means Npgsql connects to the `postgres` default database, not a dedicated application database. Under load, the default pool ceiling of 100 may be too high for small instances or too low for high-throughput scenarios. Pool misconfiguration causes connection exhaustion errors that are difficult to diagnose.  
**Recommendation:** Use a key-value connection string with explicit parameters:

```
Host=localhost;Port=5432;Database=timezone_management;Username=appuser;Password=...;
Minimum Pool Size=2;Maximum Pool Size=20;Connection Idle Lifetime=300;
```

Always create a dedicated database and a dedicated low-privilege application user rather than using the `postgres` superuser.

---

## Prioritized Action Plan

| Priority | Finding | Effort | Impact |
|----------|---------|--------|--------|
| 1 | Plaintext credentials in `appsettings.json` — move to user secrets / env vars | Low | Critical |
| 2 | Invalid `NONCLUSTERED INDEX` DDL syntax — fix to standard PostgreSQL `CREATE INDEX` | Low | Critical |
| 3 | Broken `GetUserByUid` query (`WHERE id = id`) — fix parameter binding and column reference | Low | Critical |
| 4 | `uid` column has no `DEFAULT gen_random_uuid()` and no NOT NULL path in insert | Low | High |
| 5 | `created_at`/`updated_at` have no server-side default — insert always fails | Low | High |
| 6 | DbUp scans wrong assembly (`GetExecutingAssembly()` in IoC project, scripts in Infrastructure) | Low | High |
| 7 | `UpdateUser` and `DeleteUser` throw `NotImplementedException` | Medium | High |
| 8 | `AddUser` INSERT omits required columns; `User` entity missing audit fields | Medium | High |
| 9 | `IDbConnectionFactory` returns concrete `NpgsqlConnection` instead of `IDbConnection` | Low | Medium |
| 10 | TOCTOU race in `DeleteUser` (GET then DELETE without a transaction) | Medium | Medium |
| 11 | `UpdateUser` returns no row-count — silent no-op on unknown UID | Low | Medium |
| 12 | No retry policy for transient database failures | Medium | Medium |
| 13 | No database health check endpoint | Low | Medium |
| 14 | Redundant index on `email` (UNIQUE constraint already creates one) | Low | Low |
| 15 | Suppressed code-style warnings in `PostgresConnectionFactory` | Low | Low |
| 16 | No structured logging / OpenTelemetry for repository calls | Medium | Medium |
| 17 | No email format validation in `UserValidator` | Low | Low |
| 18 | No pagination design for future list endpoints | Low | Low |
| 19 | Connection string missing database name and pool parameters | Low | Low |
| 20 | No `COMMENT ON` schema documentation | Low | Informational |

---

## What Is Done Well

- **Clean architecture boundaries are respected.** The Application layer has zero infrastructure dependencies; all concrete types (Npgsql, Dapper, DbUp) are confined to the Infrastructure and IoC projects.
- **DbUp is the right tool for the job.** Embedding SQL scripts as assembly resources and running them at startup is a sound, simple migration strategy for this scale of application.
- **Primary constructor usage.** `UserRepository` correctly uses the C# primary constructor pattern that the `.editorconfig` requires, keeping the code concise.
- **File-scoped namespaces.** All reviewed files use `namespace Foo.Bar;` consistently, which matches the code-style rules.
- **Scalar / OpenAPI setup.** The API correctly gates Scalar and OpenAPI behind the `IsDevelopment()` check, preventing developer tooling from being exposed in production.
- **`UserSecretsId` is configured.** The API `.csproj` already has a `UserSecretsId`, meaning the infrastructure for proper secret management is in place — it just needs to be used.
- **Nullable reference types enabled.** All projects target .NET 10 with `<Nullable>enable</Nullable>`, which surfaces potential null-dereference bugs at compile time.
- **Async all the way.** Repository methods and use-case methods consistently use `async`/`await` and `Task<T>` return types, avoiding sync-over-async pitfalls.

---

## References

- [PostgreSQL CREATE INDEX](https://www.postgresql.org/docs/current/sql-createindex.html) — correct syntax; `NONCLUSTERED` does not exist in PostgreSQL
- [PostgreSQL Identity Columns](https://www.postgresql.org/docs/current/sql-createtable.html) — `GENERATED ALWAYS AS IDENTITY` replaces `SERIAL`
- [PostgreSQL `gen_random_uuid()`](https://www.postgresql.org/docs/current/functions-uuid.html) — built-in UUID v4 generation since PostgreSQL 13
- [Dapper — Parameterized Queries](https://github.com/DapperLib/Dapper#parameterized-queries) — correct `@Param` binding patterns
- [DbUp Documentation — Embedded Scripts](https://dbup.readthedocs.io/en/latest/usage/) — `typeof(T).Assembly` vs `GetExecutingAssembly()`
- [Npgsql Connection String Parameters](https://www.npgsql.org/doc/connection-string-parameters.html) — pool sizing, idle lifetime, database name
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) — built-in health check middleware
- [AspNetCore.HealthChecks.Npgsql](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks) — Npgsql health check probe
- [Polly Resilience Pipelines](https://github.com/App-vNext/Polly) — retry, circuit breaker for transient DB errors
- [Npgsql OpenTelemetry](https://www.npgsql.org/doc/diagnostics/tracing.html) — distributed tracing for Dapper/Npgsql queries
- [FluentValidation EmailAddress rule](https://docs.fluentvalidation.net/en/latest/built-in-validators.html#email-validator) — email format validation
- [PostgreSQL Keyset Pagination](https://use-the-index-luke.com/no-offset) — why `OFFSET` does not scale and how keyset pagination works
- [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — local secret management without committing credentials

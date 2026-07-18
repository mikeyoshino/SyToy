# Foundation Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete M1-03 through M1-06 and the M2-01 PostgreSQL prerequisite so the solution has tested application primitives, clean startup composition, PostgreSQL persistence, structured logging, safe errors, correlation IDs, and health endpoints.

**Architecture:** Application owns result models, request contracts, behaviors, and the persistence abstraction. Infrastructure owns the EF Core/Identity context, Npgsql configuration, transaction implementation, and PostgreSQL readiness check. Web remains the composition root and owns middleware, exception responses, endpoint mapping, and Serilog startup. The initial Code First migration and automatic startup migration remain M2-03 work.

**Tech Stack:** .NET 10, Blazor Interactive Server, MediatR 12.5.0, FluentValidation 12.1.1, EF Core 10.0.10, Npgsql EF provider 10.0.3, ASP.NET Core Identity 10.0.10, Serilog.AspNetCore 10.0.0, xUnit

---

## Scope and package decision

- Pin MediatR 12.5.0, the final Apache-2.0 release, rather than 13+ dual/community-commercial licensing. An upgrade requires a separate licensing decision.
- Do not add the obsolete `MediatR.Extensions.Microsoft.DependencyInjection` package.
- Do not add OpenTelemetry until an exporter/collector is selected; M1-03 says to avoid optional infrastructure packages.
- `Serilog.AspNetCore` already brings compatible console and file sinks, so do not add redundant direct sink references.
- The workspace has no `.git` metadata. Work in place; replace commit checkpoints with explicit verification and review checkpoints.

### Task 1: Install the minimal package baseline

**Files:**
- Create: `tests/ToyStore.UnitTests/Architecture/PackageDependencyTests.cs`
- Modify: `src/ToyStore.Application/ToyStore.Application.csproj`
- Modify: `src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj`
- Modify: `src/ToyStore.Web/ToyStore.Web.csproj`
- Modify: `docs/ARCHITECTURE.md`

- [x] **Step 1: Write a failing package-baseline architecture test**

Create a test that loads each project XML, reads direct `PackageReference` names and versions, and asserts these required pairs:

```csharp
public static TheoryData<string, string, string> RequiredPackages => new()
{
    { "src/ToyStore.Application/ToyStore.Application.csproj", "MediatR", "12.5.0" },
    { "src/ToyStore.Application/ToyStore.Application.csproj", "FluentValidation.DependencyInjectionExtensions", "12.1.1" },
    { "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj", "Microsoft.AspNetCore.Identity.EntityFrameworkCore", "10.0.10" },
    { "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj", "Microsoft.EntityFrameworkCore.Design", "10.0.10" },
    { "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj", "Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore", "10.0.10" },
    { "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj", "Npgsql.EntityFrameworkCore.PostgreSQL", "10.0.3" },
    { "src/ToyStore.Web/ToyStore.Web.csproj", "Serilog.AspNetCore", "10.0.0" },
};
```

The test must locate `ToyStore.sln` by walking upward from `AppContext.BaseDirectory`, normalize project paths on Windows/Linux/macOS, and report a missing or wrong version as an assertion failure.

- [x] **Step 2: Run the test and verify RED**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter FullyQualifiedName~PackageDependencyTests`

Expected: failures for every not-yet-installed package.

- [x] **Step 3: Add only the required direct packages**

Application package references:

```xml
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
<PackageReference Include="MediatR" Version="12.5.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.10" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.10" />
```

Infrastructure package references:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.10" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.10">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.10" />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.10" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
```

Web package references:

```xml
<PackageReference Include="Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore" Version="10.0.10" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.10" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.10" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.10" />
<PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
```

Align the temporary generated Web EF packages at 10.0.10 to prevent a direct-dependency downgrade while Infrastructure references Identity EF 10.0.10. Keep the SQLite references only until Task 3 moves persistence; Task 3 must remove them and the advisory before M1-03 is marked complete.

- [x] **Step 4: Record the MediatR decision**

Add an architecture dependency note stating that 12.5.0 is pinned as Apache-2.0, 13+ requires a new license review, and no license-warning suppression is allowed. Record that OpenTelemetry is deferred until an exporter/collector and concrete task are selected, so no unused telemetry packages are installed during M1.

- [x] **Step 5: Verify GREEN and restore compatibility**

Run: `dotnet restore ToyStore.sln`

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter FullyQualifiedName~PackageDependencyTests`

Expected: package tests pass. The pre-existing SQLite advisory may remain only until Task 3.

### Task 2: Create Application common primitives and behaviors

**Files:**
- Create: `src/ToyStore.Application/Common/Models/Error.cs`
- Create: `src/ToyStore.Application/Common/Models/ErrorType.cs`
- Create: `src/ToyStore.Application/Common/Models/Result.cs`
- Create: `src/ToyStore.Application/Common/Models/ResultOfT.cs`
- Create: `src/ToyStore.Application/Common/Models/PagedResult.cs`
- Create: `src/ToyStore.Application/Common/Interfaces/IApplicationDbContext.cs`
- Create: `src/ToyStore.Application/Common/Messaging/ICommand.cs`
- Create: `src/ToyStore.Application/Common/Behaviors/LoggingBehavior.cs`
- Create: `src/ToyStore.Application/Common/Behaviors/ValidationBehavior.cs`
- Create: `src/ToyStore.Application/Common/Behaviors/TransactionBehavior.cs`
- Modify: `src/ToyStore.Application/DependencyInjection.cs`
- Create: `tests/ToyStore.UnitTests/Application/ResultTests.cs`
- Create: `tests/ToyStore.UnitTests/Application/PagedResultTests.cs`
- Create: `tests/ToyStore.UnitTests/Application/LoggingBehaviorTests.cs`
- Create: `tests/ToyStore.UnitTests/Application/ValidationBehaviorTests.cs`
- Create: `tests/ToyStore.UnitTests/Application/TransactionBehaviorTests.cs`

- [x] **Step 1: Write failing result and pagination tests**

Cover these behaviors with real objects:

```csharp
var result = Result.Success();
Assert.True(result.IsSuccess);
Assert.Equal(Error.None, result.Error);

var error = new Error("products.not_found", "ไม่พบสินค้า", ErrorType.NotFound);
var failure = Result.Failure(error);
Assert.True(failure.IsFailure);
Assert.Equal(error, failure.Error);

var valueResult = Result<int>.Success(42);
Assert.Equal(42, valueResult.Value);

var failedValue = Result<int>.Failure(error);
Assert.Throws<InvalidOperationException>(() => failedValue.Value);

var page = new PagedResult<int>([1, 2], 2, 2, 5);
Assert.Equal(3, page.TotalPages);
Assert.True(page.HasPreviousPage);
Assert.True(page.HasNextPage);
```

- [x] **Step 2: Run result tests and verify RED**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter "FullyQualifiedName~ResultTests|FullyQualifiedName~PagedResultTests"`

Expected: compilation fails because the models do not exist.

- [x] **Step 3: Implement minimal result and pagination models**

Use these contracts:

```csharp
public enum ErrorType { Failure, Validation, NotFound, Conflict, Unauthorized, Forbidden }

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if ((isSuccess && error != Error.None) || (!isSuccess && error == Error.None))
        {
            throw new ArgumentException("Result state and error must be consistent.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? value;
    private Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) =>
        this.value = value;
    public T Value => IsSuccess ? value! : throw new InvalidOperationException("A failure result has no value.");
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, true, Error.None);
    }
    public static new Result<T> Failure(Error error) => new(default, false, error);
}

public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        Items = items.ToArray();
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
```

Validate constructor inputs for impossible states; misuse may throw because it indicates a programming error, not an expected business failure.

- [x] **Step 4: Verify result GREEN**

Run the focused result/pagination command again. Expected: all pass.

- [x] **Step 5: Write failing validation and transaction behavior tests**

Create a real test request, validators, and in-memory fake context. Verify validation executes validators sequentially so scoped dependencies are safe, aggregates every failure, stops the handler, and throws `ValidationException`; valid input reaches the handler. Verify only `ICommand<TResponse>` executes through `ExecuteInTransactionAsync`, returns the handler response, and forwards cancellation. Add a capturing-logger test proving logging reports completion/failure and timing without exposing a sentinel request value.

The persistence abstraction must be:

```csharp
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
```

The command marker must be:

```csharp
public interface ICommand<out TResponse> : IRequest<TResponse>;
```

- [x] **Step 6: Run behavior tests and verify RED**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter "FullyQualifiedName~ValidationBehaviorTests|FullyQualifiedName~TransactionBehaviorTests"`

Expected: compilation fails because behaviors and contracts do not exist.

- [x] **Step 7: Implement behaviors and Application registration**

`LoggingBehavior<TRequest,TResponse>` must log request name and elapsed milliseconds through cached `LoggerMessage.Define` delegates without serializing request bodies.

`ValidationBehavior<TRequest,TResponse>` must execute validators sequentially because validators can share scoped, non-thread-safe dependencies such as an EF Core context; aggregate all failures and throw one FluentValidation `ValidationException` before calling the handler.

`TransactionBehavior<TRequest,TResponse>` must be constrained to `ICommand<TResponse>` and delegate the complete handler execution to `IApplicationDbContext.ExecuteInTransactionAsync`.

Register assembly handlers, validators, and open behaviors in this order:

```csharp
var assembly = typeof(AssemblyReference).Assembly;
services.AddValidatorsFromAssembly(assembly);
services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(assembly);
    configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
    configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

- [x] **Step 8: Verify behavior GREEN and all Application tests**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter FullyQualifiedName~Application`

Expected: all Application tests pass without analyzer warnings.

### Task 3: Implement the PostgreSQL persistence foundation and remove SQLite

**Files:**
- Create: `src/ToyStore.Infrastructure/Identity/ApplicationUser.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/ApplicationDbContext.cs`
- Modify: `src/ToyStore.Infrastructure/DependencyInjection.cs`
- Modify: `src/ToyStore.Web/Program.cs`
- Modify: all account components importing `ToyStore.Web.Data`
- Delete: `src/ToyStore.Web/Data/ApplicationDbContext.cs`
- Delete: `src/ToyStore.Web/Data/ApplicationUser.cs`
- Delete: `src/ToyStore.Web/Data/Migrations/`
- Delete: `src/ToyStore.Web/Data/app.db`
- Modify: `src/ToyStore.Web/ToyStore.Web.csproj`
- Modify: `src/ToyStore.Web/appsettings.json`
- Create: `tests/ToyStore.UnitTests/Infrastructure/PersistenceRegistrationTests.cs`
- Modify: `tests/ToyStore.UnitTests/Architecture/PackageDependencyTests.cs`
- Modify: `tests/ToyStore.UnitTests/Architecture/LayerRegistrationTests.cs`
- Modify: `docs/ARCHITECTURE.md`

- [x] **Step 1: Write failing persistence registration tests**

Build a `ServiceCollection` with an in-memory `ConfigurationBuilder` containing `ConnectionStrings:Database`. Call `AddInfrastructure(configuration)` and verify:

```csharp
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var concrete = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var abstraction = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
Assert.Same(concrete, abstraction);
Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", concrete.Database.ProviderName);
```

Also verify missing `ConnectionStrings:Database` throws `InvalidOperationException` with a configuration-safe message, and extend package tests so no production project references `Microsoft.EntityFrameworkCore.Sqlite` or contains `Data/app.db`.

- [x] **Step 2: Run persistence/package tests and verify RED**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter "FullyQualifiedName~PersistenceRegistrationTests|FullyQualifiedName~PackageDependencyTests"`

Expected: missing Infrastructure context/overload and SQLite-forbidden assertions fail.

- [x] **Step 3: Move Identity persistence and configure Npgsql**

Use this context shape:

```csharp
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
```

Change Infrastructure registration to:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
    services.AddScoped<IApplicationDbContext>(provider =>
        provider.GetRequiredService<ApplicationDbContext>());
    return services;
}
```

Update `LayerRegistrationTests` to require the new `AddInfrastructure(IServiceCollection, IConfiguration)` signature while preserving its checks that the method is public, static, and returns `IServiceCollection`.

Retain conventional EF/Identity table names for the first migration; do not add a naming-conventions package.

- [x] **Step 4: Update Web composition and remove SQLite artifacts**

Call `builder.Services.AddInfrastructure(builder.Configuration)`, update Identity to use Infrastructure `ApplicationUser` and `ApplicationDbContext`, remove `UseSqlite`, remove the SQLite connection string, package, copied database, generated SQLite migration, and developer migrations endpoint package/middleware.

- [x] **Step 5: Verify persistence GREEN and clean restore**

Run: `dotnet restore ToyStore.sln`

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter "FullyQualifiedName~PersistenceRegistrationTests|FullyQualifiedName~PackageDependencyTests|FullyQualifiedName~Architecture"`

Run: `dotnet list ToyStore.sln package --vulnerable --include-transitive`

Expected: focused tests pass; no SQLite package/database remains; vulnerability scan reports no vulnerable packages.

### Task 4: Configure startup, safe errors, Serilog, and correlation IDs

**Files:**
- Create: `src/ToyStore.Web/Diagnostics/CorrelationIdMiddleware.cs`
- Create: `src/ToyStore.Web/Diagnostics/GlobalExceptionHandler.cs`
- Modify: `src/ToyStore.Web/Program.cs`
- Modify: `src/ToyStore.Web/appsettings.json`
- Modify: `src/ToyStore.Web/Components/Account/IdentityNoOpEmailSender.cs`
- Modify: `src/ToyStore.Web/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs`
- Modify: `deploy/toystore.service.example`
- Modify: `docs/DEPLOYMENT.md`
- Modify: `tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj`
- Create: `tests/ToyStore.UnitTests/Architecture/DeploymentConfigurationTests.cs`
- Create: `tests/ToyStore.UnitTests/Web/CorrelationIdMiddlewareTests.cs`
- Create: `tests/ToyStore.UnitTests/Web/GlobalExceptionHandlerTests.cs`
- Create: `tests/ToyStore.IntegrationTests/ForwardedHeadersTests.cs`

- [x] **Step 1: Write failing middleware and exception-handler tests**

Add a direct ProjectReference from UnitTests to `src/ToyStore.Web/ToyStore.Web.csproj` so the tests compile against the real middleware and exception handler.

Correlation tests must verify a valid inbound `X-Correlation-ID` is retained in `HttpContext.TraceIdentifier` and response headers, missing/invalid values generate a safe identifier, and the next delegate is invoked once.

Exception tests must pass an exception containing a secret marker, invoke `TryHandleAsync`, deserialize the response, and verify status 500, Thai customer-safe title/detail, trace ID presence, and absence of the exception message and stack trace.

- [x] **Step 2: Run Web diagnostics tests and verify RED**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter "FullyQualifiedName~CorrelationIdMiddlewareTests|FullyQualifiedName~GlobalExceptionHandlerTests"`

Expected: compilation fails because diagnostics types do not exist.

- [x] **Step 3: Implement diagnostics**

`CorrelationIdMiddleware` must accept only 1-128 characters from `[A-Za-z0-9._-]`, otherwise generate `Guid.NewGuid().ToString("N")`; set trace identifier and response header; begin a structured logging scope; never log arbitrary invalid header content.

`GlobalExceptionHandler` must implement `IExceptionHandler`, use a source-generated `LoggerMessage` method for the internal exception, return RFC 7807 JSON with status 500 and Thai safe copy, and include only a trace identifier as diagnostic detail.

- [x] **Step 4: Wire Application, Infrastructure, Serilog, and middleware**

Register `AddApplication()`, `AddInfrastructure(configuration)`, `AddExceptionHandler<GlobalExceptionHandler>()`, and `AddProblemDetails()`. Configure Serilog from configuration and services, add correlation middleware before exception handling, add `UseSerilogRequestLogging()`, and keep production HSTS.

Configure console and daily rolling file output at `logs/toystore-.log`, retain 14 files, enrich from log context, and override Microsoft/ASP.NET noise to Warning. Do not log request bodies or secrets.

For the hardened systemd deployment, override the file sink to `/var/lib/toystore/logs/toystore-.log`, provision it with `StateDirectory=toystore/logs`, and keep `ReadWritePaths` narrowly scoped to persistent application data. Cover this deployment contract with an architecture test.

- [x] **Step 5: Remove generated analyzer warnings**

Type the no-op email sender field as `NoOpEmailSender`, and replace the personal-data `LogInformation` call with a cached/source-generated logging delegate so CA1859, CA1848, and CA1873 are resolved without suppressions.

- [x] **Step 6: Verify diagnostics GREEN and clean CI build**

Run the focused diagnostics tests again.

Run: `dotnet build ToyStore.sln --no-restore -p:CI=true`

Expected: focused tests pass and the full CI build has 0 warnings and 0 errors.

### Task 5: Add liveness and PostgreSQL readiness endpoints

**Files:**
- Modify: `src/ToyStore.Infrastructure/DependencyInjection.cs`
- Modify: `src/ToyStore.Web/Program.cs`
- Modify: `tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj`
- Delete: `tests/ToyStore.IntegrationTests/UnitTest1.cs`
- Create: `tests/ToyStore.IntegrationTests/HealthEndpointTests.cs`
- Modify: `docs/LOCAL_DEVELOPMENT.md`

- [x] **Step 1: Write failing endpoint integration tests**

Reference `Microsoft.AspNetCore.Mvc.Testing` 10.0.10 and the Web project. Expose `public partial class Program` for `WebApplicationFactory<Program>`. Supply a deliberately unreachable PostgreSQL connection string with one-second timeout.

Verify:

```csharp
var live = await client.GetAsync("/health/live");
Assert.Equal(HttpStatusCode.OK, live.StatusCode);

var ready = await client.GetAsync("/health/ready");
Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
```

Also verify `/health` includes all checks and returns 503 when PostgreSQL is unavailable.

- [x] **Step 2: Run integration tests and verify RED**

Run: `dotnet test tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj --filter FullyQualifiedName~HealthEndpointTests`

Expected: endpoint tests fail because mappings/check registrations do not exist.

- [x] **Step 3: Register and map health checks**

Infrastructure must register:

```csharp
services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>(
    "postgresql",
    failureStatus: HealthStatus.Unhealthy,
    tags: ["ready"]);
```

Web must register a dependency-free `self` check tagged `live`, then map:

```csharp
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});
```

- [x] **Step 4: Verify endpoint GREEN and document curl checks**

Run the focused integration tests again. Update local-development documentation with curl commands and the meaning of live versus ready.

- [x] **Step 5: Verify against real PostgreSQL**

Run: `docker compose up -d postgres`

Start Web with `ConnectionStrings__Database` set to the documented local PostgreSQL connection and a fixed HTTP URL. Verify `/health/live`, `/health/ready`, and `/health` return HTTP 200, then stop the Web process.

### Task 6: Final review, verification, and task tracking

**Files:**
- Modify: `TASKS.md`
- Update: `docs/superpowers/plans/2026-07-16-foundation-completion.md`
- Review: every file changed by Tasks 1-5

- [x] **Step 1: Run independent final spec and code-quality reviews**

Review every M1-03 through M1-06 acceptance criterion plus M2-01, architecture boundaries, package licensing, validation/transaction behavior, secret handling, PostgreSQL configuration, health semantics, and test realism. Fix all Critical and Important findings and re-run affected tests.

- [x] **Step 2: Run fresh full verification**

Run each command separately:

```text
dotnet restore ToyStore.sln
dotnet build ToyStore.sln --no-restore -p:CI=true
dotnet test ToyStore.sln --no-build
dotnet list ToyStore.sln package --vulnerable --include-transitive
docker compose config
```

Expected: restore/build/test exit 0; CI build has zero warnings/errors; all tests pass; vulnerability scan finds no vulnerable packages; Compose validates.

- [x] **Step 3: Update TASKS.md only after verification**

Mark M1-03, M1-04, M1-05, M1-06, M1-R01, M1-R02, M1-R03, M1-R04, and M2-01 complete only where the implementation and verification directly satisfy them. Add concise Verified notes. Set Current Focus to M2-02 and Next task to M2-03. Do not mark M2-02 or M2-03 complete.

- [x] **Step 4: Record execution evidence in this plan**

Mark completed checkboxes and append RED/GREEN, review, build, test, vulnerability, Compose, and HTTP health evidence.

## Execution evidence

- TDD RED: package contract failed 7 required references; Application model/behavior tests initially failed to compile; persistence tests initially failed on the missing Infrastructure context; diagnostics tests initially failed on missing Web diagnostics; health endpoints returned 404; trusted forwarded HTTPS initially redirected with 307.
- TDD GREEN: package 7/7; Application 28/28; persistence/package/layer 16/16; diagnostics/deployment 14/14; health 3/3; forwarded headers 3/3.
- Reviews: every task passed independent specification and code-quality review. Final cross-cutting review found and verified the loopback-only Caddy forwarded-header fix; final spec and quality verdicts are approved.
- Restore/build/test: `dotnet restore ToyStore.sln` succeeded; CI build completed with 0 warnings and 0 errors; full tests passed 62 unit + 6 integration (68 total).
- Security/configuration: transitive vulnerability scan found no vulnerable packages; `docker compose config` succeeded; SQLite package/database/migrations are absent.
- Runtime: with local Docker PostgreSQL, `/health/live`, `/health/ready`, and `/health` each returned HTTP 200. With PostgreSQL unavailable, liveness returned 200 while readiness and combined health returned 503.
- Tracking: M1-03 through M1-06, M1-R01 through M1-R04, and M2-01 are marked verified. M2-02 and M2-03 remain pending; the initial Code First migration and `MigrateAsync()` startup behavior were not implemented in this scope.

## Plan self-review

- Coverage: every remaining M1 task is mapped, and M2-01 is included because M1-06 explicitly depends on it.
- Boundary check: Application has no Infrastructure/EF dependency; EF, Identity persistence, Npgsql, and readiness remain in Infrastructure; Web is composition only.
- Scope check: M2-02 Identity roles/policies, M2-03 initial migration/startup migration, OpenTelemetry, UI redesign, and commerce features remain out of scope.
- Type consistency: `IApplicationDbContext`, `ICommand<TResponse>`, Application registration, Infrastructure registration, Web composition, and tests use the same signatures throughout.
- Placeholder scan: all implementation steps specify concrete behavior, files, commands, and expected outcomes.

# M2 Identity, PostgreSQL Test Harness, and Startup Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:test-driven-development` for every behavior change, `superpowers:verification-before-completion` before checking a task, and `superpowers:requesting-code-review` before final handoff. Use `superpowers:executing-plans` or `superpowers:subagent-driven-development` to execute this plan task by task.

**Goal:** Complete M2-02 through M2-05 so Thai-first email/password accounts, Customer/Admin authorization, safe first-admin bootstrap, persistent cookies, an isolated PostgreSQL integration-test harness, and the initial Code First migration are implemented and verified. The Web process must apply pending migrations before listening and fail startup when migration cannot complete.

**Architecture:** Account Razor pages send MediatR commands through `ISender`; Application owns account use-case contracts, policy/role names, and expected `Result<T>` failures; Infrastructure owns ASP.NET Core Identity, EF stores, role initialization, and first-admin bootstrap. Web remains the composition root and owns cookies, Data Protection configuration, Thai account presentation, route redirects, and startup orchestration. Tests use a throwaway PostgreSQL 17 Testcontainer and Respawn, never the developer database.

**Tech Stack:** .NET 10, Blazor Interactive Server, ASP.NET Core Identity 10.0.10, EF Core 10.0.10, Npgsql 10.0.3, MediatR 12.5.0, PostgreSQL 17, xUnit v3 3.2.2, Testcontainers.PostgreSql 4.13.0, Respawn 7.0.0.

**Approved product rules:** `docs/superpowers/specs/2026-07-17-commerce-platform-design.md` is the product source of truth. UI and validation copy are Thai first. Only `Customer` and `Admin` roles exist. Registration does not require an email-confirmation link. Forgot password is visible but disabled in v1. First Admin creation is an explicit command using configuration-held temporary credentials and must force a password change.

**Repository note:** This workspace currently has no `.git` metadata. Do not claim commits were created. Use the verification and review checkpoints below in place of commit checkpoints.

**Status:** Completed and independently reviewed on 2026-07-17. Final verification: CI build 0 warnings/errors, Unit 110/110, Integration 41/41, vulnerability scan clean, and Compose configuration valid. The detailed checklists below remain as the TDD execution record.

---

## Task 1: Align M2 source-of-truth documentation

**Files:**

- Modify: `TASKS.md`
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/LOCAL_DEVELOPMENT.md`
- Modify: `docs/DEPLOYMENT.md`
- Modify: `deploy/toystore.service.example`
- Test: `tests/ToyStore.UnitTests/Architecture/DeploymentConfigurationTests.cs`

- [ ] **Step 1: Write failing architecture/documentation assertions**

Extend `DeploymentConfigurationTests` with assertions that:

1. the service unit declares both `StateDirectory=toystore/logs toystore/keys` and writable uploads/keys/logs paths;
2. production documentation contains `DataProtection__KeysPath=/var/lib/toystore/keys`;
3. local documentation uses Code First and says startup applies `Database.MigrateAsync()`;
4. `TASKS.md` and `docs/ARCHITECTURE.md` contain `Customer` and `Admin`, and no longer declare `Staff` as an active role.

Use repository-root discovery already present in the architecture tests. Do not assert broad prose snapshots; assert the exact security and role contracts.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj \
  --filter FullyQualifiedName~DeploymentConfigurationTests
```

Expected: at least the systemd StateDirectory and obsolete `Staff` assertions fail.

- [ ] **Step 3: Update the source-of-truth documents**

Make these exact decisions consistent across the four documents:

- roles are `Customer` and `Admin` only;
- `CanManageProducts`, `CanManageOrders`, `CanVerifyPayments`, and `CanManageUsers` all require `Admin`;
- an Admin whose `MustChangePassword` claim is true cannot use Admin policies;
- registration uses email/password/confirm-password, requires no confirmation link, and signs the customer in after success;
- Data Protection keys persist to a configured directory and production startup fails if the path is absent;
- first Admin is created only by `--bootstrap-admin`, with email/password read from configuration rather than command-line arguments;
- the bootstrap command migrates, seeds roles, creates at most one initial Admin, exits without listening, and never logs its password;
- Testcontainers owns destructive integration databases; the development `toystore` database is never reset by tests.

Update M2-02 and M2-04 task text in `TASKS.md` to match the approved two-role model. Leave their status in progress until final verification.

- [ ] **Step 4: Fix the service-owned key directory**

Change the unit to:

```ini
StateDirectory=toystore/logs toystore/keys
StateDirectoryMode=0750
ReadWritePaths=/var/lib/toystore/uploads /var/lib/toystore/keys /var/lib/toystore/logs
```

Keep uploads provisioned by deployment setup because uploaded media and release lifecycle differ from systemd state-directory creation.

- [ ] **Step 5: Verify GREEN**

Run the focused command from Step 2. Expected: all deployment/documentation contract tests pass.

---

## Task 2: Upgrade the test runner and create the isolated PostgreSQL harness

**Files:**

- Modify: `tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj`
- Modify: `tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj`
- Modify: `tests/ToyStore.UnitTests/Architecture/PackageDependencyTests.cs`
- Create: `tests/ToyStore.IntegrationTests/Infrastructure/PostgreSqlCollection.cs`
- Create: `tests/ToyStore.IntegrationTests/Infrastructure/PostgreSqlFixture.cs`
- Create: `tests/ToyStore.IntegrationTests/Infrastructure/ToyStoreWebApplicationFactory.cs`
- Create: `tests/ToyStore.IntegrationTests/Infrastructure/TestDatabaseSafetyTests.cs`
- Modify: `tests/ToyStore.IntegrationTests/ForwardedHeadersTests.cs`
- Modify later in Task 7: `tests/ToyStore.IntegrationTests/HealthEndpointTests.cs`

- [ ] **Step 1: Write a failing database safety test**

Define `PostgreSqlFixture.IsSafeTestDatabase(string connectionString)` and first write tests for these cases:

```csharp
[Theory]
[InlineData("Host=localhost;Database=toystore", false)]
[InlineData("Host=localhost;Database=postgres", false)]
[InlineData("Host=localhost;Database=template1", false)]
[InlineData("Host=localhost;Database=toystore_integration_test", true)]
public void ResetGuardOnlyAcceptsExplicitTestDatabase(string connectionString, bool expected)
```

Also assert an empty/malformed connection string is rejected. `ResetAsync()` must call this guard before opening a connection or Respawn.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj \
  --filter FullyQualifiedName~TestDatabaseSafetyTests
```

Expected: compilation fails because the fixture does not exist.

- [ ] **Step 3: Upgrade both test projects to stable xUnit v3**

In both test projects, replace:

```xml
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
```

with:

```xml
<PackageReference Include="xunit.v3" Version="3.2.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
```

In IntegrationTests add:

```xml
<PackageReference Include="Respawn" Version="7.0.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="4.13.0" />
```

Keep `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, and coverage packages. This solution continues to use the VSTest path for `dotnet test`; xUnit's v3 guidance requires the Visual Studio runner and Test SDK for that path. A move to Microsoft Testing Platform needs its own solution-level runner decision.

Update `PackageDependencyTests` to require the new exact versions and to reject direct `xunit` v2 references in both test projects. This keeps package choices executable rather than documenting them only in this plan.

- [ ] **Step 4: Implement the PostgreSQL collection fixture**

Use a single collection fixture with disabled parallelization for database-mutating tests:

```csharp
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL integration";
}
```

`PostgreSqlFixture` must:

- implement `IAsyncLifetime` and `IAsyncDisposable`;
- start `postgres:17-alpine` with database `toystore_integration_test`;
- expose the container connection string;
- create a Respawn checkpoint only after migrations have run;
- preserve `__EFMigrationsHistory` while deleting all application/Identity data;
- rerun the role initializer after reset so invariant roles exist;
- reject any database name that is not suffixed `_test` or `_integration_test`;
- stop/dispose the container in `DisposeAsync`.

Use `NpgsqlConnectionStringBuilder` for the guard. Never accept a safety override environment variable.

- [ ] **Step 5: Implement a reusable WebApplicationFactory**

`ToyStoreWebApplicationFactory` takes the fixture connection string and configures:

```text
Environment = Testing
ConnectionStrings:Database = fixture connection
DataProtection:KeysPath = unique temp directory
```

It must delete only its own generated temp Data Protection directory on disposal. Do not disable migrations in Testing; migration behavior is part of the system under test.

- [ ] **Step 6: Move forwarded-header tests onto the real database factory**

Derive the existing proxy factory from the reusable factory or compose its settings so the app can complete migration before testing proxy behavior. Preserve the existing loopback/untrusted proxy assertions.

- [ ] **Step 7: Verify the runner and safety guard GREEN**

Run:

```bash
dotnet restore ToyStore.sln
dotnet test tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj \
  --filter FullyQualifiedName~TestDatabaseSafetyTests
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj
```

Expected: the xUnit v3 runner discovers existing tests; guard tests pass; no deprecated xUnit v2 dependency remains in `dotnet list ToyStore.sln package`.

---

## Task 3: Define account and authorization contracts in Application

**Files:**

- Create: `src/ToyStore.Application/Common/Authorization/RoleNames.cs`
- Create: `src/ToyStore.Application/Common/Authorization/PolicyNames.cs`
- Create: `src/ToyStore.Application/Common/Interfaces/IIdentityService.cs`
- Create: `src/ToyStore.Application/Common/Interfaces/IUserContext.cs`
- Create: `src/ToyStore.Application/Accounts/AccountErrors.cs`
- Create: `src/ToyStore.Application/Accounts/RegisterCustomer/RegisterCustomerCommand.cs`
- Create: `src/ToyStore.Application/Accounts/RegisterCustomer/RegisterCustomerHandler.cs`
- Create: `src/ToyStore.Application/Accounts/RegisterCustomer/RegisterCustomerValidator.cs`
- Create: `src/ToyStore.Application/Accounts/Login/LoginCommand.cs`
- Create: `src/ToyStore.Application/Accounts/Login/LoginHandler.cs`
- Create: `src/ToyStore.Application/Accounts/Login/LoginResult.cs`
- Create: `src/ToyStore.Application/Accounts/ChangePassword/ChangePasswordCommand.cs`
- Create: `src/ToyStore.Application/Accounts/ChangePassword/ChangePasswordHandler.cs`
- Create: `src/ToyStore.Application/Accounts/ChangePassword/ChangePasswordValidator.cs`
- Create: `src/ToyStore.Application/Accounts/Logout/LogoutCommand.cs`
- Create: `src/ToyStore.Application/Accounts/Logout/LogoutHandler.cs`
- Create: `tests/ToyStore.UnitTests/Application/Accounts/AccountHandlerTests.cs`
- Create: `tests/ToyStore.UnitTests/Application/Accounts/AccountValidatorTests.cs`

- [ ] **Step 1: Write failing role, validator, and handler tests**

Test these observable contracts:

- roles are exactly `Customer` and `Admin`;
- all four management policies are constants and map to Admin authorization in Web later;
- email is required and valid;
- password is 8–100 characters;
- confirm password must match;
- register handler delegates to `IIdentityService.RegisterCustomerAsync`;
- login returns `Succeeded`, `InvalidCredentials`, or `LockedOut`, plus `MustChangePassword` on success;
- change-password requires the current user ID and returns an unauthorized failure when absent;
- handlers pass the MediatR cancellation token;
- expected failures remain `Result<T>` and do not throw.

Use these public shapes:

```csharp
public static class RoleNames
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public static readonly IReadOnlyList<string> All = [Customer, Admin];
}

public sealed record LoginResult(bool MustChangePassword);

public interface IIdentityService
{
    Task<Result<string>> RegisterCustomerAsync(string email, string password, CancellationToken cancellationToken);
    Task<Result<LoginResult>> PasswordSignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken);
    Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
    Task SignOutAsync();
}

public interface IUserContext
{
    string? UserId { get; }
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj \
  --filter "FullyQualifiedName~AccountHandlerTests|FullyQualifiedName~AccountValidatorTests"
```

Expected: compilation fails because account slices do not exist.

- [ ] **Step 3: Implement minimal vertical slices**

Create one handler per action. These remain commands by use-case semantics, but the Identity-backed commands implement `IRequest<Result...>` rather than the existing generic-transaction marker. Identity performs multiple internal saves and cookie side effects, so `IdentityService` owns its explicit EF transaction and commits before any cookie is issued. Document this narrow exception in the handler tests; ordinary application write slices continue to use `ICommand<T>` and the transaction pipeline. The Application handlers must contain orchestration only and must not reference ASP.NET Core Identity types.

Use stable Thai error contracts:

```csharp
public static class AccountErrors
{
    public static readonly Error InvalidCredentials =
        new("accounts.invalid_credentials", "อีเมลหรือรหัสผ่านไม่ถูกต้อง", ErrorType.Unauthorized);
    public static readonly Error LockedOut =
        new("accounts.locked_out", "บัญชีถูกล็อกชั่วคราว กรุณาลองใหม่ภายหลัง", ErrorType.Forbidden);
    public static readonly Error EmailAlreadyUsed =
        new("accounts.email_already_used", "อีเมลนี้ถูกใช้งานแล้ว", ErrorType.Conflict);
    public static readonly Error PasswordChangeFailed =
        new("accounts.password_change_failed", "ไม่สามารถเปลี่ยนรหัสผ่านได้ กรุณาตรวจสอบรหัสผ่านปัจจุบัน", ErrorType.Validation);
}
```

Do not include the attempted email or password in errors or logs.

- [ ] **Step 4: Verify Application GREEN and architecture boundaries**

Run the focused command again, then:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj \
  --filter FullyQualifiedName~ProjectDependencyTests
```

Expected: account tests and dependency tests pass; Application has no Infrastructure reference.

---

## Task 4: Implement Identity, role initialization, and Thai Identity errors

**Files:**

- Modify: `src/ToyStore.Infrastructure/Identity/ApplicationUser.cs`
- Create: `src/ToyStore.Infrastructure/Identity/IdentityService.cs`
- Create: `src/ToyStore.Infrastructure/Identity/IdentityInitializer.cs`
- Create: `src/ToyStore.Infrastructure/Identity/IIdentityInitializer.cs`
- Create: `src/ToyStore.Infrastructure/Identity/ThaiIdentityErrorDescriber.cs`
- Create: `src/ToyStore.Infrastructure/Identity/ToyStoreClaimsPrincipalFactory.cs`
- Create: `src/ToyStore.Infrastructure/Identity/IdentityClaimNames.cs`
- Modify: `src/ToyStore.Infrastructure/DependencyInjection.cs`
- Create: `tests/ToyStore.UnitTests/Infrastructure/IdentityRegistrationTests.cs`
- Test later in Task 7: `tests/ToyStore.IntegrationTests/Identity/IdentityServiceTests.cs`

- [ ] **Step 1: Write failing DI and option tests**

Assert `AddInfrastructure` registers:

- `IIdentityService` and `IIdentityInitializer` as scoped;
- `IdentityService` through the Application interface only;
- `ThaiIdentityErrorDescriber`;
- the custom claims-principal factory;
- Identity schema version 2 (email/password only; do not create passkey persistence);
- unique email, no confirmed-account requirement, 8-character minimum, digit/lower/upper requirements, no required symbol;
- five failed attempts and 15-minute lockout;
- cookie lifetime 14 days with sliding expiration.

- [ ] **Step 2: Run tests and verify RED**

Run:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj \
  --filter FullyQualifiedName~IdentityRegistrationTests
```

Expected: registration assertions fail.

- [ ] **Step 3: Extend the persisted Identity user**

Add:

```csharp
public bool MustChangePassword { get; set; }
```

to `ApplicationUser`. Keep the Infrastructure ownership of the Identity model.

- [ ] **Step 4: Configure Identity in Infrastructure**

Move `AddIdentityCore<ApplicationUser>` and EF store registration out of `Program.cs` into `AddInfrastructure`. Add roles, `SignInManager`, token providers needed by password operations, the Thai error describer, and the custom principal factory. Use `IdentitySchemaVersions.Version2` because passkeys are not a v1 feature.

Register authentication cookies in Web only; Infrastructure must not configure routes or Blazor services.

- [ ] **Step 5: Implement expected Identity behavior**

`IdentityService.RegisterCustomerAsync` must:

1. normalize the email through Identity by using it as both email and username;
2. create the user;
3. add the `Customer` role;
4. roll back the explicit EF transaction if role assignment fails, leaving no user to compensate later;
5. commit its explicit EF transaction before returning success;
6. translate duplicate email and password failures into Thai `Result` errors.

It must not issue the authentication cookie. After `RegisterCustomerCommand` returns success, `Register.razor` sends a separate `LoginCommand` with `rememberMe=false`. That second command can issue the cookie only after user creation and role membership are durable.

`PasswordSignInAsync` must call `PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true)`, map invalid and locked-out results without revealing which field was wrong, and load `MustChangePassword` only after success.

`ChangePasswordAsync` must change the password and set `MustChangePassword=false` within one explicit EF transaction. Commit first, then refresh the cookie principal. A failed current password rolls back and returns a Thai validation result; it is not a logged system exception.

- [ ] **Step 6: Add the force-change claim**

`ToyStoreClaimsPrincipalFactory` adds claim `toystore:must_change_password=true` only when the flag is true. Never put email, password, or other personal data into this custom claim.

- [ ] **Step 7: Implement idempotent role initialization**

`IdentityInitializer.SeedRolesAsync` iterates `RoleNames.All`, checks normalized role existence through `RoleManager`, and creates only missing roles. Treat failure to create a role as an exceptional startup failure with the role name but no sensitive data.

- [ ] **Step 8: Verify GREEN**

Run the focused Identity registration tests. Then run all UnitTests. Expected: all pass with warnings treated as errors.

---

## Task 5: Configure cookies, Data Protection, authorization, and Thai account UI

**Files:**

- Modify: `src/ToyStore.Web/Program.cs`
- Modify: `src/ToyStore.Web/Components/App.razor`
- Modify: `src/ToyStore.Web/Components/Routes.razor`
- Create: `src/ToyStore.Web/Identity/HttpUserContext.cs`
- Create: `src/ToyStore.Web/Identity/DataProtectionConfiguration.cs`
- Create: `src/ToyStore.Web/Components/Account/Pages/AdminProbe.razor`
- Modify: `src/ToyStore.Web/Components/Account/Pages/Register.razor`
- Modify: `src/ToyStore.Web/Components/Account/Pages/Login.razor`
- Modify: `src/ToyStore.Web/Components/Account/Pages/ForgotPassword.razor`
- Modify: `src/ToyStore.Web/Components/Account/Pages/AccessDenied.razor`
- Modify: `src/ToyStore.Web/Components/Account/Pages/Lockout.razor`
- Modify: `src/ToyStore.Web/Components/Account/Pages/InvalidUser.razor`
- Modify: `src/ToyStore.Web/Components/Account/Pages/Manage/ChangePassword.razor`
- Modify: `src/ToyStore.Web/Components/Account/Shared/ManageLayout.razor`
- Modify: `src/ToyStore.Web/Components/Account/Shared/ManageNavMenu.razor`
- Modify: `src/ToyStore.Web/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs`
- Delete: `src/ToyStore.Web/Components/Account/IdentityNoOpEmailSender.cs`
- Delete: `src/ToyStore.Web/Components/Account/PasskeyInputModel.cs`
- Delete: `src/ToyStore.Web/Components/Account/PasskeyOperation.cs`
- Delete: `src/ToyStore.Web/Components/Account/Shared/PasskeySubmit.razor`
- Delete: `src/ToyStore.Web/Components/Account/Shared/PasskeySubmit.razor.js`
- Delete: `src/ToyStore.Web/Components/Account/Shared/ExternalLoginPicker.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/ExternalLogin.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/ResendEmailConfirmation.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/RegisterConfirmation.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/ConfirmEmail.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/ConfirmEmailChange.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/ForgotPasswordConfirmation.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/InvalidPasswordReset.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/ResetPassword.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/ResetPasswordConfirmation.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/LoginWith2fa.razor`
- Delete: `src/ToyStore.Web/Components/Account/Pages/LoginWithRecoveryCode.razor`
- Delete: `src/ToyStore.Web/Components/Account/Shared/ShowRecoveryCodes.razor`
- Delete all generated pages under `src/ToyStore.Web/Components/Account/Pages/Manage/` except `ChangePassword.razor` and `_Imports.razor`
- Create: `tests/ToyStore.UnitTests/Web/IdentityCompositionTests.cs`
- Test later in Task 7: `tests/ToyStore.IntegrationTests/Identity/AccountEndpointTests.cs`

- [ ] **Step 1: Write failing composition tests**

Assert:

- `IUserContext` resolves to the Web HTTP implementation;
- the four management policies require role `Admin` and reject the force-change claim;
- Data Protection uses the configured path;
- a missing Data Protection path throws outside Development/Testing;
- `App.razor` declares `<html lang="th">` and has no passkey script;
- unsupported passkey/external-login endpoint patterns no longer exist.

- [ ] **Step 2: Run tests and verify RED**

Run:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj \
  --filter FullyQualifiedName~IdentityCompositionTests
```

- [ ] **Step 3: Configure cookies and authorization in Web**

Keep Identity cookie schemes in `Program.cs`. Configure secure defaults:

- `HttpOnly=true`;
- `SameSite=Lax`;
- `SecurePolicy=SameAsRequest` in Development/Testing and `Always` in Production;
- 14-day persistent lifetime with sliding expiration;
- `/Account/Login` and `/Account/AccessDenied` paths.

Register each management policy with `RequireRole(RoleNames.Admin)` and an assertion that claim `toystore:must_change_password` is not `true`. Add a customer-authenticated policy only when a concrete route needs it; ordinary account routes use `RequireAuthorization`.

- [ ] **Step 4: Configure persistent Data Protection keys**

`DataProtectionConfiguration` must read `DataProtection:KeysPath`, create the directory, call:

```csharp
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("ToyStore");
```

Use `.data/keys` as the Development default in `appsettings.Development.json`; Testing supplies a unique temp path; Production must explicitly configure the path or fail startup.

- [ ] **Step 5: Make the supported account pages Thai-first and command-driven**

Register/Login/ChangePassword use `ISender.Send(...)`; they do not inject `ApplicationDbContext`, `UserManager`, or `SignInManager`. Translate page title, heading, labels, placeholders, validation messages, loading/disabled text, and results to Thai. Keep technical proper nouns such as email in natural Thai copy.

Required login copy/behavior:

- fields `อีเมล`, `รหัสผ่าน`;
- checkbox `จดจำฉัน`;
- submit `เข้าสู่ระบบ`;
- disabled control text `ลืมรหัสผ่าน (ยังไม่เปิดใช้งาน)`;
- link `สมัครสมาชิก`;
- no passkey, external-login, confirmation-resend, or 2FA choices;
- successful forced-change Admin goes to `/Account/Manage/ChangePassword`; other success honors only a safe local return URL;
- invalid login uses the generic Thai error from `AccountErrors`.

Required registration copy/behavior:

- email, password, confirm password;
- FluentValidation เป็นกติกาหลักใน Application และ Web แสดง Thai field messages/summary โดย presentation hints ต้องไม่ขัดกับ validator;
- no confirmation email generation;
- successful Customer creation first commits, then sends a separate `LoginCommand`; only successful sign-in redirects to a safe local return URL or `/`.

The forgot-password route remains a Thai informational page with no form or active submission.

- [ ] **Step 6: Reduce the generated Identity surface**

Remove passkey and external-login endpoints, imports, scripts, components, and links. Remove generated email confirmation, reset password, profile/email management, personal-data management, external-login, passkey, and 2FA pages because none is an approved v1 UI flow. Remove the no-op email sender and its registration. Keep only Register, Login, disabled ForgotPassword, Lockout, AccessDenied, InvalidUser, and ChangePassword account pages. Simplify `MapAdditionalIdentityEndpoints` to an antiforgery-protected local logout POST that sends `LogoutCommand` and accepts only a local return URL.

Do not delete password-reset token APIs from Identity itself; only the unapproved UI flow is disabled so a provider-neutral email implementation can enable it later.

- [ ] **Step 7: Add a protected test route**

Create `/account/admin-probe` with `[Authorize(Policy = PolicyNames.CanManageProducts)]` and Thai content. Mark it clearly as a temporary M2 verification route and remove it when the real Admin shell route lands in M5. It provides an observable protected-policy target for integration tests without coupling the tests to future UI.

- [ ] **Step 8: Verify GREEN and scan for English account copy**

Run:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj \
  --filter FullyQualifiedName~IdentityCompositionTests
rg -n "Log in|Register|Forgot your|Remember me|Passkey|External login|Two-factor" \
  src/ToyStore.Web/Components/Account src/ToyStore.Web/Components/App.razor
```

Expected: composition tests pass and `rg` returns no user-facing English/unsupported feature matches. Class names such as `Login` are allowed only if the search output is code identifiers rather than rendered copy.

---

## Task 6: Add explicit first-Admin bootstrap and forced password change

**Files:**

- Create: `src/ToyStore.Application/Common/Interfaces/IAdminBootstrapper.cs`
- Create: `src/ToyStore.Application/Accounts/BootstrapAdmin/BootstrapAdminResult.cs`
- Create: `src/ToyStore.Infrastructure/Identity/AdminBootstrapper.cs`
- Create: `src/ToyStore.Web/Identity/AdminBootstrapCommand.cs`
- Modify: `src/ToyStore.Infrastructure/DependencyInjection.cs`
- Modify: `src/ToyStore.Web/Program.cs`
- Modify: `docs/LOCAL_DEVELOPMENT.md`
- Modify: `docs/DEPLOYMENT.md`
- Create: `tests/ToyStore.UnitTests/Web/AdminBootstrapCommandTests.cs`
- Create: `tests/ToyStore.IntegrationTests/Identity/AdminBootstrapperTests.cs`

- [ ] **Step 1: Write failing parser and bootstrap tests**

Cover:

- the command runs only when the exact argument `--bootstrap-admin` is present;
- email and temporary password come from `BootstrapAdmin:Email` and `BootstrapAdmin:TemporaryPassword`;
- missing configuration fails without echoing either value;
- an empty Admin role creates one Admin with `MustChangePassword=true`;
- a repeat attempt creates no duplicate and returns a typed conflict result;
- an existing non-admin account with the same email is not promoted implicitly;
- role-assignment failure rolls back the explicit transaction so no partial user remains;
- logs never contain a sentinel password.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj \
  --filter FullyQualifiedName~AdminBootstrapCommandTests
```

- [ ] **Step 3: Implement bootstrap through an Application interface**

`IAdminBootstrapper.CreateFirstAdminAsync(email, temporaryPassword, cancellationToken)` returns a result containing only the created user ID. Infrastructure uses `UserManager`/`RoleManager`, requires the seeded Admin role, refuses to run when any Admin already exists, creates the account with the force-change flag, assigns only the Admin role, and wraps creation/membership in an explicit EF transaction that rolls back on failure.

- [ ] **Step 4: Add the non-listening command path**

After `builder.Build()` and before endpoint mapping/listening, Program must:

1. apply migrations;
2. seed roles;
3. if `--bootstrap-admin` is present, resolve configured credentials, call the bootstrapper, set a nonzero exit code on failure, and return without `app.Run()`;
4. otherwise continue normal startup.

Never accept the password as a CLI argument because process arguments can be inspected by other users.

- [ ] **Step 5: Document safe local and production usage**

Local example:

```bash
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project src/ToyStore.Web
dotnet user-secrets set "BootstrapAdmin:TemporaryPassword" "CHANGE_THIS_NOW" --project src/ToyStore.Web
dotnet run --project src/ToyStore.Web -- --bootstrap-admin
dotnet user-secrets remove "BootstrapAdmin:TemporaryPassword" --project src/ToyStore.Web
```

Production uses protected environment configuration for the one command invocation, removes the temporary secret immediately afterward, and then starts the normal systemd service. State clearly that the Admin must change the password at first login.

- [ ] **Step 6: Verify GREEN**

Run parser tests and `AdminBootstrapperTests`. Query the test database to assert exactly one Admin user and one role membership after the repeat attempt.

---

## Task 7: Create and apply the initial Code First migration at startup

**Files:**

- Create via EF CLI: `src/ToyStore.Infrastructure/Persistence/Migrations/*_InitialIdentity.cs`
- Create via EF CLI: `src/ToyStore.Infrastructure/Persistence/Migrations/*_InitialIdentity.Designer.cs`
- Create via EF CLI: `src/ToyStore.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
- Create: `src/ToyStore.Web/Startup/DatabaseStartupExtensions.cs`
- Modify: `src/ToyStore.Web/Program.cs`
- Create: `tests/ToyStore.IntegrationTests/Persistence/StartupMigrationTests.cs`
- Create: `tests/ToyStore.IntegrationTests/Identity/IdentityServiceTests.cs`
- Create: `tests/ToyStore.IntegrationTests/Identity/AccountEndpointTests.cs`
- Rewrite: `tests/ToyStore.IntegrationTests/HealthEndpointTests.cs`

- [ ] **Step 1: Write failing startup migration tests before generating the migration**

Test on throwaway PostgreSQL databases:

1. empty database startup creates `__EFMigrationsHistory` and expected Identity tables;
2. a second startup against the existing database adds no migration row and does not duplicate roles;
3. a database with a deliberately conflicting `AspNetUsers` table causes startup to fail before a client can be created;
4. an unreachable database causes startup to fail rather than serve `/health/live`;
5. no source file calls `EnsureCreated` or `EnsureCreatedAsync`.

Expected tables include Identity v2 tables plus the `MustChangePassword` column. Do not expect Product, Order, Cart, or other business tables in this migration.

- [ ] **Step 2: Run tests and verify RED**

Run:

```bash
dotnet test tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj \
  --filter FullyQualifiedName~StartupMigrationTests
```

Expected: compilation/startup fails because migration orchestration and migration files do not exist.

- [ ] **Step 3: Generate the migration from code**

Ensure local PostgreSQL/configuration is available, then run exactly:

```bash
dotnet ef migrations add InitialIdentity \
  --project src/ToyStore.Infrastructure \
  --startup-project src/ToyStore.Web \
  --output-dir Persistence/Migrations
```

Do not hand-edit the model snapshot to simulate a migration. Review the generated `Up`, `Down`, designer, and snapshot together.

- [ ] **Step 4: Implement startup migration orchestration**

`DatabaseStartupExtensions.ApplyDatabaseMigrationsAsync` creates an async scope, resolves `ApplicationDbContext`, and calls `Database.MigrateAsync(cancellationToken)`. It logs start/success through source-generated logging and lets connection/migration exceptions escape after logging safe structural context. Do not swallow, retry forever, call `EnsureCreated`, or expose the operation through HTTP.

Program order must be:

```text
Build app
Apply Database.MigrateAsync
Seed Customer/Admin roles
Optionally execute bootstrap-admin and exit
Configure middleware/map endpoints
Run/listen
```

- [ ] **Step 5: Generate and review idempotent SQL**

Run:

```bash
mkdir -p artifacts/migrations
dotnet ef migrations script --idempotent \
  --project src/ToyStore.Infrastructure \
  --startup-project src/ToyStore.Web \
  --output artifacts/migrations/InitialIdentity.sql
```

Review with:

```bash
rg -n "CREATE TABLE|CREATE INDEX|ALTER TABLE|DROP TABLE|DROP COLUMN|TRUNCATE" \
  artifacts/migrations/InitialIdentity.sql
```

Expected: Identity tables/indexes and `MustChangePassword`; no Product/Order tables; no destructive `DROP`, `TRUNCATE`, or unrelated alterations in the Up path. The SQL file is a review artifact, not a substitute for committed migration source.

- [ ] **Step 6: Add real Identity behavior tests**

Against the migrated Testcontainer verify:

- registering creates one user in Customer role and an authentication cookie;
- duplicate normalized email returns Thai conflict and no second user;
- incorrect password increments access-failure count and five failures lock the account;
- RememberMe produces a persistent cookie while unchecked login produces a session cookie;
- logout invalidates access to the protected route;
- Customer receives forbidden/redirected access to Admin probe;
- Admin with `MustChangePassword=true` is redirected to change password and denied Admin policy;
- successful password change clears the database flag, refreshes the claim, and permits Admin policy;
- all rendered account labels/errors involved in the tests are Thai.

Exercise account HTTP/Razor endpoints where cookie/redirect behavior matters and call `ISender`/services only for focused persistence cases.

- [ ] **Step 7: Rewrite outage health tests for the migration startup contract**

The old test starts with an unavailable database, which now correctly prevents startup. Replace it with two cases:

1. unavailable at startup => factory startup throws and no endpoint is served;
2. start successfully on a dedicated Testcontainer, stop that container after the client exists, then `/health/live` remains 200 while `/health/ready` and `/health` return 503.

Use a dedicated container for the outage test so stopping it cannot break the shared collection.

- [ ] **Step 8: Verify migration and Identity GREEN**

Run:

```bash
dotnet test tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj \
  --filter "FullyQualifiedName~StartupMigrationTests|FullyQualifiedName~IdentityServiceTests|FullyQualifiedName~AccountEndpointTests|FullyQualifiedName~HealthEndpointTests"
```

Expected: all focused integration tests pass on disposable PostgreSQL; no test connects to `Database=toystore`.

---

## Task 8: Full verification, code review, and task handoff

**Files:**

- Modify: `TASKS.md`
- Modify if review finds drift: files changed by Tasks 1–7 only

- [ ] **Step 1: Run formatting and static checks**

Run:

```bash
dotnet format ToyStore.sln --verify-no-changes
dotnet build ToyStore.sln --no-restore -p:CI=true
rg -n "EnsureCreated|IdentitySchemaVersions.Version3|RequireConfirmedAccount = true|RoleNames\.Staff|\"Staff\"" \
  src tests docs TASKS.md
```

Expected: format/build pass; prohibited implementation matches are absent. Historical prose in an archived plan is allowed only after manual confirmation that it is not active source of truth.

- [ ] **Step 2: Run focused tests, then the full suite**

Run:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --no-build
dotnet test tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj --no-build
dotnet test ToyStore.sln --no-build
docker compose config
```

Expected: every test passes; Docker is required for IntegrationTests; Compose remains PostgreSQL-only.

- [ ] **Step 3: Perform manual startup smoke tests**

With local PostgreSQL running and user secrets configured:

```bash
docker compose up -d postgres
dotnet run --project src/ToyStore.Web
```

Verify from logs that migration and role initialization complete before the listening message. Then verify:

```bash
curl -i http://localhost:<port>/health/live
curl -i http://localhost:<port>/health/ready
```

Use the actual launch-profile port. Register a disposable Customer through the UI, log out, log in with RememberMe, and confirm the Thai UI and protected Admin denial.

- [ ] **Step 4: Request a code review before completion**

Invoke `superpowers:requesting-code-review` with this plan and the approved master design. The review must examine:

- dependency direction and Razor `ISender` usage;
- cookie/Data Protection safety;
- role/policy enforcement and forced password change;
- user/role compensation on partial failure;
- migration startup ordering and failure behavior;
- database-test isolation and reset guard;
- secrets and personal data in logs;
- Thai-first rendered copy and accessible labels;
- migration SQL for destructive/unrelated changes.

Fix all Critical/High findings with a new RED/GREEN cycle. Record Medium/Low findings in `TASKS.md` only when intentionally deferred with rationale.

- [ ] **Step 5: Re-run verification after review fixes**

Repeat Step 1 and Step 2 after the final code change. Do not rely on results from before review fixes.

- [ ] **Step 6: Update delivery status only after evidence is green**

In `TASKS.md`:

- mark M1-R05 resolved because confirmed-account registration remains disabled and no dead no-op mail sender controls registration;
- mark M2-02 through M2-05 completed and include concise verification evidence;
- set Current Focus to `M3-01 Self-host Noto Sans Thai` and Next task to `M3-02 Create design tokens and responsive foundation`;
- add any intentional review follow-up inline with severity and owner.

Completion means: Customer register/login/logout works in Thai, Admin authorization is enforced server-side, first Admin creation is explicit and safe, cookies survive restart through persistent keys, migrations are Code First and run before listening, and destructive tests cannot target the development database.

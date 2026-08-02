# Authentication Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local ASP.NET Core 10 Identity + OpenIddict authentication
server with PostgreSQL, passkeys and native-client PKCE contracts.

**Architecture:** The API is a modular-monolith host. Identity owns
users/passkeys and OpenIddict owns OIDC authorization/token persistence in the
same EF Core context; protected application endpoints validate OpenIddict tokens
locally. Native clients are added only after the server contract is proven.

**Tech Stack:** C# 14, .NET 10, ASP.NET Core Identity passkeys, OpenIddict 7.x,
EF Core 10, Npgsql, PostgreSQL, Docker Compose, xUnit, WebApplicationFactory.

## Global Constraints

- Passkey operations require HTTPS; local RP ID is `localhost` and production RP
  ID remains unset.
- Native clients are public clients; no client secret is accepted or stored.
- Authorization Code + PKCE with S256 is mandatory for native clients.
- Access tokens live 5 minutes; refresh tokens live 30 days with rolling
  rotation.
- Tokens, passkey payloads, codes and private keys never enter logs, URLs,
  screenshots or committed files.
- Development-only keys and data are never reused in production configuration.
- Follow RED-GREEN-REFACTOR for every behavioral code change.

---

### Task 1: Create the .NET solution and local PostgreSQL boundary

**Files:**

- Create: `Andivum.slnx`
- Create: `services/api/Andivum.Api/Andivum.Api.csproj`
- Create: `services/api/Andivum.Api/Program.cs`
- Create: `services/api/Andivum.Api/appsettings.json`
- Create: `services/api/Andivum.Api/appsettings.Development.json`
- Create: `services/api/Andivum.Api.Tests/Andivum.Api.Tests.csproj`
- Create: `services/api/Andivum.Api.Tests/HealthEndpointTests.cs`
- Create: `infra/compose/docker-compose.yml`
- Modify: `package.json`

**Steps:**

- [x] Write a failing health integration test using
      `WebApplicationFactory<Program>` that expects `GET /health` to return 200
      and JSON `{ "status": "ok" }`.
- [x] Run `dotnet test services/api/Andivum.Api.Tests` and confirm failure
      because the solution/projects do not exist.
- [x] Create the minimal ASP.NET Core Web API project, health endpoint and test
      project; keep the test host free of PostgreSQL for this first check.
- [x] Add Compose PostgreSQL 17 with a named local volume, healthcheck,
      non-default database/user values from environment, and no host-wide data
      mounts.
- [x] Add root scripts `api:build`, `api:test`, `dev:infra` and `dev:api` using
      `pnpm`.
- [x] Run the focused test, then `dotnet test` and `pnpm check`; all must pass.
- [x] Commit `feat: создать основу API и локальной БД`.

### Task 2: Add Identity/OpenIddict persistence and migrations

**Files:**

- Create: `services/api/Andivum.Api/Data/ApplicationUser.cs`
- Create: `services/api/Andivum.Api/Data/ApplicationDbContext.cs`
- Create: `services/api/Andivum.Api/Data/DesignTimeDbContextFactory.cs`
- Create: `services/api/Andivum.Api/Migrations/*`
- Create: `dotnet-tools.json`
- Modify: `services/api/Andivum.Api/Program.cs`, `*.csproj`,
  `appsettings.Development.json`
- Create: `services/api/Andivum.Api.Tests/DatabaseSchemaTests.cs`

**Steps:**

- [x] Write a failing schema test that resolves `ApplicationDbContext` and
      verifies Identity and OpenIddict tables can be created against the test
      database.
- [x] Run the focused test and confirm the context/service registrations are
      missing.
- [x] Add EF Core PostgreSQL, Identity and OpenIddict EF packages; configure one
      context for Identity and OpenIddict stores.
- [x] Add the initial migration and a design-time factory reading
      `ConnectionStrings:Postgres`.
- [x] Run migration against Compose PostgreSQL and the schema integration test.
- [x] Run `dotnet test` and `pnpm check`.
- [x] Commit `feat: добавить хранилище Identity и OpenIddict`.

### Task 3: Enforce passkey and native-client policies

**Files:**

- Create: `services/api/Andivum.Api/Identity/IdentityOptionsSetup.cs`
- Create: `services/api/Andivum.Api/Identity/NativeClientRegistry.cs`
- Create: `services/api/Andivum.Api/Identity/AuthPolicy.cs`
- Create: `services/api/Andivum.Api.Tests/AuthPolicyTests.cs`
- Modify: `Program.cs`, `config/product.json`

**Steps:**

- [x] Write tests for explicit RP ID/origin, 64-character display-name limit,
      20-passkey limit, exact redirect URI matching, public-client rejection of
      secrets, and S256-only PKCE.
- [x] Run the focused tests and confirm the policy types are absent.
- [x] Implement minimal policy services and configure `IdentityPasskeyOptions`
      with development `localhost` RP settings only when the environment is
      Development.
- [x] Register `andivum-windows` and `andivum-android` as public clients with
      exact development redirect URIs; do not put secrets in source.
- [x] Run focused tests and then the full test suite.
- [x] Deliver the policy layer together with the OIDC auth-flow commit below.

### Task 4: Configure OIDC authorization and token endpoints

**Files:**

- Create: `services/api/Andivum.Api/Identity/AuthorizationController.cs`
- Create: `services/api/Andivum.Api/Identity/PasskeyEndpoints.cs`
- Create: `services/api/Andivum.Api/Identity/NativeClientSeeder.cs`
- Create: `services/api/Andivum.Api.Tests/OpenIddictFlowTests.cs`
- Modify: `Program.cs`, `appsettings*.json`

**Steps:**

- [x] Write integration tests that discovery returns 200, anonymous protected
      endpoint returns 401, unknown redirect URI returns an OAuth error, and
      PKCE-less authorization is rejected.
- [x] Define the missing endpoint contract with integration tests and run it
      during implementation.
- [x] Configure OpenIddict server discovery, authorization-code and token
      endpoints, S256 PKCE enforcement, development-only credentials and EF
      stores.
- [x] Add passkey request-options and assertion endpoints using ASP.NET Core
      Identity `SignInManager` APIs; keep ceremony data out of logs.
- [x] Add `/api/v1/session` with a stable authenticated user response and
      OpenIddict validation.
- [x] Run integration tests against isolated PostgreSQL and verify no token
      material is emitted to test output.
- [x] Commit `feat: реализовать OIDC auth flow с passkeys` вместе с
      runtime-проверками PKCE/public-client и CSRF, защитой auto-migration и
      production-конфигурацией ключей OpenIddict.

### Task 5: Add API contract, local HTTPS docs and security verification

**Files:**

- Create: `contracts/openapi/andivum-auth.yaml`
- Create: `docs/AUTHENTICATION.md`
- Create: `docs/adr/0008-authentication-slice.md`
- Create: `services/api/Andivum.Api.Tests/SecretLeakTests.cs`
- Modify: `README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`,
  `config/product.json`

**Steps:**

- [ ] Write a test that scans generated logs/config samples for access-token,
      refresh-token, private-key and client-secret literals.
- [ ] Run it and confirm the contract/documentation files are absent.
- [ ] Add OpenAPI paths for discovery-related metadata, session and native
      redirect contract; document browser passkey ceremony separately from
      native API clients.
- [ ] Document local HTTPS certificate setup, Compose startup, migrations, test
      commands and exact deferred real-device checks.
- [ ] Run security-focused checks, `dotnet test`, `pnpm check`,
      `git diff --check` and an independent review.
- [ ] Commit `docs: описать authentication slice` and push after CI passes.

## Verification commands

```powershell
docker compose -f infra/compose/docker-compose.yml up -d
dotnet test services/api/Andivum.Api.Tests
pnpm check
git diff --check
```

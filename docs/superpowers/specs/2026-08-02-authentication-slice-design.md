# Authentication Slice Design

## Goal

Создать первый работающий сквозной authentication slice Andivum для Windows и
Android: локальный ASP.NET Core 10 backend с PostgreSQL, passkey ceremony через
системный браузер, OIDC Authorization Code + PKCE и защищённый endpoint,
доступный после входа.

## Scope

В этот срез входят:

- `services/api/Andivum.Api` — ASP.NET Core 10 modular monolith host;
- `modules/identity` — Identity user model, passkeys, account/session policy;
- PostgreSQL в локальном Docker Compose;
- ASP.NET Core Identity passkey registration/sign-in pages and endpoints;
- OpenIddict authorization server with discovery, authorization and token
  endpoints;
- public native clients `andivum-windows` and `andivum-android` without client
  secrets, PKCE mandatory;
- a protected `/api/v1/session` endpoint;
- API integration tests and deterministic unit tests for auth policies;
- local HTTPS development certificate and documented localhost setup.

В этот срез не входят Google, Steam, семейные пространства, MFA/TOTP, сложный
account recovery, offline-first synchronization, production hosting и финальные
native UI clients. Native client shells подключаются следующим вертикальным
срезом после подтверждения серверного протокола.

## Chosen architecture

Identity и OpenIddict используют один EF Core context и PostgreSQL. Identity
отвечает за пользователя, passkey credentials и browser session; OpenIddict
отвечает за OIDC discovery, authorization codes, access/refresh tokens и
revocation. API validation использует локальный OpenIddict server configuration.

Passkey UI живёт на auth domain в server-rendered web surface только как
authentication surface. Это не web client продукта: нативные приложения всегда
открывают её в системной authentication session.

Native apps запрашивают только authorization code. Они не передают client
secret, используют S256 PKCE и сохраняют токены только в Windows Credential
Locker или Android Keystore-backed storage в последующих client tasks.

## Local development contract

- API: `https://localhost:7240`.
- Local passkey RP ID: `localhost`.
- Production RP ID остаётся unset до выбора реального HTTPS-домена.
- Development signing/encryption keys допустимы только в Development и не
  коммитятся.
- PostgreSQL: container `andivum-postgres`, database `andivum`.
- Test data is isolated by database/container and never uses real personal or
  financial data.

## Authentication flows

### Registration

1. Native app opens the system browser to `/connect/authorize` with client ID,
   redirect URI, `response_type=code`, `scope=openid profile offline_access`,
   random state and S256 PKCE challenge.
2. Server renders the passkey registration/sign-in surface.
3. ASP.NET Core Identity creates the user and verifies the WebAuthn attestation.
4. The browser returns an authorization code to the native callback.
5. Native client redeems the code at `/connect/token` with the verifier.

### Sign-in

1. Server issues a fresh passkey request challenge.
2. Browser invokes the platform authenticator through WebAuthn.
3. Identity verifies the assertion, challenge, origin, RP ID, user presence and
   signature counter.
4. OpenIddict issues a short-lived access token and a rotating refresh token.

### Session protection

- Access token lifetime: 5 minutes.
- Refresh token lifetime: 30 days, rolling rotation enabled.
- Refresh token reuse invalidates the token family.
- Logout revokes the current authorization/token family where supported.
- Account-wide logout is a later explicit management operation, not silently
  implied by local logout.

## Security invariants

- Production passkey operations require HTTPS.
- RP ID and allowed origins are explicit configuration, never trusted from an
  arbitrary Host header.
- Authorization requests require registered client ID and exact redirect URI.
- PKCE S256 is required for every native client; plain PKCE is rejected.
- No native client secret is accepted as a substitute for PKCE.
- State is generated and validated by the native client; the server never treats
  a redirect URI as a state substitute.
- Access and refresh tokens never appear in logs, test output, URLs or
  screenshots.
- Passkey display names are limited to 64 Unicode scalar values and each user
  has at most 20 active passkeys in this first slice.
- Every protected endpoint returns 401 for a missing/invalid token and never
  leaks whether another user's resource exists.
- Development-only bypasses are forbidden, including fixed test users and
  backdoors reachable from a production configuration.

## Errors and observability

Auth failures use RFC 9457 Problem Details with stable `type` and `code` values;
they do not disclose whether an email/account exists. Structured logs include
correlation ID, client ID, endpoint and outcome, but never credential material,
tokens, passkey payloads or raw authorization codes.

Rate limiting and account recovery are explicitly deferred, but the interfaces
must leave room for them. An auth attempt ID may be logged as a non-secret
correlation value.

## Testing strategy

- Unit tests first for redirect validation, PKCE policy, passkey limits, token
  lifetime configuration and error redaction.
- API integration tests with `WebApplicationFactory` and a disposable PostgreSQL
  test database/container verify discovery, client registration, protected
  endpoint authorization and invalid-token behavior.
- Browser/Computer Use smoke test later verifies the real passkey page on
  Windows Hello; Android emulator and physical-device passkey tests remain
  environment dependent.
- No test stores or prints actual private keys, tokens, recovery codes or real
  user data.

## Definition of done

- `dotnet test` and `pnpm check` pass;
- PostgreSQL starts with one documented command and migrations apply cleanly;
- OIDC discovery is reachable over local HTTPS;
- an unregistered client/redirect URI is rejected;
- PKCE-less and plain-PKCE requests are rejected;
- protected session endpoint rejects anonymous access;
- a real browser passkey smoke flow is either green or explicitly recorded as
  environment-deferred with the exact missing prerequisite;
- docs, ADR and configuration are updated and delivered through CI.

## References

- [ASP.NET Core Identity passkeys](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)
- [OpenIddict ASP.NET Core integration](https://documentation.openiddict.com/integrations/aspnet-core)
- [OpenIddict PKCE enforcement](https://documentation.openiddict.com/configuration/proof-key-for-code-exchange)

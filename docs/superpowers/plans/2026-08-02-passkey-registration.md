# Passkey registration implementation plan

## Goal

Complete the first passwordless registration path while preserving the existing
OIDC + PKCE and passkey security invariants.

## Tasks

- [x] Add failing policy/integration tests for the registration surface and for
      refusing authorization before the first passkey is stored.
- [x] Add the minimal registration endpoint and signed-in pending browser state.
- [x] Gate `/connect/authorize` on at least one stored passkey.
- [x] Add registration controls to the auth surface.
- [ ] Add localized status strings to the auth surface.
- [ ] Run API tests, Android/Windows builds, and available device smoke checks.
- [x] Update authentication docs and roadmap with the exact completed/deferred
      behavior.

## Constraints

- Keep the backend modular-monolith boundary.
- Do not introduce a provider-specific hosting dependency.
- Do not store secrets or real user data in fixtures.
- Follow Red-Green-Refactor for production behavior.

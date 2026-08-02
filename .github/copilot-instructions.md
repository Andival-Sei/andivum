# Andivum repository instructions

`AGENTS.md` is the canonical instruction file. Read and follow it before making
changes. Then read `PROJECT_CONTEXT.md`, `docs/DEVELOPMENT_PROCESS.md`,
`docs/ARCHITECTURE.md`, and the relevant ADR/spec/plan.

Key rules:

- Windows is native C#/.NET/WinUI 3; Android is native Kotlin/Jetpack Compose.
- Production behavior is developed test-first using Red-Green-Refactor.
- Use root `pnpm` commands as the repository command facade.
- Do not edit generated OpenAPI clients manually.
- Do not hardcode the product display name.
- Never commit secrets or use embedded WebViews for authentication.
- Report which Windows, Android, API, and static checks actually ran.
- Use Conventional Commits written in Russian.

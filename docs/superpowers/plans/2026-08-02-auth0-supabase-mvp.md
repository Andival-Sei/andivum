# План: Auth0 + Supabase для первого облачного MVP

- Связанная spec: `docs/superpowers/specs/2026-08-02-auth0-supabase-mvp.md`
- Ветка: `agent/auth0-supabase`
- Статус: локальный каркас завершён; внешний smoke ожидает настройки провайдеров

## Задачи

### 1. Зафиксировать архитектуру и границы identity/data — готово

- Изменяемые файлы: ADR, `PROJECT_CONTEXT.md`, `docs/ARCHITECTURE.md`,
  `docs/AUTHENTICATION.md`, `docs/DECISIONS.md`, `docs/ROADMAP.md`.
- RED: не применимо, документационное/архитектурное решение.
- GREEN: документы одинаково описывают Auth0 как identity source и Supabase как
  data source, без второй системы пользователей.
- REFACTOR: убрать устаревшие формулировки про production OpenIddict, сохранив
  локальный fallback как временную dev-деталь.
- Проверка: `pnpm check:foundation` и поиск старых contradictory claims.

### 2. Добавить воспроизводимую Supabase schema/RLS основу — готово локально

- Изменяемые файлы: `supabase/config.toml`, `supabase/migrations/*` и
  SQL-проверки.
- RED: тест/проверка должна падать, если у profile table нет RLS или policy
  сравнивает email вместо Auth0 subject.
- GREEN: `app_profiles` и owner policies разрешают только текущему JWT subject.
- REFACTOR: вынести повторяющийся subject predicate в безопасную SQL-функцию
  только после подтверждения поведения интеграционным тестом.
- Проверка: Supabase CLI local migration/lint и SQL RLS test после появления
  Docker/local Supabase или подключённого dev project.

### 3. Перевести native OIDC configuration на Auth0 без секретов — готово

- Изменяемые файлы: Windows/Android auth configuration, tests, developer
  `.env.example` и root command facade.
- RED: unit tests проверяют, что production profile с пустым Auth0 issuer/client
  ID завершается понятной configuration error, а не использует случайный issuer.
- GREEN: при заданных `AUTH0_DOMAIN`, client IDs и redirect URIs оба клиента
  используют Auth0 discovery, `S256` PKCE и system browser; local profile может
  явно использовать OpenIddict fallback.
- REFACTOR: устранить hardcoded `andivum-windows`/`andivum-android` из runtime.
- Проверка: `pnpm windows:test`, Android unit/instrumentation tests, затем
  live smoke после настройки tenant.

### 4. Добавить direct Supabase profile/session client — готово локально

- Изменяемые файлы: native data clients, profile DTO/contract tests,
  configuration validation.
- RED: клиентский тест должен падать, если запрос использует service-role key,
  не передаёт JWT или пытается считать profile owner из email.
- GREEN: клиент отправляет publishable key + Auth0 ID token, bootstrap-ит
  profile по `sub` и обрабатывает 401/403 без обхода RLS.
- REFACTOR: общий HTTP policy внутри каждой native платформы без общего runtime.
- Проверка: mocked HTTP tests, Supabase SQL integration test, physical Android и
  Windows smoke.

### 5. Закрыть старую production auth boundary и обновить документацию — готово

- Изменяемые файлы: API fallback flags, deployment docs, development log,
  roadmap и security notes.
- RED: проверка должна выявлять запуск production без внешнего issuer или с
  ephemeral signing keys.
- GREEN: production profile не поднимает OpenIddict fallback; локальный dev
  profile остаётся явно обозначенным.
- REFACTOR: удалить obsolete auth UI только после green external smoke и
  отдельного решения о судьбе backend проекта.
- Проверка: `pnpm check`, `pnpm api:build`, `pnpm windows:build`, Android build,
  `git diff --check`.

## Общие проверки

```powershell
pnpm check
pnpm test
git diff --check
```

## Review

- [x] Соответствие spec
- [x] Нет лишнего scope
- [x] Security threat model для repository сохранён в scan artifacts
- [ ] RLS/identity threat review завершён на реальном Supabase project
- [ ] Platform QA отражает реально выполненные проверки

## Доставка

- [x] Commit
- [x] Push
- [x] CI
- [x] Draft PR
- [x] HEAD синхронизирован с remote

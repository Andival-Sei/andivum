# План: модуль финансов

- Связанная spec: `docs/superpowers/specs/2026-08-03-finance-module.md`
- Ветка: `agent/finance-module`
- Статус: in-progress

## Задачи

### 1. Финансовые инварианты и контракты

- Изменяемые файлы: `modules/specs/finance/*`, `services/api/Andivum.Api/Finance/*`.
- RED: тесты отвергают нулевую транзакцию, несовпадение суммы строк, неверную
  валюту и категорию не того типа.
- GREEN: валидатор денег, JSON Schema и golden cases.
- Проверка: `dotnet test services/api/Andivum.Api.Tests/Andivum.Api.Tests.csproj`.

### 2. Supabase schema и seed

- Изменяемые файлы: `supabase/migrations/*finance*.sql`,
  `tools/scripts/finance-migration.test.mjs`.
- RED: статическая проверка не находит таблицы, RLS, RPC и минимальный набор
  категорий.
- GREEN: миграция, политики и атомарная `finance_create_transaction`.
- Проверка: `pnpm test`, локальный `supabase db reset` при наличии Docker.

### 3. Windows data layer и Finance surface

- Изменяемые файлы: `apps/windows/Finance/*`, `MainPage.xaml*`, view model,
  resources и Windows tests.
- RED: тесты строят правильный REST/RPC request и проверяют duplicate/error
  handling.
- GREEN: загрузка, ручной ввод, file picker, preview draft, настройки Gemini.
- Проверка: `dotnet test apps/windows.tests/Andivum.Windows.Tests.csproj` и
  `pnpm windows:build`.

### 4. Android data layer и Finance surface

- Изменяемые файлы: `apps/android/.../Finance/*`, `MainActivity.kt`, resources,
  Gradle catalog и instrumentation tests.
- RED: тесты проверяют JSON-сериализацию, minor units и Gemini schema.
- GREEN: NavigationBar, список, ручной ввод, file picker, локальный OCR hook,
  preview draft и secure key settings.
- Проверка: `pnpm android:build`; instrumentation — только при доступном
  эмуляторе/устройстве.

### 5. Documentation, security review and delivery

- Изменяемые файлы: `docs/adr/0015-finance-module.md`,
  `docs/DEVELOPMENT_LOG.md`, README/config where needed.
- Проверка: `pnpm check`, `git diff --check`, `git status --short --branch`.

## Review

- [ ] Суммы и RLS не зависят от UI.
- [ ] API key хранится только в secure storage.
- [ ] AI output не сохраняется без подтверждения.
- [ ] Нет ручных правок generated code.
- [ ] Platform QA честно отделён от статических проверок.

## Доставка

- [ ] Commit на русском Conventional Commits.
- [ ] Push ветки.
- [ ] CI/PR, если доступен GitHub remote.

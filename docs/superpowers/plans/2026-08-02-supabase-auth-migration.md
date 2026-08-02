# План реализации: Supabase Auth

## 1. Документация и контракты

- Добавить ADR-0014, spec и этот план.
- Обновить PROJECT_CONTEXT, ARCHITECTURE, PRODUCT, ROADMAP, AUTHENTICATION,
  AGENTS и DECISIONS.
- Зафиксировать ошибку Auth0 → Supabase `401` в DEVELOPMENT_LOG.

## 2. RED: тесты

- Переписать конфигурационные тесты с Auth0 на Supabase.
- Добавить Windows HTTP tests для signup, signin, refresh и logout.
- Добавить Android instrumentation tests для тех же контрактов.
- Запустить тесты и убедиться, что новые тесты падают до production-кода.

## 3. GREEN: клиенты

- Заменить OIDC/AppAuth flow прямым Supabase Auth REST client.
- Добавить нативные email/password поля и команды входа/регистрации.
- Реализовать сохранение и обновление Supabase TokenSet.
- Сохранить существующий dashboard shell и sign out.

## 4. GREEN: данные и cloud

- Создать миграцию через Supabase CLI.
- Перевести `app_profiles` на `auth.users(id)` и `auth.uid()` RLS.
- Применить миграцию к Andivum Supabase project.
- Проверить security/performance advisors.
- Отключить Auth0 Third-party Auth и удалить Auth0 runtime/config assets.

## 5. Проверка и доставка

- `pnpm check`, Android build/instrumentation tests, Windows build/tests.
- Установить свежий APK на подключённый Pixel и пройти cloud smoke.
- Запустить Windows версию и пройти тот же сценарий.
- Обновить DEVELOPMENT_LOG, проверить diff и секреты.
- Commit/push после зелёных проверок.

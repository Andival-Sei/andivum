# Дорожная карта

## 0. Foundation

- [x] Зафиксировать продукт, стек и архитектурные решения.
- [x] Ввести machine-readable product configuration.
- [x] Определить правила для AI-агентов.
- [x] Зафиксировать автономный spec-first TDD-процесс.
- [x] Добавить воспроизводимую проверку developer toolchain.
- [x] Создать backend monorepo skeleton и единый `pnpm` command facade.
- [ ] Добавить CI для Windows, Android, backend и контрактов.
- [x] Добавить локальную PostgreSQL-инфраструктуру без production-секретов.
- [x] Зафиксировать нативную локализацию Windows/Android и policy fallback.
- [x] Выбрать Auth0 + Supabase как managed backend первого облачного MVP.

## 1. Authentication vertical slice

- [x] Создать ASP.NET Core backend и PostgreSQL migrations.
- [x] Подключить ASP.NET Core Identity и OpenIddict.
- [x] Зафиксировать auth API, локальный HTTPS запуск и security checks.
- [x] Реализовать registration/sign-in с email/password и optional passkeys в
      настройках.
- [ ] Реализовать logout и account recovery.
- [x] Создать WinUI 3 shell и OIDC PKCE client foundation.
- [x] Создать Android Compose shell и OIDC PKCE client foundation.
- [x] Добавить защищённую проверку native-сессии и автоматическое обновление
      токенов на Windows и Android.
- [ ] Добавить нативные `en-US`/`ru-RU` ресурсы, language settings и `pnpm i18n:check`.
- [ ] Добавить secure token storage и logout-all-devices.
- [ ] Проверить один аккаунт на Windows и Android.

## 1.5. Managed authentication/data MVP

- [ ] Создать Auth0 tenant, Native Applications и email/password connection.
- [ ] Создать Supabase project и включить Third-party Auth для Auth0.
- [ ] Добавить Auth0 Action с `role=authenticated` в ID token.
- [x] Подготовить конфигурацию native issuer/client IDs/callbacks для Auth0 без
      client secrets.
- [x] Добавить миграцию `app_profiles` с RLS по Auth0 `sub` и автоматический
      bootstrap профиля.
- [x] Подготовить direct Supabase profile/session flow, mocked tests и SQL RLS
      проверку.
- [x] Оставить OpenIddict только как явный local-dev fallback до настройки
      внешних проектов.
- [ ] Проверить direct Supabase profile/session flow на Windows и физическом
      Android с реальным Auth0/Supabase project.
- [ ] Решить отдельным ADR, какие Tasks/Finance операции требуют RPC, Edge
      Functions или тонкого API.

## 2. Tasks vertical slice

- [ ] Inbox и быстрый ввод.
- [ ] Today, сроки, приоритеты и статусы.
- [ ] Повторяющиеся задачи.
- [ ] Локальные уведомления.
- [ ] Cache/command queue и базовая синхронизация.

## 3. Finance vertical slice

- [ ] Счета, валюты и операции.
- [ ] Категории и регулярные операции.
- [ ] Бюджеты и базовые сводки.
- [ ] Денежные golden tests на обеих платформах и backend.

## 4. Связи и автоматизация

- [ ] Стабильные cross-module references.
- [ ] Domain events между Tasks и Finance.
- [ ] Пользовательские правила и журнал их выполнения.

## 5. Интеграции и совместная работа

- [ ] Capability-based integration framework.
- [ ] Первый реальный провайдер после отдельного threat review.
- [ ] Семейные пространства, роли и аудит доступа.

Веб-клиент рассматривается только после стабилизации API и основных модулей.

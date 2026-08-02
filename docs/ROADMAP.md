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
- [x] Выбрать Supabase Auth + Supabase PostgreSQL как managed backend первого
      облачного MVP.

## 1. Authentication vertical slice

- [x] Создать ASP.NET Core backend и PostgreSQL migrations.
- [x] Подключить ASP.NET Core Identity и OpenIddict.
- [x] Зафиксировать auth API, локальный HTTPS запуск и security checks.
- [x] Реализовать registration/sign-in с email/password через Supabase Auth.
- [ ] Добавить optional passkeys в настройках после отдельной проверки Beta API.
- [x] Реализовать logout.
- [ ] Реализовать account recovery.
- [x] Создать WinUI 3 shell.
- [x] Создать Android Compose shell.
- [x] Добавить нативные Supabase Auth email/password signup/sign-in.
- [x] Добавить защищённую проверку native-сессии и автоматическое обновление
      Supabase-токенов на Windows и Android.
- [ ] Добавить нативные `en-US`/`ru-RU` ресурсы, language settings и `pnpm i18n:check`.
- [x] Добавить secure token storage.
- [ ] Добавить logout-all-devices.
- [ ] Проверить один аккаунт на Windows и Android.

## 1.5. Managed authentication/data MVP

- [x] Создать Supabase project и включить email/password Auth.
- [x] Перевести `app_profiles` на `auth.users.id` и `auth.uid()` RLS.
- [x] Реализовать direct Supabase Auth signup/sign-in/refresh/logout.
- [ ] Проверить direct Supabase Auth flow на Windows и физическом Android.
- [x] Убрать Auth0 из runtime, Supabase config, Actions и native application
      config. Auth0 tenant не удаляется автоматически, чтобы не потерять его
      данные необратимо.
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

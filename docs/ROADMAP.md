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

## 1. Authentication vertical slice

- [x] Создать ASP.NET Core backend и PostgreSQL migrations.
- [x] Подключить ASP.NET Core Identity и OpenIddict.
- [x] Зафиксировать auth API, локальный HTTPS запуск и security checks.
- [ ] Реализовать registration/sign-in/logout с passkeys.
- [x] Создать WinUI 3 shell и OIDC PKCE client foundation.
- [x] Создать Android Compose shell и OIDC PKCE client foundation.
- [x] Добавить passwordless registration surface и блокировку authorize до
      сохранения первого passkey.
- [ ] Добавить нативные `en-US`/`ru-RU` ресурсы, language settings и `pnpm i18n:check`.
- [ ] Добавить secure token storage и logout-all-devices.
- [ ] Проверить один аккаунт на Windows и Android.

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

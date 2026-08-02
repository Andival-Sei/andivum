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

## 1. Authentication vertical slice

- [ ] Создать ASP.NET Core backend и PostgreSQL migrations.
- [ ] Подключить ASP.NET Core Identity и OpenIddict.
- [ ] Реализовать registration/sign-in/logout с passkeys.
- [ ] Создать WinUI 3 shell и OIDC PKCE client.
- [ ] Создать Android Compose shell и OIDC PKCE client.
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

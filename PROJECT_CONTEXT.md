# Project Context

## Коротко

Andivum — рабочее название персональной модульной системы для Windows 11 и
Android. Первый пользователь — владелец проекта, затем его семья и другие
желающие. Приложение объединяет задачи, финансы и будущие области жизни в одной
системе; модули могут безопасно взаимодействовать друг с другом.

## Первый продуктовый срез

1. Экран входа и регистрации.
2. Passwordless-аутентификация с passkeys.
3. Базовый модуль умных задач.
4. Базовый модуль личных финансов.
5. Синхронизация данных между устройствами одного пользователя.
6. Русский и английский языки с выбором по системной локали и русским fallback.
7. Светлая и тёмная темы с выбором по системной теме.

Семейные пространства, сложная автоматизация, Google/Steam и другие интеграции
следуют после устойчивого персонального сценария.

## Зафиксированный стек

- Windows: C# 14, .NET 10 LTS, WinUI 3, Windows App SDK stable.
- Android: Kotlin, Jetpack Compose, Material 3, AndroidX.
- Backend: ASP.NET Core 10 modular monolith.
- Данные сервера: PostgreSQL + EF Core/Npgsql.
- Локальные данные: SQLite; Room на Android, Microsoft.Data.Sqlite/EF Core на
  Windows.
- API: HTTPS REST + OpenAPI, generated native clients.
- Auth: ASP.NET Core Identity passkeys + OIDC/OAuth 2.0 Authorization Code with
  PKCE; OpenIddict как протокольный сервер.
- Наблюдаемость: OpenTelemetry.
- Репозиторий: monorepo, единый command facade через `pnpm`.

Точные версии Android SDK, Kotlin и библиотек фиксируются lock/version catalog
при создании проектов. Используются только stable-релизы.

## Что является общим

- серверные бизнес-инварианты;
- OpenAPI-контракты и генерируемые модели клиентов;
- схемы событий и синхронизации;
- product configuration;
- спецификации поведения и одинаковые golden test cases;
- локальная инфраструктура, CI и developer tooling.

UI, интеграция с ОС, локальное хранение и часть offline-логики реализуются
нативно и осознанно дублируются. Общий runtime-код между C# и Kotlin пока не
создаётся.

## Архитектурная форма

- Один modular-monolith backend на старте.
- Вертикальные модули: Identity, Tasks, Finance, Integrations, Automation.
- Каждый модуль владеет своими данными и публичными контрактами.
- Клиенты повторяют модульные границы внутри своих платформенных проектов.
- Межмодульные связи выражаются стабильными идентификаторами и событиями, а не
  прямым доступом к чужим таблицам.

## Важные ограничения

- Web-клиент не входит в начальный scope.
- Название `Andivum` рабочее и может измениться.
- Display name изменяем, опубликованные package IDs и passkey RP ID — нет.
- Не строим микросервисы до измеримой потребности.
- Не встраиваем MCP или административный backdoor в production-приложение.
- Не выбираем облачного провайдера до появления работающего локального vertical
  slice.

## Текущий следующий шаг

Нативные login/dashboard shells для Windows и Android уже созданы и подключены
к passkey-flow. Защищённый `GET /api/v1/session`, обновление access/refresh
токенов и стабильный `userId` уже подключены к обоим клиентам; физический
Android smoke подтверждает серверную проверку. Следующая цель authentication
slice — logout, account recovery и ручной Windows/Android smoke одного аккаунта.
После этого начинается Tasks vertical slice.

# Project Context

## Коротко

Andivum — рабочее название персональной модульной системы для Windows 11 и
Android. Первый пользователь — владелец проекта, затем его семья и другие
желающие. Приложение объединяет задачи, финансы и будущие области жизни в одной
системе; модули могут безопасно взаимодействовать друг с другом.

## Первый продуктовый срез

1. Экран входа и регистрации.
2. Регистрация и вход по email + паролю через Auth0 и системный браузер;
   passkey можно подключить дополнительно в настройках аккаунта.
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
- Identity: Auth0 Database Connection + OIDC Authorization Code with PKCE.
- Данные первого облачного MVP: Supabase PostgreSQL, Data API, Storage и RLS.
- Server-side code: Supabase SQL/RPC/Edge Functions по мере необходимости;
  будущий тонкий ASP.NET Core API допускается отдельным ADR.
- Локальные данные: SQLite; Room на Android, Microsoft.Data.Sqlite/EF Core на
  Windows.
- API: HTTPS REST + OpenAPI, generated native clients.
- Auth: Auth0 email/password + hosted Universal Login + OIDC/OAuth 2.0
  Authorization Code with PKCE. Пароль вводится только в Auth0 browser surface,
  не в native-клиенте.
- Наблюдаемость: OpenTelemetry.
- Репозиторий: monorepo, единый command facade через `pnpm`.

Точные версии Android SDK, Kotlin и библиотек фиксируются lock/version catalog
при создании проектов. Используются только stable-релизы.

## Что является общим

- серверные бизнес-инварианты в Supabase RLS/RPC/Edge Functions или будущем API;
- OpenAPI-контракты и генерируемые модели клиентов;
- схемы событий и синхронизации;
- product configuration;
- спецификации поведения и одинаковые golden test cases;
- локальная инфраструктура, CI и developer tooling.

UI, интеграция с ОС, локальное хранение и часть offline-логики реализуются
нативно и осознанно дублируются. Общий runtime-код между C# и Kotlin пока не
создаётся.

## Архитектурная форма

- На первом облачном срезе — managed backend Supabase без собственного
  постоянно работающего сервера; сложная логика позже выносится в Edge Functions
  или тонкий modular-monolith API.
- Вертикальные модули: Identity, Tasks, Finance, Integrations, Automation.
- Каждый модуль владеет своими таблицами, RLS-политиками и публичными
  контрактами.
- Клиенты повторяют модульные границы внутри своих платформенных проектов.
- На прямом MVP-клиенте межмодульный direct table access ограничен RLS; сложные
  связи выражаются RPC/Edge Functions и стабильными идентификаторами. Полный
  event-driven flow появится вместе с server-side orchestration.

## Важные ограничения

- Web-клиент не входит в начальный scope.
- Название `Andivum` рабочее и может измениться.
- Display name изменяем, опубликованные package IDs и passkey RP ID — нет.
- Не строим микросервисы до измеримой потребности.
- Не встраиваем MCP или административный backdoor в production-приложение.
- Auth0 и Supabase выбраны для первого облачного MVP; локальный OpenIddict и
  PostgreSQL остаются только временным dev fallback до завершения реального
  cloud smoke-теста.

## Текущий следующий шаг

Нативные login/dashboard shells для Windows и Android уже созданы и подключены
к конфигурируемому OIDC-flow. В dev tenant Auth0 созданы отдельные Native
Applications Andivum Windows/Android, email/password connection, ограниченный
Action для Supabase и включена ротация refresh-токенов. В Supabase project
Andivum включён Auth0 Third-party Auth, применены `app_profiles` и RLS-
миграции, а локальные публичные параметры лежат в игнорируемом
`.env.andivum.local`. Следующий шаг — live smoke: регистрация/вход на Windows,
затем на физическом Android и проверка одного Auth0 subject в обеих системах.
После этого внешний smoke заканчивается и начинается Tasks vertical slice.

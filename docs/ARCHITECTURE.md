# Архитектура

## 1. Принципы

1. Нативность важнее максимального переиспользования UI-кода.
2. Общие правила хранятся на сервере или выражаются исполняемыми контрактами и
   общими тестовыми примерами.
3. Сначала modular monolith, затем разделение только по измеримой нагрузке или
   организационной необходимости.
4. Каждый модуль владеет своими данными.
5. Security boundary проектируется до интеграций и синхронизации.
6. CLI — основной программный интерфейс разработки; MCP может быть тонкой
   оболочкой над ним.

## 2. Состав системы

```text
Native Windows app (C#/WinUI 3) ─┐
                                 ├─ OIDC/PKCE ─ Auth0
Native Android app (Kotlin) ─────┤
                                 └─ Supabase Data API/Storage + RLS
                                      │
                                      └─ SQL/RPC/Edge Functions
                                         (позже тонкий API при необходимости)
```

Планируемая структура monorepo:

```text
apps/
  windows/
  android/
services/
  api/
contracts/
  openapi/
infra/
modules/
  specs/
tools/
  cli/
  mcp/          # только когда CLI стабилен и MCP действительно нужен
config/
docs/
```

Первые реальные каталоги клиентов уже созданы в `apps/windows` и `apps/android`;
их команды сборки закреплены в корневом `package.json`.

`modules/specs` содержит платформонезависимые спецификации, схемы событий и
golden test cases, но не общий runtime-код UI-клиентов.

## 3. Стек

### Windows

- C# 14 и .NET 10 LTS;
- WinUI 3 на stable-канале Windows App SDK 1.8.x;
- MVVM и однонаправленный поток состояния;
- Windows App SDK/Win32 APIs для возможностей ОС;
- SQLite через Microsoft.Data.Sqlite или EF Core SQLite;
- MSIX для упаковки и обновлений.

Используется системная локаль с ручным per-device override. Windows локализуется
через MRT Core и `.resw`, Android — через `strings.xml`/`plurals`. Языки `ru-*`
и `en-*` сводятся к `ru-RU` и `en-US`, остальные или недоступные локали получают
русский fallback. Платформенные сервисы (уведомления, secure storage, deep links,
lifecycle и locale settings) находятся за внутренними интерфейсами, чтобы
domain/use-case слой тестировался без UI.

### Android

- Kotlin 2.4.0 и Gradle 9.1 через version catalog/wrapper;
- Android Gradle Plugin 9.0.1 и compile/target SDK 36;
- Jetpack Compose + Material 3;
- Compose BOM 2026.06.00;
- AppAuth-Android для OIDC Authorization Code + PKCE;
- ViewModel, StateFlow и unidirectional data flow;
- AndroidX Navigation;
- Room/SQLite;
- WorkManager для гарантированной фоновой работы;
- Credential Manager и App Links/Digital Asset Links там, где необходим прямой
  платформенный credential flow.

Используются stable Android SDK и библиотеки. Preview-зависимости в production
ветку не добавляются без ADR.

### Managed backend первого MVP

- Auth0 Database Connection и Universal Login для identity;
- Supabase PostgreSQL;
- Supabase Data API, Storage и Row Level Security;
- Supabase SQL/RPC и Edge Functions только для операций, которые нельзя
  безопасно отдать прямому клиентскому CRUD;
- OpenAPI остаётся контрактом будущего API и server-side use cases;
- OpenTelemetry появится в Edge Functions/тонком API, когда эти surfaces будут
  добавлены.

ASP.NET Core 10 + EF Core/Npgsql и локальный OpenIddict сохраняются в репозитории
как временный dev fallback и задел будущего тонкого API. Они не являются
обязательной production-зависимостью первого облачного MVP.

## 4. Аутентификация и passkeys

Начальный облачный поток нативных приложений:

1. Приложение открывает Auth0 Universal Login в системном браузере, а не
   embedded WebView.
2. Auth0 выполняет email/password registration/sign-in и OIDC Authorization Code
   flow с PKCE `S256`.
3. Пароль вводится только в Auth0 browser surface; native-клиент его не видит
   и не использует password grant.
4. Auth0 возвращает короткоживущий access token, ID token и rotation-capable
   refresh token на точный native callback.
5. Токены сохраняются только в защищённом хранилище ОС.
6. Supabase Third-party Auth проверяет JWT Auth0; Auth0 Action добавляет
   `role=authenticated` в ID token, который используется для Data API/Storage.
7. RLS проверяет immutable Auth0 `sub` из `auth.jwt()`, а не email и не
   client-provided owner id.
8. Dashboard открывается только после успешной проверки/создания Supabase
   `app_profiles`; при истечении access token используется refresh rotation.

До реализации production-auth необходимо:

- создать Auth0 tenant, Native Applications и Database Connection;
- создать Supabase project и Third-party Auth integration;
- закрепить HTTPS custom domain для Auth0 passkeys;
- записать provider configuration через secrets/environment;
- настроить strict redirect/issuer/audience policy;
- определить account recovery;
- добавить подтверждение email и безопасное восстановление пароля;
- ограничить число и длину имён passkeys;
- спроектировать revocation, logout-all-devices и refresh-token rotation.

Текущий локальный fallback и команды запуска описаны в
[`docs/AUTHENTICATION.md`](AUTHENTICATION.md), а стабильная часть API — в
[`contracts/openapi/andivum-auth.yaml`](../contracts/openapi/andivum-auth.yaml).
OIDC discovery остаётся источником актуальных endpoint metadata. Production не
может стартовать без явного внешнего issuer, RLS и безопасной конфигурации
provider keys.

## 5. Модули

Начальные backend-модули:

- `Identity` — аккаунты, устройства, сессии и passkeys;
- `Tasks` — задачи, списки, повторения и напоминания;
- `Finance` — счета, операции, категории и бюджеты;
- `Integrations` — внешние провайдеры и защищённые токены;
- `Automation` — правила между модулями; добавляется после базовых Tasks и
  Finance.

Модуль публикует application contracts и domain events. Например,
`Finance.RecurringPaymentDue` может породить задачу через Automation, но Tasks
не читает таблицу Finance.

## 6. Общая логика двух нативных клиентов

Один и тот же исходный код между C# и Kotlin на старте не делится. Вместо этого
переиспользуются:

- OpenAPI schema и сгенерированные DTO/API clients;
- JSON Schema для доменных событий и настроек;
- серверная валидация и вычисления, которые должны быть авторитетными;
- одинаковые golden test vectors для денег, дат, повторений и синхронизации;
- UX/specification documents;
- локальная инфраструктура и test fixtures.

Клиенты могут дублировать быстрые проверки для UX, но сервер повторно проверяет
каждую команду. Если позже появится большой чистый алгоритмический core и
профилирование докажет выгоду общего binary-модуля, это рассматривается
отдельным ADR.

## 7. Данные и синхронизация

Supabase Postgres — источник истины для облачного состояния. На первом этапе
клиенты используют локальную SQLite-базу как cache и очередь локальных команд,
но не обещают полное редактирование всех сущностей без сети. Прямой клиентский
доступ разрешён только через publishable key и RLS; операции с денежными
инвариантами будут RPC/Edge Functions или будущим API.

Прикладной профиль связывается с Auth0 так:

```text
Auth0 JWT sub (text) ──RLS──> public.app_profiles.auth0_subject
                                  │
                                  └──> Tasks/Finance rows and future spaces
```

Пароли и passkey credentials в Supabase не дублируются. Email может быть
отображаемым атрибутом, но не authorization key.

Контракт синхронизации должен поддерживать:

- UUID/ULID-подобные client-generated IDs;
- idempotency key для команд;
- optimistic concurrency/version;
- tombstones для удалений;
- incremental cursor;
- UTC timestamps + отдельную пользовательскую timezone там, где она значима;
- decimal/minor units для денег, без binary floating point.

Полный conflict-resolution проектируется после первого работающего online
vertical slice.

## 8. Интеграции

Google, Steam и будущие сервисы подключаются backend-адаптерами. Клиенты не
хранят долгоживущие provider secrets. Каждый адаптер реализует минимальный
capability contract, например `CalendarRead`, `ProfileRead` или
`GameLibraryRead`, вместо одного универсального интерфейса.

Пользователь явно подключает и отключает каждую интеграцию. Scope запрашивается
по принципу минимальных привилегий.

## 9. Имя и идентификаторы

`config/product.json` — источник display name, поддерживаемых локалей,
технической базовой локали ресурсов, locale fallback policy и тем. При создании
проектов появятся нативные каталоги ресурсов и CLI-проверка их согласованности;
общий runtime JSON со всеми переводами не вводится.

Не все значения являются переименуемыми:

| Значение                 | Можно менять после публикации | Комментарий                     |
| ------------------------ | ----------------------------- | ------------------------------- |
| Display name             | Да                            | Централизованный ресурс         |
| Логотип/описание         | Да                            | Обычный ребрендинг              |
| Android application ID   | Практически нет               | Смена создаёт другое приложение |
| Windows package identity | Только с миграцией            | Связано со Store/установкой     |
| Passkey RP ID            | Нет без миграции              | Passkeys привязаны к домену     |
| API namespace            | Возможно, но дорого           | Не пользовательское имя         |

## 10. AI-friendly developer interface

Корневой `pnpm` facade постепенно получает команды:

```text
pnpm run doctor
pnpm check
pnpm format
pnpm test
pnpm build
pnpm api:generate
pnpm dev:infra
pnpm dev:api
pnpm dev:windows
pnpm dev:android
```

Developer CLI должен иметь `--json`, стабильные exit codes и безопасные операции
с test/demo data. Supabase migrations выполняются через Supabase CLI. Если
появится MCP server, он вызывает CLI/application services и работает только в
development-профиле. Встраивать MCP в production приложение не нужно: это
увеличит поверхность атаки и не даст преимуществ по сравнению с отдельным
dev-tool.

## 11. Проверенные источники

- [.NET 10 LTS support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [Windows WebAuthn API](https://learn.microsoft.com/en-us/windows/win32/webauthn/-webauthn-portal)
- [ASP.NET Core Identity passkeys](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)
- [Android Credential Manager prerequisites](https://developer.android.com/identity/credential-manager/prerequisites)
- [Auth0 native applications](https://auth0.com/docs/get-started/auth0-overview/create-applications/native-apps)
- [Supabase Auth0 third-party authentication](https://supabase.com/docs/guides/auth/third-party/auth0)
- [Supabase Row Level Security](https://supabase.com/docs/guides/database/postgres/row-level-security)
- [Jetpack Compose architecture](https://developer.android.com/develop/ui/compose/architecture)

Источники проверены 2026-08-02. Точные версии зависимостей всё равно фиксируются
в репозитории и обновляются контролируемо.

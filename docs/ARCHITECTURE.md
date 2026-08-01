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
                                 ├─ HTTPS REST/OpenAPI ─ Modular backend ─ PostgreSQL
Native Android app (Kotlin) ─────┘                         │
                                                          └─ Integration adapters
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

Используются системные локаль и тема. Платформенные сервисы (уведомления, secure
storage, deep links, lifecycle) находятся за внутренними интерфейсами, чтобы
domain/use-case слой тестировался без UI.

### Android

- Kotlin, версия фиксируется в Gradle version catalog;
- Jetpack Compose + Material 3;
- ViewModel, StateFlow и unidirectional data flow;
- AndroidX Navigation;
- Room/SQLite;
- WorkManager для гарантированной фоновой работы;
- Credential Manager и App Links/Digital Asset Links там, где необходим прямой
  платформенный credential flow.

Используются stable Android SDK и библиотеки. Preview-зависимости в production
ветку не добавляются без ADR.

### Backend

- ASP.NET Core 10 на .NET 10 LTS;
- modular monolith с вертикальными модулями;
- PostgreSQL;
- EF Core 10 + Npgsql;
- ASP.NET Core Identity;
- OpenIddict для стандартных OIDC/OAuth 2.0 flows;
- OpenAPI как источник контрактов клиентов;
- OpenTelemetry для logs, metrics и traces;
- контейнеризованный локальный запуск PostgreSQL и API.

Облачный провайдер пока не выбран. Архитектура не должна требовать Supabase,
Azure или другого конкретного поставщика для локальной разработки.

## 4. Аутентификация и passkeys

Начальный поток нативных приложений:

1. Приложение открывает системную authentication session, а не embedded WebView.
2. Backend выполняет OIDC Authorization Code flow с PKCE.
3. Страница auth-домена использует ASP.NET Core Identity passkeys/WebAuthn.
4. ОС показывает Windows Hello или доступный Android passkey provider.
5. Клиент получает короткоживущий access token и rotation-capable refresh token.
6. Токены сохраняются только в защищённом хранилище ОС.

Этот вариант даёт единый безопасный протокол Windows, Android и будущему вебу.
Прямые вызовы Windows WebAuthn API или Android Credential Manager могут быть
добавлены позже поверх тех же серверных ceremony endpoints, если системный
browser flow окажется недостаточно нативным.

До реализации production-auth необходимо:

- закрепить HTTPS-домен;
- записать `passkeyRelyingPartyId` в `config/product.json`;
- настроить строгую проверку origin и host headers;
- определить account recovery;
- ограничить число и длину имён passkeys;
- спроектировать revocation, logout-all-devices и refresh-token rotation.

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

Сервер — источник истины для межустройственного состояния. На первом этапе
клиенты используют локальную SQLite-базу как cache и очередь локальных команд,
но не обещают полное редактирование всех сущностей без сети.

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

`config/product.json` — источник display name, локалей и тем. При создании
проектов появится генератор ресурсов и build properties.

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
pnpm doctor
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
с test/demo data. Если появится MCP server, он вызывает CLI/application services
и работает только в development-профиле. Встраивать MCP в production приложение
не нужно: это увеличит поверхность атаки и не даст преимуществ по сравнению с
отдельным dev-tool.

## 11. Проверенные источники

- [.NET 10 LTS support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [Windows WebAuthn API](https://learn.microsoft.com/en-us/windows/win32/webauthn/-webauthn-portal)
- [ASP.NET Core Identity passkeys](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)
- [Android Credential Manager prerequisites](https://developer.android.com/identity/credential-manager/prerequisites)
- [Jetpack Compose architecture](https://developer.android.com/develop/ui/compose/architecture)

Источники проверены 2026-08-02. Точные версии зависимостей всё равно фиксируются
в репозитории и обновляются контролируемо.

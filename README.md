# Andivum

> Рабочее название персональной модульной системы для управления делами,
> финансами и другими областями жизни.

Andivum проектируется как два нативных приложения:

- Windows 11: C#, .NET 10, WinUI 3;
- Android: Kotlin, Jetpack Compose;
- общий backend: ASP.NET Core 10 и PostgreSQL.

Первый продуктовый срез — авторизация с passkeys, умные задачи и личные финансы.
Архитектура сразу учитывает несколько пользователей, связанные модули, будущие
интеграции и возможный веб-клиент, но не пытается реализовать всё одновременно.

## Статус

Проект находится на этапе создания первого authentication vertical slice. API
skeleton и локальная PostgreSQL-инфраструктура уже готовы; нативные клиенты
подключаются следующим срезом. Текущие решения и границы проекта описаны в:

- [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) — короткий постоянный контекст;
- [docs/PRODUCT.md](docs/PRODUCT.md) — продукт и первый релиз;
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — целевая архитектура и стек;
- [docs/DECISIONS.md](docs/DECISIONS.md) — журнал решений;
- [docs/ROADMAP.md](docs/ROADMAP.md) — последовательность работ;
- [docs/DEVELOPMENT_PROCESS.md](docs/DEVELOPMENT_PROCESS.md) — автономный
  AI-driven/TDD процесс;
- [docs/TOOLCHAIN.md](docs/TOOLCHAIN.md) — SDK, CLI, MCP и проверка среды;
- [docs/AUTHENTICATION.md](docs/AUTHENTICATION.md) — локальный OIDC/passkey
  контракт и запуск auth-среза;
- [contracts/openapi/andivum-auth.yaml](contracts/openapi/andivum-auth.yaml) —
  машинно-читаемый контракт для native clients;
- [AGENTS.md](AGENTS.md) — правила работы AI-агентов и разработчиков.

## Быстрый старт для разработки

Пока доступна проверка целостности документации:

```powershell
pnpm install
pnpm run doctor
pnpm check
Copy-Item .env.example .env
pnpm dev:infra
pnpm api:test
```

Перед первым запуском скопируйте `.env.example` в `.env` и задайте локальный
пароль PostgreSQL. `.env` не попадает в Git. `pnpm dev:infra` запускает
локальный PostgreSQL в Docker и ждёт его healthy-состояния. `pnpm api:build`,
`pnpm api:test` и `pnpm dev:api` собирают, проверяют и запускают backend.
Корневые команды `pnpm` остаются единым входом для сборки, тестов,
форматирования, генерации API-клиентов и запуска локальной инфраструктуры.

## Имя продукта

`Andivum` — рабочее отображаемое имя. Оно хранится в
[`config/product.json`](config/product.json) и не должно без необходимости
дублироваться в коде. Технические идентификаторы приложений, домен passkeys и
имена пакетов имеют другой жизненный цикл: после публикации некоторые из них
нельзя безопасно переименовать.

## Лицензия

Лицензия пока не выбрана. Публичность репозитория сама по себе не предоставляет
разрешение на использование, изменение или распространение кода.

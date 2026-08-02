# Инструменты разработки

## Единая проверка

```powershell
pnpm run doctor
```

Для machine-readable результата:

```powershell
pnpm --silent run doctor --json
```

Команда завершается с кодом `1`, только если отсутствует обязательная часть
toolchain. Отложенные инструменты показываются как `WARN` и не блокируют
разработку.

`pnpm run` здесь обязателен: у pnpm есть собственная встроенная команда
`pnpm doctor`, не связанная с Andivum.

Doctor проверяет среду разработки проекта на Windows 11. Поддержка запуска этой
проверки на Linux/macOS сейчас не заявлена: серверные CI-проверки будут
добавляться отдельно и не должны требовать Windows/Android toolchain.

## Обязательная среда

- Git и авторизованный GitHub CLI;
- Node.js 24+ и pnpm 11;
- .NET 10 SDK;
- официальный `dotnet new winui` template;
- Windows Developer Mode;
- Android SDK, platform-tools, emulator и command-line tools;
- хотя бы один Android AVD;
- JDK 17+; локально используется JBR из Android Studio, без зависимости от
  случайной Java в системном `PATH`;
- Docker daemon для PostgreSQL и integration tests.

На 2026-08-02 локальная среда прошла все обязательные проверки: .NET 10.0.302,
Android SDK 34–36.1, AVD `Medium_Phone_API_36.1`, Android Studio с JBR 21,
официальный WinUI template, Developer Mode и Docker 29.6.2.

## Репозиторное закрепление

Глобальные установки используются только для базовых SDK. Проектные версии
закрепляются рядом с кодом:

- pnpm — `packageManager` и lockfile;
- .NET tools — `.config/dotnet-tools.json` после появления первого tool;
- Android — Gradle Wrapper и version catalog;
- OpenAPI generator — project-local package/config;
- Appium — project-local dependency и отдельный `APPIUM_HOME`;
- containers — versioned compose-файлы и pinned image tags.

Нативные клиентские проверки запускаются из корня:

```powershell
pnpm windows:build
pnpm windows:run
pnpm android:build
pnpm android:build:cloud
```

`windows:run` запускает WinUI через package-aware `dotnet run`, поэтому служебная
регистрация Windows App SDK создаётся автоматически. Он подхватывает `.env` и
неотслеживаемый `.env.andivum.local`, если файл существует, и передаёт публичную
Supabase-конфигурацию упакованному приложению через launch-параметры.

Windows-клиент закрепляет Windows App SDK 1.8.x. Android использует AGP 9.0.1,
Gradle 9.1, Kotlin 2.4.0 и JDK 17+; `tools/scripts/run-android-gradle.ps1`
подхватывает Android Studio JBR, если системный `JAVA_HOME` не задан.

## Отложенные инструменты

### Appium Windows Driver

Добавляется вместе с первым Windows end-to-end сценарием. Установка заранее
создала бы непроверяемую конфигурацию без приложения и AUMID.

### OpenAPI Generator

Добавляется после появления первой стабильной OpenAPI schema. Генератор и его
настройки должны быть закреплены в репозитории, а generated clients —
воспроизводимы одной командой.

### Android managed devices

Конфигурация добавляется в Android Gradle project вместе с первыми Compose UI
tests. Существующий локальный AVD подходит для live smoke testing.

## MCP и skills

На старте достаточно уже доступных возможностей:

- GitHub — репозиторий, issues, PR и delivery;
- Browser/Computer Use — web auth, Windows UI и Android emulator smoke QA;
- security skills — threat model и проверки auth/finance/integrations;
- Superpowers — spec, TDD, review и выполнение планов;
- creator-vibe — продуктовый характер и UX.

Новый MCP подключается только если даёт действие, которого нет у CLI/API, и
имеет минимальные полномочия. Database MCP не получает production credentials;
developer tooling работает через CLI/application services и локальные данные.

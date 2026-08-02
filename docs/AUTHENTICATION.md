# Authentication vertical slice

Этот документ описывает текущий локальный контракт авторизации Andivum для
Windows и Android. Источник машинно-читаемого контракта —
[`contracts/openapi/andivum-auth.yaml`](../contracts/openapi/andivum-auth.yaml),
а точные endpoint metadata всегда берутся из OIDC discovery.

## Что уже реализовано

- ASP.NET Core Identity хранит пользователей и passkeys в PostgreSQL.
- OpenIddict предоставляет OIDC discovery, authorization code и refresh token
  flows.
- Нативные клиенты являются public clients: `andivum-windows` и
  `andivum-android` не имеют client secret.
- Для native authorization обязателен Authorization Code + PKCE с `S256`.
- Access token живёт 5 минут, refresh token — 30 дней с rolling rotation.
- Passkey request options используют discoverable credentials и не принимают
  username.
- Mutating passkey endpoints защищены anti-forgery token.
- Анонимный пользователь может создать первый аккаунт через passkey без
  обязательной почты или пароля.
- Pending-аккаунт не может продолжить `/connect/authorize`, пока attestation не
  сохранит хотя бы один passkey.

Восстановление аккаунта, logout-all-devices и полный нативный UI пока остаются
следующими срезами. Регистрация сейчас реализована на server-rendered auth
surface и требует browser/WebAuthn smoke-теста.

## Локальный запуск

В PowerShell из корня репозитория:

```powershell
Copy-Item .env.example .env
dotnet dev-certs https --trust
pnpm install
pnpm dev:infra
pnpm dev:api
```

API доступен по адресу `https://localhost:7240`. `pnpm dev:api` использует
локальную PostgreSQL-базу из `.env`, включает только для неё
`Database:AutoMigrate=true` и создаёт зарегистрированные native clients.
Подключение к внешней базе не включает автоматические миграции.

Android Emulator обращается к машине разработчика через `10.0.2.2`; debug
сборка поэтому использует `https://10.0.2.2:7240`. Доверие к локальному
сертификату на эмуляторе будет добавлено в отдельном device-smoke шаге до
проверки реального обмена токенами.

Для физического Android-устройства debug API URL можно переопределить через
Gradle property. Например, при USB debugging и `adb reverse`:

```powershell
adb reverse tcp:7240 tcp:7240
pnpm android:build -- -PandivumApiBaseUrl=https://localhost:7240
```

Локальный HTTPS-сертификат всё равно должен быть доверен браузером и debug
клиентом; это отдельная часть device-smoke проверки.

На 2026-08-02 физический Pixel 7 Pro успешно определяется по USB, APK
устанавливается и UI запускается. `adb reverse` работает, но AppAuth discovery
останавливается на `Trust anchor for certification path not found`: устройство
ещё не доверяет локальному dev-сертификату. Trust-all обход для приложения не
используется; следующий вариант — установить только локальный CA на debug-
устройство или проверить flow на staging HTTPS.

Для остановки инфраструктуры:

```powershell
docker compose --env-file .env -f infra/compose/docker-compose.yml down
```

## Native OIDC flow

1. Клиент генерирует `state`, `code_verifier` и `code_challenge` методом S256.
2. Клиент открывает `/connect/authorize` в системной browser/authentication
   session. Embedded WebView не используется.
3. Пользователь проходит passkey ceremony на auth-домене.
4. Сервер возвращает authorization code на точный custom-scheme callback.
5. Клиент обменивает code на `/connect/token`, передавая тот же
   `code_verifier`.
6. Access и refresh tokens хранятся только в защищённом хранилище ОС.
7. При истечении access token клиент выполняет refresh-token grant и заменяет
   старый refresh token новым.

### Регистрация первого passkey

1. Пользователь выбирает `Create an account with passkey` на auth surface.
2. Сервер создаёт технический Identity account и устанавливает pending browser
   session.
3. Браузер выполняет WebAuthn creation ceremony.
4. Сервер сохраняет attestation и только после этого разрешает исходный
   authorize request.

Технический username не является отображаемым именем пользователя. Recovery,
email verification и смена имени аккаунта пока не входят в контракт.

Текущие development callbacks:

| Клиент | Redirect URI |
| --- | --- |
| Windows | `andivum://windows/auth/callback` |
| Android | `andivum://android/auth/callback` |

URI сравниваются целиком, включая path и завершающий slash. В production
появятся отдельные HTTPS-домен, RP ID, сертификаты OpenIddict и redirect policy;
development keys и localhost RP ID туда не переносятся.

## Проверки

```powershell
pnpm doctor
pnpm check
pnpm api:build
git diff --check
```

Для passkey-тестирования браузеру нужен доверенный локальный HTTPS-сертификат.
Реальный Windows Hello и Android Credential Manager будут проверены отдельным
device-smoke этапом после создания нативных оболочек.

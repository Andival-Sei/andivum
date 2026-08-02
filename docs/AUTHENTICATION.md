# Authentication vertical slice

Этот документ описывает текущий локальный контракт авторизации Andivum для
Windows и Android. Источник машинно-читаемого контракта —
[`contracts/openapi/andivum-auth.yaml`](../contracts/openapi/andivum-auth.yaml),
а точные endpoint metadata всегда берутся из OIDC discovery.

## Что уже реализовано

- ASP.NET Core Identity хранит пользователей, хэши паролей и passkeys в
  PostgreSQL.
- OpenIddict предоставляет OIDC discovery, authorization code и refresh token
  flows.
- Нативные клиенты являются public clients: `andivum-windows` и
  `andivum-android` не имеют client secret.
- Для native authorization обязателен Authorization Code + PKCE с `S256`.
- Access token живёт 5 минут, refresh token — 30 дней с rolling rotation.
- Passkey request options используют discoverable credentials и не принимают
  username.
- Mutating passkey endpoints защищены anti-forgery token.
- `/connect/authorize` показывает регистрацию и вход по email/password в
  системном браузере. Пароль не передаётся в native-клиент.
- Пароль должен содержать минимум 12 символов, цифру, строчные и прописные
  буквы, специальный символ и минимум 5 разных символов; после 5 неудачных
  попыток аккаунт временно блокируется на 15 минут.
- Passkey остаётся дополнительным способом входа и подключается из
  authenticated account settings; его наличие не блокирует authorize после
  email/password входа.
- Локальный MVP пока не отправляет email confirmation и не предоставляет
  recovery. Это не считается production-ready auth.

Восстановление аккаунта и logout-all-devices остаются следующими auth-срезами.
Нативные login/dashboard shells уже реализованы на Windows и Android; email/password
и passkey ceremony проходят на server-rendered auth surface через системный
браузер.
После получения токенов оба клиента вызывают защищённый `GET /api/v1/session`.
Dashboard открывается только после ответа сервера, а не только потому, что
локально нашёлся сохранённый токен.
Native-клиенты запрашивают `offline_access`, поэтому при истечении короткого
access token клиент получает новую пару токенов через refresh token и сохраняет
её в защищённом хранилище ОС.
Ответ `/api/v1/session` содержит стабильный `userId`. Одинаковый `userId` на
Windows и Android означает, что обе платформы работают с одной аккаунтной
записью; токен и сам идентификатор не выводятся в UI или логи.

## Локальный запуск

В PowerShell из корня репозитория:

```powershell
Copy-Item .env.example .env
pnpm install
pnpm dev:infra
pnpm dev:api
```

API доступен по адресу `https://localhost:7240`. При первом запуске
`pnpm dev:api` автоматически подготавливает локальный CA и сертификат API в
`%TEMP%\andivum-local-ca`, а корневой CA добавляется в доверенные сертификаты
текущего пользователя Windows. Это нужно, чтобы Windows и debug Android
доверяли одному и тому же API-сертификату. На Android устанавливается только
файл `andivum-local-ca.crt` как «Сертификат центра сертификации»; PFX-файл на
телефон не переносится.

`pnpm dev:api` использует локальную PostgreSQL-базу из `.env`, включает только для неё
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

Для debug APK добавлена только debug-конфигурация Network Security Config,
разрешающая доверять пользовательскому CA на устройстве. Release APK эту
настройку не получает и не ослабляет проверку сертификатов.

На 2026-08-02 физический Pixel 7 Pro успешно определяется по USB, APK
устанавливается и UI запускается. После установки локального CA на debug-
устройство и `adb reverse` AppAuth discovery, passkey sign-in, возврат по
callback и защищённая проверка сессии проходят успешно; instrumentation дал
5/5 тестов. Trust-all обход для приложения не используется. Windows login shell
проверен в packaged-приложении; Windows Hello ceremony требует ручного
подтверждения пользователя и не автоматизируется агентом. Email/password
registration/sign-in покрыты API integration tests; ручной browser smoke для
реального аккаунта остаётся отдельным шагом.

Для остановки инфраструктуры:

```powershell
docker compose --env-file .env -f infra/compose/docker-compose.yml down
```

## Native OIDC flow

1. Клиент генерирует `state`, `code_verifier` и `code_challenge` методом S256.
2. Клиент открывает `/connect/authorize` в системной browser/authentication
   session. Embedded WebView не используется.
3. Пользователь вводит email/password или выбирает уже подключённый passkey на
   auth-домене.
4. Сервер возвращает authorization code на точный custom-scheme callback.
5. Клиент обменивает code на `/connect/token`, передавая тот же
   `code_verifier`.
6. Access и refresh tokens хранятся только в защищённом хранилище ОС.
7. При истечении access token клиент выполняет refresh-token grant и заменяет
   старый refresh token новым.

### Подключение passkey после регистрации

1. Пользователь сначала регистрируется или входит по email/password.
2. В authenticated `/Account/Settings` выбирает подключение passkey.
3. Браузер выполняет WebAuthn creation ceremony.
4. Сервер сохраняет attestation для текущего Identity account.

Email используется как логин, но не является отображаемым именем пользователя.
Recovery, email verification и смена имени аккаунта пока не входят в контракт.

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
Android physical smoke уже выполнен; ручное подтверждение Windows Hello и
полная проверка одного аккаунта на двух устройствах остаются отдельным шагом.

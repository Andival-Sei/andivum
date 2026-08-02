# Authentication vertical slice

Andivum использует Auth0 как источник identity для первого облачного MVP, а
Supabase — как managed data backend. Текущий локальный OpenIddict остаётся
явным dev fallback, пока реальные Auth0/Supabase проекты не настроены.

## Целевая облачная схема

```text
Windows/Android
      │ system browser + Authorization Code + PKCE (S256)
      ▼
Auth0 Universal Login
      │ ID/access/refresh tokens
      ▼
Supabase Data API / Storage
      │ Third-party Auth + RLS
      ▼
app_profiles, Tasks, Finance
```

Auth0 Database Connection хранит email/password и данные identity. Пароли,
passkeys и Auth0 Management API credentials в Supabase не копируются.
Supabase хранит только прикладной профиль и данные модулей.

## Почему нет второй базы пользователей

Auth0 `sub` — стабильный идентификатор identity. Supabase `app_profiles` хранит
его в поле `auth0_subject` вместе с display name, locale, theme и техническими
датами. Email может измениться и не используется как первичный ключ или
authorization key.

RLS сравнивает текущий JWT subject с владельцем строки:

```sql
to authenticated
using ((select auth.jwt() ->> 'sub') = auth0_subject)
with check ((select auth.jwt() ->> 'sub') = auth0_subject)
```

Для будущих family spaces проверяется membership, а не только строка профиля.
Каждая таблица в exposed schema должна иметь RLS и explicit policies.

## Auth0 configuration

Нужно создать в Auth0 Dashboard:

1. Native Application для Windows.
2. Native Application для Android.
3. Database Connection с email/password и включить её для обоих приложений.
4. API/аудиторию, если операции позже будут идти через собственный API.
5. Auth0 Action `onExecutePostLogin`, добавляющий в ID token literal claim:

   ```javascript
   exports.onExecutePostLogin = async (event, api) => {
     api.idToken.setCustomClaim('role', 'authenticated');
   };
   ```

В native apps нельзя хранить client secret. Для production нужно зарегистрировать
точные callback/logout URIs и включить refresh-token rotation.

Android production предпочитает verified HTTPS App Links с package name и
SHA-256 fingerprint. Уникальная custom scheme допустима как development
fallback. Windows использует зарегистрированный custom scheme либо согласованный
HTTPS callback.

## Supabase configuration

В Supabase Dashboard:

1. Создать проект.
2. В Authentication settings добавить Third-party Auth integration для Auth0.
3. Проверить tenant ID/region и signing algorithm Auth0.
4. Выполнить миграции из `supabase/migrations`.
5. Проверить RLS policy на реальном dev project.

Supabase client получает URL и publishable key из окружения приложения, а JWT
Auth0 передаётся как пользовательский bearer token. Service-role key и пароль
PostgreSQL в native apps запрещены.

## Подключение настроек к локальным клиентам

Windows читает значения из окружения процесса. Пример запуска из PowerShell
после создания внешних проектов:

```powershell
$env:ANDIVUM_AUTH_PROVIDER = "auth0-supabase"
$env:ANDIVUM_AUTH0_DOMAIN = "dev-example.eu.auth0.com"
$env:ANDIVUM_AUTH0_WINDOWS_CLIENT_ID = "<windows-client-id>"
$env:ANDIVUM_AUTH0_WINDOWS_REDIRECT_URI = "andivum://windows/auth/callback"
$env:ANDIVUM_SUPABASE_URL = "https://<project-ref>.supabase.co"
$env:ANDIVUM_SUPABASE_PUBLISHABLE_KEY = "<publishable-key>"
pnpm windows:build
```

Android получает эти публичные значения как Gradle properties. Пример не
содержит и не должен содержать client secret:

```powershell
pnpm android:build -- `
  -PandivumAuthProvider=auth0-supabase `
  -PandivumAuth0Domain=dev-example.eu.auth0.com `
  -PandivumAuthClientId=<android-client-id> `
  -PandivumAuthRedirectUri=andivum://android/auth/callback `
  -PandivumSupabaseUrl=https://<project-ref>.supabase.co `
  -PandivumSupabasePublishableKey=<publishable-key> `
  assembleDebug
```

Если эти параметры не переданы, debug Android использует local OpenIddict и
текущий `andivumApiBaseUrl`.

## Local development fallback

До появления внешних настроек:

```powershell
Copy-Item .env.example .env
pnpm install
pnpm dev:infra
pnpm dev:api
```

Локальный API доступен по адресу `https://localhost:7240`. Этот flow нужен для
сборки, API integration tests и offline-разработки. Он не должен использоваться
как production identity provider после включения Auth0.

Для Android physical smoke с USB debugging:

```powershell
adb reverse tcp:7240 tcp:7240
pnpm android:build:physical
```

Сертификат локального CA устанавливается на телефон как «Сертификат центра
сертификации». PFX/private key на телефон не переносится. Release Android не
получает debug trust configuration.

## Native cloud flow

1. Клиент читает Auth0 issuer, client ID и redirect URI из configuration.
2. Клиент генерирует `state`, `code_verifier` и `code_challenge` методом S256.
3. Клиент открывает Auth0 `/authorize` в системном браузере. Embedded WebView
   запрещён.
4. Пользователь регистрируется или входит по email/password на Auth0.
5. Auth0 возвращает authorization code на точный callback.
6. Клиент обменивает code на token endpoint, проверяет state и сохраняет
   credential state только в secure storage ОС.
7. Клиент отправляет Auth0 ID token вместе с Supabase publishable key, получает
   или создаёт `app_profiles` и только после успешного ответа открывает dashboard.
8. При истечении access token выполняется refresh с rotation. Если refresh
   невозможен, локальная сессия очищается и пользователь возвращается к входу.

## Passkeys

Passkey не является обязательным для первого email/password MVP. Auth0 native
passkeys для Android/iOS сейчас находятся в limited Early Access и требуют
custom domain, passkey policy, device settings и Passkey grant. Поэтому сначала
настраиваются обычный Universal Login и recovery, затем отдельный passkey
срез. Нельзя принимать passkey flow, пока не проверены его доступность для
выбранного Auth0 tenant и общий RP ID.

## Секреты и логирование

Разрешено хранить в приложении только значения, которые по модели Auth0/Supabase
являются публичными: Auth0 domain, client ID, Supabase URL и publishable key.
Нельзя коммитить или логировать:

- Auth0 client secret и Management API token;
- Supabase service-role/secret key;
- пароль PostgreSQL;
- access/refresh tokens;
- integration OAuth refresh tokens.

## Проверки

Локальный fallback:

```powershell
pnpm doctor
pnpm check
pnpm api:build
pnpm windows:build
git diff --check
```

Облачный flow дополнительно требует:

- mocked HTTP tests для отсутствующего issuer/JWT/RLS errors;
- Supabase migration/RLS integration test;
- Windows runtime smoke с Auth0;
- physical Android smoke с Auth0;
- один и тот же Auth0 subject на Windows и Android;
- проверку, что service-role key отсутствует в APK/MSIX и логах.

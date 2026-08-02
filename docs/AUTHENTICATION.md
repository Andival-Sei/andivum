# Authentication vertical slice

Andivum использует Supabase Auth как источник identity и Supabase как managed
data backend первого облачного MVP. Текущий локальный OpenIddict остаётся
явным dev fallback только для локальных API-тестов.

## Целевая облачная схема

```text
Windows/Android
      │ HTTPS: email/password
      ▼
Supabase Auth API
      │ access/refresh JWT session
      ▼
Supabase Data API / Storage
      │ auth.uid() + RLS
      ▼
app_profiles, Tasks, Finance
```

Supabase Auth хранит identity и выдаёт access/refresh-сессии. Пароли не
копируются в таблицы приложения и не попадают в логи. Supabase хранит
прикладной профиль и данные модулей.

## Идентификатор пользователя и RLS

`auth.users.id` — стабильный UUID identity. `app_profiles.user_id` ссылается на
него и используется как владелец строки. Email может измениться и не является
ключом авторизации.

```sql
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id)
```

Для будущих family spaces проверяется membership, а не только строка профиля.
Каждая таблица в exposed schema должна иметь RLS и explicit policies.

## Supabase configuration

В Supabase project `Andivum` включены email/password Auth и ротация refresh
токенов. Подтверждение email остаётся включённым для cloud-проекта.

Применяемые миграции находятся в `supabase/migrations`. Security и performance
advisors запускаются после изменения схемы. Supabase client получает URL и
publishable key из окружения приложения. Service-role key и пароль PostgreSQL в
native apps запрещены.

## Подключение настроек к локальным клиентам

Windows получает значения из окружения процесса. Рекомендуемый helper
`windows:run` дополнительно передаёт их упакованному WinUI-процессу через
launch-параметры, потому что package-aware запуск не обязан наследовать env
родительского Node-процесса:

```powershell
$env:ANDIVUM_AUTH_PROVIDER = "supabase"
$env:ANDIVUM_SUPABASE_URL = "https://<project-ref>.supabase.co"
$env:ANDIVUM_SUPABASE_PUBLISHABLE_KEY = "<publishable-key>"
pnpm windows:build
pnpm windows:run
```

`windows:build` только собирает приложение. Для запуска WinUI используйте
`windows:run`: эта команда запускает приложение через `dotnet run` и создаёт
необходимую служебную регистрацию Windows App SDK, а также передаёт текущую
Supabase-конфигурацию в приложение.

Android получает эти публичные значения как Gradle properties. Если в корне
есть неотслеживаемый `.env.andivum.local`, достаточно выполнить:

```powershell
pnpm android:build:cloud
```

Команда сама передаст cloud-значения в Gradle. Ручной вариант:

```powershell
pnpm android:build -- `
  "-PandivumAuthProvider=supabase" `
  "-PandivumSupabaseUrl=https://<project-ref>.supabase.co" `
  "-PandivumSupabasePublishableKey=<publishable-key>" `
  assembleDebug
```

Публикуемый ключ разрешено встраивать в клиентскую сборку. Service-role/secret
key в неё встраивать нельзя.

## Local development fallback

Локальный API доступен по адресу `https://localhost:7240` и нужен для API
integration tests и offline-разработки. Он не является production identity
provider. Для cloud Auth smoke используйте реальные Supabase URL и publishable
key из `.env.andivum.local`.

## Native cloud flow

1. Клиент читает Supabase URL и publishable key из configuration.
2. Пользователь вводит email/password в нативном экране.
3. Клиент отправляет данные в `/auth/v1/signup` или
   `/auth/v1/token?grant_type=password` по HTTPS.
4. После успешного ответа access/refresh tokens сохраняются только в secure
   storage ОС.
5. Клиент получает или создаёт `app_profiles` через Data API с Bearer access
   token и только после этого открывает dashboard.
6. При истечении access token выполняется
   `/auth/v1/token?grant_type=refresh_token` с rotation. Если refresh невозможен,
   локальная сессия очищается.
7. Выход вызывает `/auth/v1/logout` и очищает локальную копию токенов.

## Passkeys и внешние провайдеры

Passkey не является обязательным для первого email/password MVP. Supabase
passkeys находятся в Beta и требуют отдельной проверки WebAuthn/Relying Party
на Android и Windows. Google, Steam и другие OAuth-провайдеры подключаются
отдельными срезами и могут использовать системный браузер/OS UI.

## Секреты и логирование

Разрешено хранить в приложении только Supabase URL и publishable key. Нельзя
коммитить или логировать:

- Supabase service-role/secret key;
- пароль PostgreSQL;
- access/refresh tokens;
- integration OAuth refresh tokens.

## Проверки

```powershell
pnpm doctor
pnpm check
pnpm api:build
pnpm windows:build
git diff --check
```

Облачный flow дополнительно требует:

- mocked HTTP tests signup/sign-in/refresh/logout и profile bootstrap;
- Supabase migration/RLS integration test;
- Windows runtime smoke с Supabase Auth;
- physical Android smoke с Supabase Auth;
- один и тот же Supabase user ID на Windows и Android;
- проверку, что service-role key отсутствует в APK/MSIX и логах.

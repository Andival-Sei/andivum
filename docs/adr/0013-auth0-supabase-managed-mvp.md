# ADR 0013: Auth0 и Supabase для первого облачного MVP

- Статус: принято
- Дата: 2026-08-02
- Область: Identity, data access, Windows, Android, deployment
- Заменяет: способ аутентификации из [ADR 0012](0012-email-password-authentication.md)

## Контекст

Текущий authentication vertical slice содержит собственный ASP.NET Core
Identity/OpenIddict-сервер и локальную PostgreSQL-инфраструктуру. Это полезно для
разработки протокола, но для первого облачного MVP означает отдельный сервер,
обновления, сертификаты, хранение паролей и эксплуатацию базы.

Пользователю сейчас важнее быстро получить работающие Windows- и Android-
клиенты, чем самостоятельно обслуживать authentication server. При этом
Andivum должен сохранить системный браузер, Authorization Code + PKCE, будущие
passkeys, изоляцию данных и возможность позже добавить сложную серверную
логику.

## Варианты

### Вариант A: оставить ASP.NET Core Identity/OpenIddict

Даёт полный контроль над протоколом и данными, но требует собственного
постоянно работающего backend, базы, сертификатов, обновлений, recovery и
мониторинга уже на первом шаге.

### Вариант B: Auth0 + Supabase без собственного API на первом срезе

Auth0 хранит учётные данные и отвечает за вход. Supabase предоставляет
PostgreSQL, Data API, Storage и Row Level Security. Native-клиенты обращаются к
Supabase с publishable key и JWT Auth0; service-role key в приложения не
попадает.

Плюсы: нет собственного сервера для первого MVP, меньше DevOps, быстрый старт,
готовые email/password и hosted login, возможность добавить интеграции позднее.

Минусы: RLS и SQL становятся частью authorization boundary; сложные правила
нельзя реализовывать только в UI; для финансовых операций, cross-module events,
webhooks и provider secrets понадобятся Supabase Edge Functions или тонкий API.

### Вариант C: Supabase Auth + Supabase

Ещё проще для прямого доступа к базе, но не используется, потому что выбранный
пользователем Auth0 должен быть единственным владельцем identity. Одновременное
использование Auth0 и Supabase Auth создало бы две системы пользователей и
усложнило бы passkeys, logout и миграцию.

### Вариант D: Auth0 + тонкий API + Supabase

Это целевая форма для сложных модулей: API проверяет JWT Auth0, владеет
use-case-ами и секретами интеграций, Supabase остаётся managed PostgreSQL и
Storage. Вводится после появления требований, которые нельзя безопасно
выразить RLS/RPC/Edge Functions.

## Решение

Для первого облачного MVP принимается вариант B с подготовленной миграцией к
варианту D.

1. Auth0 Database Connection является единственным хранилищем identity для
   email/password. Пароли, passkey credentials и recovery-данные не копируются
   в Supabase.
2. Windows и Android регистрируются в Auth0 как Native Applications. Оба клиента
   используют системный браузер и Authorization Code + PKCE с `S256`; embedded
   WebView и password grant запрещены.
3. Supabase подключается к Auth0 через Third-party Auth integration. Auth0
   Action добавляет в ID token literal claim `role=authenticated`, который
   необходим Supabase для назначения роли `authenticated`.
4. Нативные клиенты используют только Supabase publishable key. Каждая
   открытая таблица получает RLS-политики. Service-role key, database password,
   Auth0 Management API token и integration secrets никогда не попадают в
   клиента.
5. Supabase `app_profiles` хранит прикладную проекцию пользователя:
   внутренний UUID, immutable Auth0 `sub`, display name, locale, theme и даты.
   `sub`, а не email, является связью между identity и данными. Email не
   используется как первичный ключ и не участвует в authorization.
6. Для Auth0 subjects используется текстовое сравнение через
   `auth.jwt() ->> 'sub'`; нельзя без проверки приводить значения вроде
   `auth0|...` к UUID. Персональные таблицы и будущие membership-таблицы
   проверяют subject/space membership на уровне RLS.
7. Пока внешний Auth0 tenant и Supabase project не настроены, локальный
   OpenIddict остаётся только dev fallback, чтобы не блокировать сборку и
   тесты. Production-конфигурация не использует его ephemeral keys.
8. Сложные денежные команды, интеграции, webhooks и межмодульные события не
   получают право на произвольный direct table access. Они будут вынесены в
   SQL RPC с безопасными границами, Supabase Edge Functions или тонкий API — по
   отдельному ADR и threat review.

## Security review

- Auth0 и Supabase не образуют две конкурирующие системы логина: Auth0 владеет
  identity, Supabase только проверяет внешний JWT и хранит данные приложения.
- RLS является обязательной защитой, а не удобной фильтрацией в клиенте.
- Native applications являются public clients и не содержат client secret.
- ID/access/refresh tokens не логируются; локальное хранение остаётся за
  защищённым хранилищем ОС.
- Redirect URI, issuer, signing algorithm и audience настраиваются явно.
- Для Supabase используется асимметричная подпись Auth0, совместимая с
  Third-party Auth; HS256 и PS256 не допускаются этой интеграцией.
- Native passkeys Auth0 на Android/iOS на текущий момент находятся в limited
  Early Access и требуют custom domain. Поэтому passkey не блокирует email/
  password MVP и будет подключаться после настройки домена; hosted Universal
  Login остаётся допустимым промежуточным вариантом.

## Последствия

Первый облачный срез можно запускать без купленного VPS и без отдельного
authentication server. Цена — зависимость от Auth0/Supabase, необходимость
аккуратно писать RLS и ограничение прямого клиентского доступа для сложной
бизнес-логики. Архитектура сохраняет путь к отдельному API: native clients,
Auth0 subjects и модульные данные не должны зависеть от конкретной реализации
первого backend-слоя.

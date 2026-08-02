# Спецификация: нативная Supabase Auth авторизация

## Цель

Заменить Auth0 на Supabase Auth и дать пользователю полностью нативный
email/password flow на Windows и Android.

## Пользовательское поведение

1. На экране входа пользователь вводит email и пароль.
2. Кнопка «Войти» отправляет данные в Supabase Auth без открытия браузера.
3. Кнопка «Создать аккаунт» регистрирует пользователя.
4. Если подтверждение email включено, приложение сообщает, что нужно открыть
   письмо и подтвердить адрес.
5. После успешного входа приложение создаёт или получает личный профиль и
   показывает dashboard.
6. При перезапуске access token обновляется через refresh token, если это
   возможно. При окончательной недействительности refresh token локальная
   сессия очищается.
7. Выход удаляет локальную сессию и завершает серверную Supabase-сессию.
8. Один email/password account даёт одну и ту же личную область на Windows и
   Android.

## Технический контракт

- Auth base: `{SUPABASE_URL}/auth/v1`.
- Sign up: `POST /signup`, JSON `{ email, password }`.
- Sign in: `POST /token?grant_type=password`, JSON `{ email, password }`.
- Refresh: `POST /token?grant_type=refresh_token`, JSON `{ refresh_token }`.
- Sign out: `POST /logout` с текущим Bearer access token.
- Profile: Data API `app_profiles` с publishable key и Bearer access token.
- Токены сохраняются в Android Keystore-backed storage и Windows PasswordVault.

## Схема данных

`public.app_profiles` содержит `user_id uuid not null references auth.users(id)`
и больше не содержит `auth0_subject`. Все policies ограничивают строки
выражением `((select auth.uid()) = user_id)`.

## Не входит в этот срез

- Google/Steam/OAuth integrations;
- passkey registration/sign-in;
- MFA;
- полноценный account settings UI;
- перенос существующих Auth0-пользователей: в текущем проекте таблица профилей
  пуста, поэтому выполняется чистая миграция identity.

## Критерии приёмки

- Windows tests проверяют signup/signin/refresh/logout, заголовки и отсутствие
  Auth0-параметров.
- Android instrumentation tests проверяют те же HTTP-контракты.
- RLS migration проходит локальную/удалённую проверку и advisors не находят
  security/performance проблем.
- `rg -i "auth0"` не находит active runtime/config references; исторические ADR
  и журнал могут содержать слово Auth0 как описание заменённого решения.
- Реальный cloud smoke подтверждает регистрацию/вход на Windows и Android,
  один профиль и отсутствие браузерного окна для password flow.

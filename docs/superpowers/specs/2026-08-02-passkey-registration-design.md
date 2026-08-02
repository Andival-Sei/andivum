# Passkey registration slice

## Цель

Пользователь без существующего аккаунта может начать регистрацию из OIDC
authentication surface, создать первый passkey и вернуться в тот же native
Authorization Code + PKCE flow.

## Наблюдаемое поведение

1. Анонимный пользователь открывает `/connect/authorize` и видит действия входа
   и создания аккаунта с passkey.
2. Регистрация не требует пароля или электронной почты. Сервер создаёт
   неприводимое к пользователю техническое имя Identity-аккаунта и использует
   заданное имя passkey только для отображения в authenticator.
3. До успешной attestation пользователь не считается готовым к выдаче OAuth
   authorization code.
4. После успешной attestation браузерная сессия продолжает исходный authorize
   request; native client получает authorization code и обменивает его по PKCE.
5. Ошибки регистрации не раскрывают наличие чужого аккаунта и не логируют
   credential JSON, tokens или authorization codes.
6. Регистрация ограничена теми же правилами имени passkey и максимальным
   количеством passkeys, что и добавление passkey в существующий аккаунт.

## Границы

В этот срез входят server-rendered registration surface, pending browser
session, создание первого Identity user, WebAuthn attestation и защита
authorize от аккаунта без passkey. В него не входят recovery, смена имени
аккаунта, email verification, multi-factor authentication и device management.

## Безопасность

- Pending account не может получить OAuth code до сохранённой passkey.
- Anti-forgery token обязателен для mutating registration requests.
- Native clients по-прежнему используют public-client PKCE S256.
- Production certificates, RP ID и origins остаются отдельным deployment
  решением.

## Проверка

- API integration test подтверждает registration surface и регистрацию без
  passkey не проходит authorize.
- Policy tests подтверждают ограничения имени и состояния аккаунта.
- Полный browser/WebAuthn smoke выполняется отдельно на доступном устройстве;
  отсутствие ADB-устройства не превращается в зелёный device QA.

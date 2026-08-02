# Feature: Auth0 + Supabase для первого облачного MVP

- Дата: 2026-08-02
- Статус: accepted
- Владелец: AI под продуктовым руководством владельца Andivum

## Намерение

Пользователь должен зарегистрироваться и войти на Windows или Android по
email/паролю через Auth0, увидеть своё пустое личное пространство и использовать
один и тот же аккаунт на обоих устройствах. Для этого не требуется собственный
постоянно работающий сервер.

Пароль вводится только на hosted login Auth0 в системном браузере. Supabase
хранит прикладной профиль и данные модулей, но не пароль и не passkey secrets.

## Наблюдаемое поведение

1. Given приложение запущено без сессии, when пользователь нажимает вход,
   then Windows/Android открывает системный браузер на Auth0 Universal Login.
2. Given пользователь вводит email/пароль или регистрирует аккаунт в Auth0,
   then Auth0 возвращает приложение по заранее зарегистрированному redirect URI
   через Authorization Code + PKCE, а пароль не попадает в приложение.
3. Given callback успешно обработан, when клиент обращается к Supabase,
   then он использует только publishable key и JWT Auth0, сохранённый в
   защищённом хранилище ОС.
4. Given у Auth0 subject ещё нет прикладного профиля, when пользователь впервые
   открывает приложение, then создаётся ровно один `app_profiles` с этим
   immutable subject.
5. Given один и тот же Auth0 subject входит на Windows и Android, then оба
   клиента получают один и тот же профиль и не видят профиль другого subject.
6. Given пользователь не авторизован или JWT просрочен, when он запрашивает
   данные, then Supabase отклоняет запрос политиками Data API/RLS, а приложение
   предлагает войти снова.
7. Given пользователь выходит, then локальные токены и локальная сессия
   очищаются; browser logout добавляется в отдельном auth-lifecycle срезе.

## Границы

### Входит

- Auth0 Native Applications для Windows и Android.
- Authorization Code + PKCE с `S256` через системный браузер.
- Auth0 Database Connection с email/password.
- Supabase Third-party Auth integration и Auth0 Action с
  `role=authenticated` в ID token.
- Supabase `app_profiles`, RLS и direct profile/session request.
- Конфигурация через environment/local secrets без коммита реальных значений.
- Локальный fallback только для разработки до подключения внешних проектов.

### Не входит

- Хранение паролей, passkeys или Auth0 Management API credentials в Supabase.
- Supabase Auth как второй источник пользователей.
- Production deployment, billing, custom domain и выбор тарифов.
- Полная миграция существующих production accounts (их пока нет).
- Сложные Finance/Tasks commands, integrations, webhooks и cross-module events.
- Нативные passkey APIs Auth0 до подтверждения доступности Early Access и
  настройки custom domain.

## Ошибки и восстановление

- Неверный/неполный Auth0 configuration: понятная ошибка конфигурации в
  development, без silent fallback в production.
- Несовпадение callback/redirect URI: показать инструкцию по Auth0 Dashboard,
  не принимать callback с другим state/PKCE.
- Невалидный или просроченный JWT: очистить локальную сессию после невозможности
  refresh и открыть hosted login.
- Ошибка RLS/profile bootstrap: не открывать dashboard как будто вход выполнен;
  показать повторяемую ошибку и correlation id без токена.
- Сетевые ошибки Supabase: сохранить только безопасное локальное состояние и
  повторить запрос, не обходя RLS.

## Данные и безопасность

- Identity source: Auth0 `sub`.
- App data source: Supabase Postgres/Data API/Storage.
- Связь: `app_profiles.auth0_subject` и RLS через
  `(select auth.jwt() ->> 'sub')`.
- Публичные в клиенте значения: Auth0 domain/client ID, Supabase URL и
  publishable key. Они не являются секретами, но сами по себе не дают доступ к
  чужим строкам при корректном RLS.
- Запрещённые в клиенте значения: Supabase service-role key, database password,
  Auth0 client secret/Management token, integration refresh tokens.
- Все таблицы в exposed schema получают RLS, explicit `authenticated` policies
  и индексы по колонкам, используемым в политике.

## Платформенные различия

- Windows: generic OIDC/AppAuth-like PKCE client, системный браузер и
  зарегистрированный `andivum://windows/auth/callback` либо Auth0-compatible
  HTTPS callback после выбора домена.
- Android: AppAuth + Custom Tabs/системный browser; production предпочитает
  verified Android App Links, custom scheme разрешён только как контролируемый
  development fallback.
- Оба клиента используют native secure storage и один и тот же набор
  acceptance cases, но не общий runtime-код.

## Критерии приёмки

- [ ] Auth0 domain и отдельные client IDs Windows/Android читаются из окружения.
- [ ] Оба клиента используют Auth0 discovery, `S256` PKCE и system browser.
- [ ] Регистрация и вход по email/password проходят через Auth0 Universal Login.
- [ ] Supabase принимает JWT Auth0 только после Third-party Auth integration и
      `role=authenticated` Action.
- [ ] RLS не даёт одному subject читать/изменять профиль другого subject.
- [ ] Пароли и секреты отсутствуют в репозитории, APK, MSIX и логах.
- [ ] Один Auth0 subject получает один app profile на Windows и Android.
- [ ] Local OpenIddict fallback не активируется в production profile.
- [ ] Physical Android smoke и Windows runtime smoke выполнены после выдачи
      реальных Auth0/Supabase settings.

## Проверки

- [ ] Unit
- [ ] Integration
- [ ] Windows UI
- [ ] Android UI
- [ ] Security
- [ ] Localization/theme/accessibility

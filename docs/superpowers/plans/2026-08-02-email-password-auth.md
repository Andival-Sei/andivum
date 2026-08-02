# План: email/password authentication

- Статус: реализовано; ручной ввод реального аккаунта в браузере оставлен за
  владельцем, чтобы не использовать его пароль в автоматизации
- Спецификация: `docs/superpowers/specs/2026-08-02-email-password-auth.md`
- ADR: `docs/adr/0012-email-password-authentication.md`

1. [x] Прочитать auth-контекст и подтвердить ожидаемый RED тест.
2. [x] Добавить failing-тесты для email/password страницы, регистрации и входа.
3. [x] Реализовать server-rendered login/register с anti-forgery и Identity
      lockout, не добавляя password grant.
4. [x] Убрать требование passkey из authorize после email/password входа и
      оставить подключение passkey только authenticated settings.
5. [x] Обновить тексты Windows/Android и синхронизировать product/auth docs.
6. [x] Запустить API, Windows и Android проверки; отдельно проверить локальный
      браузерный flow без публикации секретов.
7. [x] Выполнить security review, `pnpm check`, `git diff --check`, commit и
      push только связанных изменений.

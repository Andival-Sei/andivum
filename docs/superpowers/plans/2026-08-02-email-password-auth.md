# План: email/password authentication

- Статус: в работе
- Спецификация: `docs/superpowers/specs/2026-08-02-email-password-auth.md`
- ADR: `docs/adr/0012-email-password-authentication.md`

1. [x] Прочитать auth-контекст и подтвердить ожидаемый RED тест.
2. [x] Добавить failing-тесты для email/password страницы, регистрации и входа.
3. [ ] Реализовать server-rendered login/register с anti-forgery и Identity
      lockout, не добавляя password grant.
4. [ ] Убрать требование passkey из authorize после email/password входа и
      оставить подключение passkey только authenticated settings.
5. [ ] Обновить тексты Windows/Android и синхронизировать product/auth docs.
6. [ ] Запустить API, Windows и Android проверки; отдельно проверить локальный
      браузерный flow без публикации секретов.
7. [ ] Выполнить security review, `pnpm check`, `git diff --check`, commit и
      push только связанных изменений.

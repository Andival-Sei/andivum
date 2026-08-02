# План: защищённая сессия

- Статус: реализовано; осталось ручное подтверждение Windows Hello и delivery
- Спецификация: `docs/superpowers/specs/2026-08-02-protected-session.md`

1. [x] Зафиксировать понятный язык сообщений и обязательный журнал проблем.
2. [x] Добавить failing-тесты контракта сервера, refresh и session validation.
3. [x] Реализовать offline access, refresh и защищённый endpoint flow на
   Windows и Android.
4. [x] Подключить результат проверки к dashboard обоих клиентов.
5. [x] Запустить unit/API/instrumentation проверки и physical Android smoke.
6. [x] Обновить документацию; commit и push выполняются после финального review.

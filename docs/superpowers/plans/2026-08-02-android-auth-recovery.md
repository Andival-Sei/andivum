# План: восстановление Android-входа

- [x] Воспроизвести сценарий через AppAuth, browser custom tab и logcat.
- [x] Проверить сохранение AuthState и путь `onCreate` с `isBusy`.
- [x] Сначала добавить падающие Node и Android проверки.
- [x] Сохранять AuthState только после успешного token exchange.
- [x] Очищать невалидное состояние после ошибок callback/refresh.
- [x] Добавить явный Android redirect receiver и кнопку отмены.
- [x] Запустить cloud build и instrumentation на физическом телефоне.
- [ ] Повторить полноценный вход с пользователем на установленном APK.

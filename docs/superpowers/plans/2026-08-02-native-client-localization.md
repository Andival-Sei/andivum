# План: локализация нативных клиентов

- Связанная spec: `docs/superpowers/specs/2026-08-02-native-client-localization.md`
- Ветка: `agent/native-client-localization`
- Статус: planned

## Предварительное решение

Сейчас runtime-реализация отложена: в репозитории ещё нет Windows и Android
client shell-проектов. В текущем foundation-срезе добавляется только контракт
политики в product configuration и документация решения.

## Задачи

### 1. Создать Windows client shell с ресурсами

- Изменяемые файлы: `apps/windows/Andivum/**`.
- RED: тест resolver-а должен падать на неизвестной системной локали до
  реализации fallback `ru-RU`.
- GREEN: добавить `.resw`, `ResourceLoader`, ручной override и применение
  настройки до создания окна.
- REFACTOR: вынести locale policy и pluralization в тестируемые platform
  services.
- Проверка: unit tests, `dotnet build`, Windows UI smoke test.

### 2. Создать Android client shell с ресурсами

- Изменяемые файлы: `apps/android/**`.
- RED: Compose test должен падать, если `System`, `English` и `Русский` не
  меняют отображаемую строку.
- GREEN: добавить `strings.xml`, `values-ru`, AndroidX per-app locales и
  settings UI.
- REFACTOR: вынести locale preference в ViewModel/settings boundary.
- Проверка: unit tests, `./gradlew test`, Compose UI test.

### 3. Добавить межплатформенную проверку переводов

- Изменяемые файлы: `tools/scripts/i18n-check*.mjs`, `package.json`.
- RED: fixture с отсутствующим ключом, лишним ключом и несовпадающим
  placeholder должен завершаться ошибкой.
- GREEN: реализовать `pnpm i18n:check --json` с машинно-читаемым выводом.
- REFACTOR: переиспользовать parser и диагностические коды без привязки к UI.
- Проверка: `pnpm i18n:check`, unit tests и CI.

### 4. Проверить API error code boundary

- Изменяемые файлы: соответствующие contracts/backend/client adapters.
- RED: тест должен запрещать локализованный `message` как единственный
  источник ошибки.
- GREEN: возвращать стабильный `code` и локализовать его на клиенте.
- Проверка: API contract/integration tests.

## Общие проверки

```powershell
pnpm check
pnpm test
git diff --check
```

## Review

- [ ] Соответствие ADR 0008 и spec
- [ ] Нет общего runtime localization framework
- [ ] Unknown/unsupported system locale получает `ru-RU`
- [ ] Platform QA отражает реально выполненные проверки
- [ ] Accessibility и pluralization проверены

## Доставка

- [ ] Commit
- [ ] Push
- [ ] CI
- [ ] PR/merge
- [ ] HEAD синхронизирован с remote

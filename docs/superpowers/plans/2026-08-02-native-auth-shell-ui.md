# План: нативный auth shell UI

- Связанная spec: `docs/superpowers/specs/2026-08-02-native-auth-shell-ui.md`
- Ветка: `agent/passkey-registration`
- Статус: in-progress

## Задачи

### 1. Проверяемое состояние login/dashboard

- Изменяемые файлы: Android presentation state, Windows presentation state и
  соответствующие тесты.
- RED: тесты должны падать, пока состояния signed-in и signed-out не имеют
  явного перехода между экранами.
- GREEN: добавить минимальные state-модели, на которых строится UI.
- REFACTOR: убрать дублирование переходов и не смешивать auth protocol с UI.
- Проверка: Android instrumentation и `dotnet test` для Windows state tests.

### 2. Реализовать Android UI

- Изменяемые файлы: `apps/android/app/src/main/**` и ресурсы.
- RED: state/UI test должен отличать login surface от dashboard surface.
- GREEN: Compose login screen, loading/error states, dashboard placeholder,
  system theme и локализованные строки.
- REFACTOR: вынести цвета и общие spacing tokens в нативную тему.
- Проверка: Android build, instrumentation test, физический Pixel screenshot.

### 3. Реализовать Windows UI

- Изменяемые файлы: `apps/windows/MainPage.xaml`, ViewModel, native resources
  и Windows state test project.
- RED: state test должен показывать, что сохранённая сессия открывает shell.
- GREEN: WinUI login surface, dashboard placeholder, resource-based strings и
  visibility transitions.
- REFACTOR: оставить OS-specific layout в XAML, а auth state — в ViewModel.
- Проверка: `pnpm windows:build` и реальный Windows UI smoke.

### 4. Сквозная проверка и доставка

- Изменяемые файлы: только файлы из предыдущих задач и документация статуса.
- Проверка: `pnpm check`, Android physical build, `git diff --check`.
- Review: сравнить критерии spec, убедиться, что auth callback и secure
  storage не изменены небезопасно.
- Доставка: commit, push и обновление PR.

## Общие проверки

```powershell
pnpm check
pnpm windows:build
pnpm android:build:physical
git diff --check
git status --short --branch
```

## Review

- [ ] Соответствие spec
- [ ] Login/dashboard не смешаны в один технический экран
- [ ] Нет токенов и секретов в UI, логах или тестах
- [ ] Android и Windows остаются нативными
- [ ] Platform QA отражает реально выполненные проверки

## Доставка

- [ ] Commit
- [ ] Push
- [ ] CI
- [ ] PR/merge
- [ ] HEAD синхронизирован с remote

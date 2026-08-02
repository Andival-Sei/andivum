# План: AI development foundation

- Связанная spec:
  `docs/superpowers/specs/2026-08-02-autonomous-development-design.md`
- Ветка: `main` (документальный foundation до включения feature-branch policy)
- Статус: in-progress

## Задачи

1. Добавить постоянный процесс, ADR и шаблоны.
2. Добавить совместимые инструкции для GitHub/Copilot.
3. Через TDD реализовать `pnpm doctor` с machine-readable режимом.
4. Проверить фактический Windows/Android toolchain.
5. Обновить foundation checks и roadmap.
6. Запустить проверки, commit, push и проверить CI.

## Проверки

```powershell
pnpm test
pnpm check
deno fmt --check
git diff --check
```

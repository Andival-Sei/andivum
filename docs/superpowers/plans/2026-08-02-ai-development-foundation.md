# План: AI development foundation

- Связанная spec:
  `docs/superpowers/specs/2026-08-02-autonomous-development-design.md`
- Ветка: `main` (документальный foundation до включения feature-branch policy)
- Статус: in-progress

## Задачи

1. [x] Добавить постоянный процесс, ADR и шаблоны.
2. [x] Добавить совместимые инструкции для GitHub/Copilot.
3. [x] Через TDD реализовать `pnpm run doctor` с machine-readable режимом.
4. [x] Проверить фактический Windows/Android toolchain.
5. [x] Обновить foundation checks и roadmap.
6. [ ] Запустить локальные проверки; commit, push и CI завершают delivery.

## Проверки

```powershell
pnpm test
pnpm check
deno fmt --check README.md AGENTS.md .github/copilot-instructions.md docs tools/scripts package.json
git diff --check
```

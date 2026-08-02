# План: <название>

- Связанная spec: `<path>`
- Ветка: `agent/<description>`
- Статус: planned | in-progress | completed

## Задачи

### 1. <Проверяемый результат>

- Изменяемые файлы: ...
- RED: тест и ожидаемая причина падения.
- GREEN: минимальное production-поведение.
- REFACTOR: допустимое упрощение после Green.
- Проверка: точная команда.

## Общие проверки

```powershell
pnpm check
pnpm test
git diff --check
```

## Review

- [ ] Соответствие spec
- [ ] Нет лишнего scope
- [ ] Security review, если применимо
- [ ] Platform QA отражает реально выполненные проверки

## Доставка

- [ ] Commit
- [ ] Push
- [ ] CI
- [ ] PR/merge
- [ ] HEAD синхронизирован с remote

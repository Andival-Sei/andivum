# Журнал архитектурных решений

| ADR                                              | Решение                                  | Статус  |
| ------------------------------------------------ | ---------------------------------------- | ------- |
| [0001](adr/0001-native-platform-clients.md)      | Два нативных клиента                     | Принято |
| [0002](adr/0002-modular-monolith.md)             | Modular monolith backend                 | Принято |
| [0003](adr/0003-passkey-authentication.md)       | Passkeys через OIDC + PKCE               | Принято |
| [0004](adr/0004-shared-contracts-not-runtime.md) | Общие контракты вместо общего runtime    | Принято |
| [0005](adr/0005-product-identity.md)             | Разделение display name и технических ID | Принято |
| [0006](adr/0006-cli-first-ai-tooling.md)         | CLI-first tooling, MCP как адаптер       | Принято |
| [0007](adr/0007-autonomous-ai-development.md)    | Автономная spec-first TDD-разработка     | Принято |
| [0008](adr/0008-native-client-localization.md)   | Нативная локализация клиентов            | Принято |
| [0009](adr/0009-authentication-slice.md)         | Первый authentication vertical slice    | Принято |
| [0010](adr/0010-passwordless-account-bootstrap.md) | Passwordless account bootstrap без почты | Заменён 0012 |
| [0011](adr/0011-protected-native-session.md)     | Проверка и обновление native-сессии     | Принято |
| [0012](adr/0012-email-password-authentication.md) | Email/password через OIDC browser auth | Принято |

## Открытые решения

- production hosting и cloud provider;
- постоянный домен и passkey Relying Party ID;
- генератор C#/Kotlin клиентов из OpenAPI;
- формат и политика полного offline conflict resolution;
- модель семейных пространств и совместного доступа;
- лицензия репозитория;
- финальное название продукта.

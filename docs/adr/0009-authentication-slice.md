# ADR 0009: Первый authentication vertical slice

- Статус: принято
- Дата: 2026-08-02

## Контекст

Andivum должен поддерживать passkeys на Windows и Android, а клиенты должны
получить одинаковую серверную модель сессии. При этом UI и системные APIs
останутся нативными для каждой платформы.

## Решение

Первый slice фиксирует ASP.NET Core Identity + OpenIddict + PostgreSQL как
серверную границу. Native clients используют public-client Authorization Code +
PKCE с обязательным S256 и точными custom-scheme redirect URI. Passkey ceremony
проходит в системной browser/authentication session; встроенный WebView не
используется. OpenAPI описывает стабильную часть контракта, а OIDC discovery
остаётся источником динамических endpoint metadata.

Development использует только localhost RP ID и ephemeral OpenIddict keys.
Production обязан предоставить явные RP ID, HTTPS origins и persistent signing/
encryption certificates; сервер отказывается стартовать без них.

## Последствия

Windows и Android смогут реализовать клиентов независимо, не разделяя UI
runtime. Нам всё ещё нужны нативные secure storage, account recovery,
logout-all-devices, регистрация первого passkey и device-smoke тесты.

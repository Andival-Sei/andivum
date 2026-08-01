# ADR 0003: Passkeys через OIDC Authorization Code + PKCE

- Статус: принято
- Дата: 2026-08-02

## Контекст

Windows и Android должны поддерживать passkeys с первого authentication slice; в
будущем может появиться веб-клиент.

## Решение

ASP.NET Core Identity отвечает за passkey/WebAuthn ceremony, OpenIddict — за
OIDC/OAuth 2.0. Нативные приложения используют Authorization Code + PKCE через
системную authentication session. Embedded WebView запрещён.

## Последствия

Один протокол обслуживает обе платформы и будущий web. Нужны HTTPS-домен,
фиксированный RP ID, account recovery, безопасная ротация refresh tokens и
защищённое хранилище ОС.

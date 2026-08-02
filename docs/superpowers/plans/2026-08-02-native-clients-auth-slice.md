# Native clients authentication slice

## Goal

Дать Windows и Android минимальные нативные оболочки, которые используют
единый OIDC Authorization Code + PKCE контракт backend и системный browser flow.

## Delivered

- [x] Windows WinUI 3 shell на C#/.NET 10 с Windows App SDK 1.8.x.
- [x] Windows PKCE S256, system browser launch, protocol callback registration и
      хранение токенов в Windows `PasswordVault`.
- [x] Android Kotlin/Jetpack Compose shell на AGP 9.0.1, Gradle 9.1 и Kotlin
      2.4.0.
- [x] Android AppAuth Custom Tabs flow, PKCE S256 и callback через
      `RedirectUriReceiverActivity`.
- [x] Android auth state encrypted with AES-GCM key from Android Keystore.
- [x] Стартовые `en-US`/`ru-RU` ресурсы для нативного UI.
- [x] Команды `pnpm windows:build` и `pnpm android:build`.
- [x] Android debug APK установлен и запущен на `Medium_Phone_API_36.1`.

## Deferred device checks

- Windows packaged callback и реальный Windows Hello passkey ceremony.
- Android emulator HTTPS trust for `10.0.2.2` and full passkey/token exchange.
- Physical Android device and Credential Manager provider.
- Automated UI tests and CI jobs on Windows/Android runners.

## Verification

```powershell
pnpm check
pnpm windows:build
pnpm android:build
```

import assert from "node:assert/strict";
import test from "node:test";

import { validateProductConfiguration } from "./product-config-lib.mjs";

test("system locale fallback must be one of the supported locales", () => {
  const errors = validateProductConfiguration({
    displayName: "Andivum",
    defaultLocale: "en-US",
    systemLocaleFallback: "ru-RU",
    supportedLocales: ["en-US", "ru-RU"],
    theme: { default: "system" },
  });

  assert.deepEqual(errors, []);
});

test("unsupported system locale fallback is rejected", () => {
  const errors = validateProductConfiguration({
    displayName: "Andivum",
    defaultLocale: "en-US",
    systemLocaleFallback: "de-DE",
    supportedLocales: ["en-US", "ru-RU"],
    theme: { default: "system" },
  });

  assert.deepEqual(errors, [
    "systemLocaleFallback должен входить в supportedLocales",
  ]);
});

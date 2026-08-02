import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import test from "node:test";

const root = resolve(import.meta.dirname, "../..");

test("Android password auth stays native and has a recoverable session path", async () => {
  const [manifest, authManager, mainActivity, russianStrings] = await Promise.all([
    readFile(resolve(root, "apps/android/app/src/main/AndroidManifest.xml"), "utf8"),
    readFile(
      resolve(
        root,
        "apps/android/app/src/main/java/io/github/andivalsei/andivum/AuthManager.kt",
      ),
      "utf8",
    ),
    readFile(
      resolve(
        root,
        "apps/android/app/src/main/java/io/github/andivalsei/andivum/MainActivity.kt",
      ),
      "utf8",
    ),
    readFile(resolve(root, "apps/android/app/src/main/res/values-ru/strings.xml"), "utf8"),
  ]);

  assert.doesNotMatch(manifest, /RedirectUriReceiverActivity/);
  assert.match(authManager, /fun signIn\(/);
  assert.match(authManager, /fun signUp\(/);
  assert.match(authManager, /fun clearSession\(\)/);
  assert.match(mainActivity, /onEmailChanged/);
  assert.match(mainActivity, /onPasswordChanged/);
  assert.match(mainActivity, /onSignUp/);
  assert.match(russianStrings, /name="auth_email_label"/);
  assert.match(russianStrings, /name="auth_password_label"/);
});

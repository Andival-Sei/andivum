import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import test from "node:test";

const root = resolve(import.meta.dirname, "../..");

test("Android auth flow has a recoverable callback and retry path", async () => {
  const [manifest, authManager, mainActivity, russianStrings] = await Promise.all([
    readFile(
      resolve(root, "apps/android/app/src/main/AndroidManifest.xml"),
      "utf8",
    ),
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
    readFile(
      resolve(root, "apps/android/app/src/main/res/values-ru/strings.xml"),
      "utf8",
    ),
  ]);

  assert.match(manifest, /xmlns:tools="http:\/\/schemas\.android\.com\/tools"/);
  assert.match(
    manifest,
    /android:name="net\.openid\.appauth\.RedirectUriReceiverActivity"[\s\S]*?tools:node="replace"[\s\S]*?android:scheme="andivum"[\s\S]*?android:host="android"[\s\S]*?android:path="\/auth\/callback"/,
  );
  assert.match(authManager, /fun clearSession\(\)/);
  assert.match(
    authManager,
    /tokenException != null[\s\S]*?stateStore\.clear\(\)/,
  );
  assert.match(
    authManager,
    /if \(exception != null \|\| accessToken\.isNullOrBlank\(\)\) \{\s+clearSession\(\)/,
  );
  assert.match(mainActivity, /onCancelSignIn/);
  assert.match(mainActivity, /authManager\.clearSession\(\)/);
  assert.match(mainActivity, /R\.string\.auth_cancel/);
  assert.match(russianStrings, /name="auth_cancel"/);
});

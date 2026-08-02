import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import test from "node:test";

const root = resolve(import.meta.dirname, "../..");

test("Android cloud build loads public values from the local cloud env file", async () => {
  const [packageJson, script] = await Promise.all([
    readFile(resolve(root, "package.json"), "utf8"),
    readFile(resolve(root, "tools/scripts/run-android-gradle.ps1"), "utf8"),
  ]);

  assert.match(packageJson, /"android:build:cloud":\s*"powershell/);
  assert.match(script, /\[switch\]\s+\$Cloud/);
  assert.match(script, /\.env\.andivum\.local/);
  assert.match(script, /andivumAuthProvider/);
  assert.match(script, /andivumAuth0Domain/);
  assert.match(script, /andivumAuthClientId/);
  assert.match(script, /andivumAuthRedirectUri/);
  assert.match(script, /andivumSupabaseUrl/);
  assert.match(script, /andivumSupabasePublishableKey/);
});

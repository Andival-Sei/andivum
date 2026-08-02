import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import test from "node:test";

const root = resolve(import.meta.dirname, "../..");
const actionPath = resolve(
  root,
  "infra/auth0/actions/andivum-supabase-role-claim.js",
);

test("Andivum Auth0 Action scopes the Supabase claim to Andivum clients", async () => {
  const source = await readFile(actionPath, "utf8");

  assert.match(source, /event\.client\?\.client_id/);
  assert.match(source, /C3mYmpsm3g0Bs3e09bMyHTw0sXiTKCeV/);
  assert.match(source, /Gotbwwp9n3FNEv4TQ18KKYuKdxMGzSbO/);
  assert.match(source, /api\.idToken\.setCustomClaim\(['"]role['"], ['"]authenticated['"]\)/);
  assert.doesNotMatch(source, /api\.accessToken\.setCustomClaim/);
});

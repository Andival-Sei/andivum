import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import test from "node:test";

const root = resolve(import.meta.dirname, "../..");

test("Supabase Auth site URL belongs to Andivum instead of the previous local app", async () => {
  const config = await readFile(resolve(root, "supabase/config.toml"), "utf8");

  assert.match(config, /site_url\s*=\s*"https:\/\/[a-z0-9]{20}\.supabase\.co"/);
  assert.doesNotMatch(config, /site_url\s*=\s*"http:\/\/localhost:3000"/);
});

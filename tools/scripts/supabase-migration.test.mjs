import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import test from "node:test";

const root = resolve(import.meta.dirname, "../..");
const migrationPath = resolve(
  root,
  "supabase/migrations/20260802143025_create_app_profiles.sql",
);

test("app profile migration binds ownership to Auth0 subject and enables RLS", async () => {
  const sql = await readFile(migrationPath, "utf8");

  assert.match(sql, /create table public\.app_profiles/i);
  assert.match(sql, /auth0_subject text not null default \(auth\.jwt\(\) ->> 'sub'\) unique/i);
  assert.match(sql, /alter table public\.app_profiles enable row level security/i);
  assert.equal((sql.match(/create policy /gi) ?? []).length, 3);
  assert.equal((sql.match(/to authenticated/gi) ?? []).length, 4);
  assert.match(sql, /grant select, insert, update on table public\.app_profiles to authenticated/i);
  assert.equal((sql.match(/auth\.jwt\(\)\s*->>\s*'sub'/gi) ?? []).length, 5);
});

test("app profile migration does not grant client access to service-role or credentials", async () => {
  const sql = await readFile(migrationPath, "utf8");

  assert.doesNotMatch(sql, /grant .*service_role/i);
  assert.doesNotMatch(sql, /password|secret|refresh[_ -]?token/i);
  assert.doesNotMatch(sql, /auth\.users/i);
});

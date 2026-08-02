import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import test from "node:test";

const root = resolve(import.meta.dirname, "../..");
const migrationUpdatePath = resolve(
  root,
  "supabase/migrations/20260802183647_migrate_app_profiles_to_supabase_auth.sql",
);
const securityMigrationPath = resolve(
  root,
  "supabase/migrations/20260802155444_revoke_auto_rls_function_execute.sql",
);

test("cloud update moves profile ownership to Supabase Auth only with an empty table", async () => {
  const sql = await readFile(migrationUpdatePath, "utf8");

  assert.match(sql, /if exists \(select 1 from public\.app_profiles limit 1\)/i);
  assert.match(sql, /raise exception/i);
  assert.match(sql, /add column user_id uuid/i);
  assert.match(sql, /alter column user_id set default auth\.uid\(\)/i);
  assert.match(sql, /references auth\.users \(id\) on delete cascade/i);
  assert.match(sql, /drop column if exists auth0_subject/i);
  assert.match(sql, /add constraint app_profiles_user_id_fkey/i);
  assert.match(sql, /using \(\(select auth\.uid\(\)\) = user_id\)/i);
  assert.match(sql, /with check \(\(select auth\.uid\(\)\) = user_id\)/i);
  assert.doesNotMatch(sql, /grant .*service_role/i);
  assert.doesNotMatch(sql, /password|secret|refresh[_ -]?token/i);
});

test("automatic RLS helper is not executable by public API roles", async () => {
  const sql = await readFile(securityMigrationPath, "utf8");

  assert.match(
    sql,
    /revoke execute on function public\.rls_auto_enable\(\) from public, anon, authenticated/i,
  );
});

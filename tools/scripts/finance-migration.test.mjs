import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const migration = readFileSync(
  "supabase/migrations/20260803090000_finance_module.sql",
  "utf8",
);

test("finance migration has isolated tables and row level security", () => {
  assert.match(migration, /create table public\.finance_accounts/i);
  assert.match(migration, /create table public\.finance_categories/i);
  assert.match(migration, /create table public\.finance_transactions/i);
  assert.match(migration, /create table public\.finance_transaction_items/i);
  assert.match(migration, /enable row level security/i);
  assert.match(migration, /select auth\.uid\(\)/i);
});

test("finance migration writes transactions atomically through a checked RPC", () => {
  assert.match(migration, /create or replace function public\.finance_create_transaction/i);
  assert.match(migration, /item totals must equal the transaction total/i);
  assert.match(migration, /unique (?:constraint|index).*import_fingerprint/is);
  assert.match(migration, /security invoker/i);
});

test("finance migration seeds materially more expense categories than income categories", () => {
  const expenseCount = (migration.match(/'expense'/g) ?? []).length;
  const incomeCount = (migration.match(/'income'/g) ?? []).length;
  assert.ok(expenseCount >= 35, `expected >=35 expense markers, got ${expenseCount}`);
  assert.ok(incomeCount >= 8, `expected >=8 income markers, got ${incomeCount}`);
  assert.ok(expenseCount > incomeCount * 2, "expense catalog should be much larger");
});

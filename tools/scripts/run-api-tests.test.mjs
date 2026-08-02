import assert from "node:assert/strict";
import test from "node:test";

import {
  buildPostgresConnectionString,
  buildTestPostgresConnectionString,
  loadDotEnv,
} from "./run-api-tests-lib.mjs";

test("loads simple dotenv values without overriding syntax", () => {
  assert.deepEqual(
    loadDotEnv("# comment\nA=one\nB=two words\nEMPTY=\n"),
    { A: "one", B: "two words", EMPTY: "" },
  );
});

test("builds the API PostgreSQL connection string from shared environment", () => {
  assert.equal(
    buildPostgresConnectionString({
      ANDIVUM_POSTGRES_DB: "andivum_dev",
      ANDIVUM_POSTGRES_USER: "andivum_dev",
      ANDIVUM_POSTGRES_PASSWORD: "local-only",
    }),
    "Host=localhost;Port=5432;Database=andivum_dev;Username=andivum_dev;Password=local-only",
  );
});

test("builds a dedicated test connection and escapes unsafe values", () => {
  assert.equal(
    buildTestPostgresConnectionString({
      ANDIVUM_TEST_POSTGRES_DB: "andivum_test",
      ANDIVUM_TEST_POSTGRES_USER: "andivum_test",
      ANDIVUM_TEST_POSTGRES_PASSWORD: "unsafe;value",
      ANDIVUM_TEST_POSTGRES_PORT: "5433",
      ConnectionStrings__Postgres: "Host=production.example",
    }),
    'Host=localhost;Port=5433;Database=andivum_test;Username=andivum_test;Password="unsafe;value"',
  );
});

test("rejects a non-dedicated test database name", () => {
  assert.equal(
    buildTestPostgresConnectionString({
      ANDIVUM_TEST_POSTGRES_DB: "production",
      ANDIVUM_TEST_POSTGRES_USER: "prod",
    }),
    null,
  );
});

import { existsSync, readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { resolve } from "node:path";

import {
  buildTestPostgresConnectionString,
  loadDotEnv,
} from "./run-api-tests-lib.mjs";

const root = resolve(import.meta.dirname, "../..");
const envFile = resolve(root, ".env");
const fileEnvironment = existsSync(envFile)
  ? loadDotEnv(readFileSync(envFile, "utf8"))
  : {};
const environment = { ...fileEnvironment, ...process.env };
const connectionString = buildTestPostgresConnectionString(environment);

if (!connectionString) {
  console.error(
    "Dedicated test PostgreSQL is not configured. Set ANDIVUM_TEST_POSTGRES_DB/USER/PASSWORD (and optionally PORT).",
  );
  process.exitCode = 1;
} else {
  environment.ConnectionStrings__Postgres = connectionString;
}

if (connectionString) {
  const result = spawnSync(
    "dotnet",
    [
      "test",
      "services/api/Andivum.Api.Tests/Andivum.Api.Tests.csproj",
      ...process.argv.slice(2),
    ],
    {
      cwd: root,
      env: environment,
      stdio: "inherit",
      windowsHide: true,
    },
  );

  if (result.error) {
    console.error(result.error.message);
    process.exitCode = 1;
  } else {
    process.exitCode = result.status ?? 1;
  }
}

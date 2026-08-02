import { existsSync, readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { resolve } from "node:path";

import {
  buildPostgresConnectionString,
  loadDotEnv,
} from "./run-api-tests-lib.mjs";

const root = resolve(import.meta.dirname, "../..");
const envFile = resolve(root, ".env");
const fileEnvironment = existsSync(envFile)
  ? loadDotEnv(readFileSync(envFile, "utf8"))
  : {};
const environment = { ...fileEnvironment, ...process.env };

if (!environment.ConnectionStrings__Postgres) {
  const connectionString = buildPostgresConnectionString(environment);
  if (connectionString) {
    environment.ConnectionStrings__Postgres = connectionString;
  }
}

const result = spawnSync(
  "dotnet",
  [
    "run",
    "--project",
    "services/api/Andivum.Api/Andivum.Api.csproj",
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

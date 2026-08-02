import { existsSync, readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { resolve } from "node:path";

import { loadDotEnv } from "./run-api-tests-lib.mjs";

const root = resolve(import.meta.dirname, "../..");
const environment = {};

for (const fileName of [".env", ".env.andivum.local"]) {
  const filePath = resolve(root, fileName);

  if (existsSync(filePath)) {
    Object.assign(environment, loadDotEnv(readFileSync(filePath, "utf8")));
  }
}

Object.assign(environment, process.env);

const authLaunchArguments = [
  `--andivum-auth-provider=${environment.ANDIVUM_AUTH_PROVIDER ?? "supabase"}`,
  `--andivum-supabase-url=${environment.ANDIVUM_SUPABASE_URL ?? "http://localhost:54321"}`,
  `--andivum-supabase-publishable-key=${environment.ANDIVUM_SUPABASE_PUBLISHABLE_KEY ?? "local-publishable-key"}`,
].join(" ");

const result = spawnSync(
  "dotnet",
  [
    "run",
    "--project",
    "apps/windows/Andivum.Windows.csproj",
    "-p:Platform=x64",
    `-p:WinAppLaunchArgs=${authLaunchArguments}`,
    ...process.argv.slice(2),
  ],
  {
    cwd: root,
    env: environment,
    stdio: "inherit",
    windowsHide: false,
  },
);

if (result.error) {
  console.error(result.error.message);
  process.exitCode = 1;
} else {
  process.exitCode = result.status ?? 1;
}

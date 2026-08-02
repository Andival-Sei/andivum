import { existsSync, readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { join, resolve } from "node:path";
import { tmpdir } from "node:os";

import {
  buildPostgresConnectionString,
  loadDotEnv,
} from "./run-api-tests-lib.mjs";
import {
  buildLocalHttpsEnvironment,
  localHttpsCertificatePassword,
} from "./run-api-lib.mjs";

const root = resolve(import.meta.dirname, "../..");
const envFile = resolve(root, ".env");
const fileEnvironment = existsSync(envFile)
  ? loadDotEnv(readFileSync(envFile, "utf8"))
  : {};
let environment = { ...fileEnvironment, ...process.env };
environment.ASPNETCORE_ENVIRONMENT ??= "Development";
const hasExternalConnectionString = Boolean(
  process.env.ConnectionStrings__Postgres,
);

if (!hasExternalConnectionString) {
  const connectionString = buildPostgresConnectionString(environment);
  if (connectionString) {
    environment.ConnectionStrings__Postgres = connectionString;
    environment.Database__AutoMigrate = "true";
  }
}

const kestrelCertificatePath = "ASPNETCORE_Kestrel__Certificates__Default__Path";
const certificateDirectory = join(tmpdir(), "andivum-local-ca");
const certificatePath = join(certificateDirectory, "localhost-server.pfx");
const hasExplicitCertificate = Boolean(environment[kestrelCertificatePath]);
const windowsPowerShell = process.env.SystemRoot
  ? join(
    process.env.SystemRoot,
    "System32",
    "WindowsPowerShell",
    "v1.0",
    "powershell.exe",
  )
  : "powershell.exe";
const windowsPowerShellModulePath = [
  process.env.SystemRoot && join(
    process.env.SystemRoot,
    "System32",
    "WindowsPowerShell",
    "v1.0",
    "Modules",
  ),
  process.env.ProgramFiles && join(
    process.env.ProgramFiles,
    "WindowsPowerShell",
    "Modules",
  ),
].filter(Boolean).join(";");

if (process.platform === "win32" && !hasExplicitCertificate) {
  const certificateSetup = spawnSync(
    windowsPowerShell,
    [
      "-NoProfile",
      "-ExecutionPolicy",
      "Bypass",
      "-File",
      resolve(root, "tools/scripts/prepare-local-https-cert.ps1"),
      "-OutputDirectory",
      certificateDirectory,
    ],
    {
      cwd: root,
      env: {
        ...environment,
        ...(windowsPowerShellModulePath
          ? { PSModulePath: windowsPowerShellModulePath }
          : {}),
      },
      stdio: "inherit",
      windowsHide: true,
    },
  );

  if (certificateSetup.error || certificateSetup.status !== 0) {
    console.error(
      "Не удалось подготовить локальный HTTPS-сертификат для API.",
      certificateSetup.error?.message ?? "PowerShell завершился с ошибкой.",
    );
    process.exit(certificateSetup.status || 1);
  }

  environment = buildLocalHttpsEnvironment(environment, {
    certificatePath,
    password: environment.ANDIVUM_LOCAL_HTTPS_CERT_PASSWORD ||
      localHttpsCertificatePassword,
  });
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

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import test from "node:test";

import {
  assessCommand,
  assessPath,
  buildReport,
  formatHumanReport,
  versionFromUserAgent,
} from "./doctor-lib.mjs";

test("required failure makes the toolchain not ready and exits unsuccessfully", () => {
  const report = buildReport([
    { id: "git", required: true, status: "pass", message: "git 2.0" },
    { id: "jdk", required: true, status: "fail", message: "JDK 8" },
    { id: "appium", required: false, status: "warn", message: "not installed" },
  ]);

  assert.equal(report.ready, false);
  assert.equal(report.exitCode, 1);
  assert.deepEqual(report.summary, { pass: 1, warn: 1, fail: 1 });
});

test("optional warning keeps a complete required toolchain ready", () => {
  const report = buildReport([
    { id: "dotnet", required: true, status: "pass", message: ".NET 10" },
    { id: "appium", required: false, status: "warn", message: "later" },
  ]);

  assert.equal(report.ready, true);
  assert.equal(report.exitCode, 0);
  assert.deepEqual(report.summary, { pass: 1, warn: 1, fail: 0 });
});

test("human report exposes readiness and every actionable check", () => {
  const text = formatHumanReport(
    buildReport([
      {
        id: "android-sdk",
        required: true,
        status: "pass",
        message: "API 36.1",
      },
      {
        id: "docker",
        required: true,
        status: "fail",
        message: "daemon unavailable",
        action: "Start Docker Desktop",
      },
    ]),
  );

  assert.match(text, /NOT READY/);
  assert.match(text, /android-sdk.*PASS.*API 36\.1/);
  assert.match(text, /docker.*FAIL.*daemon unavailable/);
  assert.match(text, /Start Docker Desktop/);
});

test("command version below the minimum is a required failure", () => {
  assert.deepEqual(
    assessCommand({
      id: "node",
      required: true,
      available: true,
      versionText: "v22.18.0",
      minimumMajor: 24,
      action: "Install Node.js 24 LTS",
    }),
    {
      id: "node",
      required: true,
      status: "fail",
      message: "v22.18.0 (нужна версия 24+)",
      action: "Install Node.js 24 LTS",
    },
  );
});

test("missing optional command is a warning rather than a blocker", () => {
  assert.deepEqual(
    assessCommand({
      id: "appium",
      required: false,
      available: false,
      action: "Install with the Windows E2E slice",
    }),
    {
      id: "appium",
      required: false,
      status: "warn",
      message: "не найден",
      action: "Install with the Windows E2E slice",
    },
  );
});

test("found command with a failing invocation is not reported as passed", () => {
  assert.deepEqual(
    assessCommand({
      id: "docker",
      required: true,
      available: true,
      succeeded: false,
      failureMessage: "daemon unavailable",
      action: "Start Docker Desktop",
    }),
    {
      id: "docker",
      required: true,
      status: "fail",
      message: "daemon unavailable",
      action: "Start Docker Desktop",
    },
  );
});

test("existing required path is reported as passed", () => {
  assert.deepEqual(
    assessPath({
      id: "android-sdk",
      required: true,
      path: "C:/Android/Sdk",
      exists: true,
      detail: "platforms 34, 35, 36",
    }),
    {
      id: "android-sdk",
      required: true,
      status: "pass",
      message: "C:/Android/Sdk · platforms 34, 35, 36",
    },
  );
});

test("pnpm version is read from the package-manager user agent on Windows", () => {
  assert.equal(
    versionFromUserAgent(
      "pnpm/11.18.0 npm/? node/v26.5.1 win32 x64",
      "pnpm",
    ),
    "11.18.0",
  );
  assert.equal(versionFromUserAgent("npm/11.0.0 node/v26.5.1", "pnpm"), null);
});

test("silent pnpm JSON remains parseable when doctor exits unsuccessfully", () => {
  assert.ok(process.env.npm_execpath, "test must run through pnpm");

  const result = spawnSync(
    process.execPath,
    [process.env.npm_execpath, "--silent", "run", "doctor", "--json"],
    {
      cwd: process.cwd(),
      encoding: "utf8",
      timeout: 30_000,
      env: {
        ...process.env,
        ANDROID_SDK_ROOT: "Z:\\andivum-doctor-missing-sdk",
      },
    },
  );

  assert.equal(result.status, 1);
  const report = JSON.parse(result.stdout);
  assert.equal(report.ready, false);
  assert.ok(report.checks.some((check) => check.id === "android-sdk"));
});

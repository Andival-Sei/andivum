import { existsSync, readdirSync } from "node:fs";
import { homedir } from "node:os";
import { basename, join, resolve } from "node:path";
import { spawnSync } from "node:child_process";

import {
  assessCommand,
  assessPath,
  buildReport,
  formatHumanReport,
  versionFromUserAgent,
} from "./doctor-lib.mjs";

const isWindows = process.platform === "win32";
const jsonOutput = process.argv.includes("--json");
const checks = [];

function run(command, args = []) {
  const result = spawnSync(command, args, {
    encoding: "utf8",
    timeout: 15_000,
    windowsHide: true,
  });
  const output = `${result.stdout || ""}\n${result.stderr || ""}`.trim();

  return {
    available: !result.error,
    ok: !result.error && result.status === 0,
    output,
    error: result.error,
  };
}

function commandCheck({ id, command, args, minimumMajor, action }) {
  const result = run(command, args);
  const versionText = result.output.split(/\r?\n/, 1)[0];

  checks.push(
    assessCommand({
      id,
      required: true,
      available: result.available,
      succeeded: result.ok,
      failureMessage: result.error?.code === "ETIMEDOUT"
        ? "превышено время ожидания (15 секунд)"
        : result.output || result.error?.message,
      versionText,
      minimumMajor,
      action,
    }),
  );

  return result;
}

commandCheck({ id: "git", command: "git", args: ["--version"] });
commandCheck({
  id: "gh",
  command: "gh",
  args: ["--version"],
  action: "Установить GitHub CLI и выполнить gh auth login",
});
const pnpmVersion = versionFromUserAgent(
  process.env.npm_config_user_agent,
  "pnpm",
);
checks.push(
  assessCommand({
    id: "pnpm",
    required: true,
    available: Boolean(pnpmVersion),
    versionText: pnpmVersion,
    minimumMajor: 11,
    action: "Запускать doctor через pnpm run doctor или установить pnpm 11",
  }),
);
checks.push(
  assessCommand({
    id: "node",
    required: true,
    available: true,
    versionText: process.version,
    minimumMajor: 24,
    action: "Установить Node.js 24 LTS или новее",
  }),
);
commandCheck({
  id: "dotnet",
  command: "dotnet",
  args: ["--version"],
  minimumMajor: 10,
  action: "Установить .NET 10 SDK",
});

const ghAuth = run("gh", ["auth", "status"]);
checks.push({
  id: "github-auth",
  required: true,
  status: ghAuth.ok ? "pass" : "fail",
  message: ghAuth.ok ? "активная сессия найдена" : "нет активной сессии",
  ...(!ghAuth.ok ? { action: "Выполнить gh auth login" } : {}),
});

const localAppData = process.env.LOCALAPPDATA ||
  join(homedir(), "AppData", "Local");
const androidSdk = process.env.ANDROID_SDK_ROOT || process.env.ANDROID_HOME ||
  join(localAppData, "Android", "Sdk");
const androidPlatforms = existsSync(join(androidSdk, "platforms"))
  ? readdirSync(join(androidSdk, "platforms"))
    .filter((name) => name.startsWith("android-"))
    .sort()
    .join(", ")
  : undefined;

checks.push(
  assessPath({
    id: "android-sdk",
    required: true,
    path: androidSdk,
    exists: existsSync(androidSdk),
    detail: androidPlatforms ? `platforms: ${androidPlatforms}` : undefined,
    action: "Установить Android SDK command-line tools",
  }),
);

const executableSuffix = isWindows ? ".exe" : "";
const batchSuffix = isWindows ? ".bat" : "";
const adbPath = join(androidSdk, "platform-tools", `adb${executableSuffix}`);
const emulatorPath = join(
  androidSdk,
  "emulator",
  `emulator${executableSuffix}`,
);
const sdkManagerPath = join(
  androidSdk,
  "cmdline-tools",
  "latest",
  "bin",
  `sdkmanager${batchSuffix}`,
);

for (
  const [id, path] of [
    ["adb", adbPath],
    ["android-emulator", emulatorPath],
    ["sdkmanager", sdkManagerPath],
  ]
) {
  checks.push(
    assessPath({
      id,
      required: true,
      path,
      exists: existsSync(path),
      action: `Установить ${id} через Android SDK Manager`,
    }),
  );
}

const avdResult = existsSync(emulatorPath)
  ? run(emulatorPath, ["-list-avds"])
  : { ok: false, output: "" };
checks.push({
  id: "android-avd",
  required: true,
  status: avdResult.ok && avdResult.output ? "pass" : "fail",
  message: avdResult.ok && avdResult.output
    ? avdResult.output.split(/\r?\n/).join(", ")
    : "AVD не найден",
  ...(avdResult.ok && avdResult.output ? {} : {
    action: "Создать managed/emulator device для актуального Android API",
  }),
});

const androidStudioCandidates = [
  join(localAppData, "Programs", "Android Studio"),
  "C:\\Program Files\\Android\\Android Studio",
];
const androidStudioRoot = androidStudioCandidates.find((path) =>
  existsSync(join(path, "bin", "studio64.exe"))
);
checks.push(
  assessPath({
    id: "android-studio",
    required: false,
    path: androidStudioRoot || androidStudioCandidates[0],
    exists: Boolean(androidStudioRoot),
    action: "Установить stable Android Studio",
  }),
);

const jdkCandidates = [
  process.env.ANDIVUM_JAVA_HOME,
  process.env.JAVA_HOME,
  androidStudioRoot && join(androidStudioRoot, "jbr"),
].filter(Boolean);
const jdkRoot = jdkCandidates.find((path) =>
  existsSync(join(path, "bin", `java${executableSuffix}`))
);
const javaResult = jdkRoot
  ? run(join(jdkRoot, "bin", `java${executableSuffix}`), ["-version"])
  : { available: false, output: "" };
checks.push(
  assessCommand({
    id: "jdk",
    required: true,
    available: Boolean(jdkRoot) && javaResult.available,
    succeeded: Boolean(jdkRoot) && javaResult.ok,
    failureMessage: javaResult.error?.code === "ETIMEDOUT"
      ? "превышено время ожидания (15 секунд)"
      : javaResult.output || javaResult.error?.message,
    versionText: javaResult.output.split(/\r?\n/, 1)[0],
    minimumMajor: 17,
    action: "Использовать JBR Android Studio или установить JDK 17",
  }),
);

const winUiTemplate = run("dotnet", ["new", "list", "winui"]);
checks.push({
  id: "winui-template",
  required: true,
  status: winUiTemplate.ok && /winui/i.test(winUiTemplate.output)
    ? "pass"
    : "fail",
  message: winUiTemplate.ok && /winui/i.test(winUiTemplate.output)
    ? "dotnet new winui доступен"
    : "шаблон dotnet new winui не найден",
  ...(winUiTemplate.ok && /winui/i.test(winUiTemplate.output) ? {} : {
    action:
      "Установить официальный WinUI toolchain: winget configure -f https://aka.ms/winui-config",
  }),
});

const developerMode = isWindows
  ? run("reg", [
    "query",
    "HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AppModelUnlock",
    "/v",
    "AllowDevelopmentWithoutDevLicense",
  ])
  : { ok: false, output: "" };
checks.push({
  id: "developer-mode",
  required: isWindows,
  status: !isWindows
    ? "warn"
    : developerMode.ok && /0x1\b/i.test(developerMode.output)
    ? "pass"
    : "fail",
  message: !isWindows
    ? "проверяется только на Windows"
    : developerMode.ok && /0x1\b/i.test(developerMode.output)
    ? "включён"
    : "выключен или не определён",
  ...(isWindows && !(developerMode.ok && /0x1\b/i.test(developerMode.output))
    ? { action: "Включить Windows Developer Mode" }
    : {}),
});

const docker = run("docker", ["info", "--format", "{{.ServerVersion}}"]);
checks.push({
  id: "docker",
  required: true,
  status: docker.ok ? "pass" : "fail",
  message: docker.ok ? `daemon ${docker.output}` : "Docker daemon недоступен",
  ...(!docker.ok ? { action: "Запустить Docker Desktop" } : {}),
});

const appium = run(isWindows ? "appium.cmd" : "appium", ["--version"]);
checks.push(
  assessCommand({
    id: "appium",
    required: false,
    available: appium.available,
    succeeded: appium.ok,
    failureMessage: appium.error?.code === "ETIMEDOUT"
      ? "превышено время ожидания (15 секунд)"
      : appium.output || appium.error?.message,
    versionText: appium.output.split(/\r?\n/, 1)[0],
    action: "Добавить project-local Appium вместе с Windows E2E slice",
  }),
);

const report = buildReport(checks);

if (jsonOutput) {
  console.log(JSON.stringify(report, null, 2));
} else {
  console.log(formatHumanReport(report));
}

process.exitCode = report.exitCode;

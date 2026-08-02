const statuses = ["pass", "warn", "fail"];

export function versionFromUserAgent(userAgent, packageManager) {
  const escapedName = packageManager.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return userAgent?.match(new RegExp(`(?:^|\\s)${escapedName}/([^\\s]+)`))
    ?.[1] ??
    null;
}

export function assessCommand({
  id,
  required,
  available,
  succeeded = available,
  failureMessage,
  versionText,
  minimumMajor,
  action,
}) {
  if (!available) {
    return {
      id,
      required,
      status: required ? "fail" : "warn",
      message: "не найден",
      ...(action ? { action } : {}),
    };
  }

  if (!succeeded) {
    return {
      id,
      required,
      status: required ? "fail" : "warn",
      message: failureMessage || "команда не выполнилась",
      ...(action ? { action } : {}),
    };
  }

  const major = Number(versionText?.match(/\d+/)?.[0]);

  if (minimumMajor && (!Number.isFinite(major) || major < minimumMajor)) {
    return {
      id,
      required,
      status: "fail",
      message: `${
        versionText || "неизвестная версия"
      } (нужна версия ${minimumMajor}+)`,
      ...(action ? { action } : {}),
    };
  }

  return {
    id,
    required,
    status: "pass",
    message: versionText || "найден",
  };
}

export function assessPath({ id, required, path, exists, detail, action }) {
  if (!exists) {
    return {
      id,
      required,
      status: required ? "fail" : "warn",
      message: path ? `не найден: ${path}` : "путь не задан",
      ...(action ? { action } : {}),
    };
  }

  return {
    id,
    required,
    status: "pass",
    message: `${path}${detail ? ` · ${detail}` : ""}`,
  };
}

export function buildReport(checks) {
  const summary = { pass: 0, warn: 0, fail: 0 };

  for (const check of checks) {
    if (!statuses.includes(check.status)) {
      throw new TypeError(`Неизвестный doctor status: ${check.status}`);
    }

    summary[check.status] += 1;
  }

  const ready = !checks.some(
    (check) => check.required && check.status === "fail",
  );

  return {
    ready,
    exitCode: ready ? 0 : 1,
    summary,
    checks,
  };
}

export function formatHumanReport(report) {
  const lines = [
    `Andivum toolchain: ${report.ready ? "READY" : "NOT READY"}`,
    "",
  ];

  for (const check of report.checks) {
    lines.push(
      `${check.id.padEnd(20)} ${
        check.status.toUpperCase().padEnd(4)
      } ${check.message}`,
    );

    if (check.action) {
      lines.push(`${"".padEnd(25)}→ ${check.action}`);
    }
  }

  lines.push(
    "",
    `PASS ${report.summary.pass} · WARN ${report.summary.warn} · FAIL ${report.summary.fail}`,
  );

  return lines.join("\n");
}

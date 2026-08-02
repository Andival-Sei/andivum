export function loadDotEnv(contents) {
  const values = {};

  for (const rawLine of contents.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) {
      continue;
    }

    const separator = line.indexOf("=");
    if (separator < 1) {
      continue;
    }

    const key = line.slice(0, separator).trim();
    const value = line.slice(separator + 1).trim();
    values[key] = value.replace(/^(['"])(.*)\1$/, "$2");
  }

  return values;
}

function escapeConnectionValue(value) {
  const stringValue = String(value);

  if (!/[;"'\s]/.test(stringValue)) {
    return stringValue;
  }

  return `"${stringValue.replaceAll('"', '""')}"`;
}

function buildConnectionString({ database, username, password, port }) {
  if (!database || !username) {
    return null;
  }

  const parts = [
    "Host=localhost",
    `Port=${escapeConnectionValue(port ?? 5432)}`,
    `Database=${escapeConnectionValue(database)}`,
    `Username=${escapeConnectionValue(username)}`,
  ];

  if (password) {
    parts.push(`Password=${escapeConnectionValue(password)}`);
  }

  return parts.join(";");
}

export function buildPostgresConnectionString(environment) {
  return buildConnectionString({
    database: environment.ANDIVUM_POSTGRES_DB,
    username: environment.ANDIVUM_POSTGRES_USER,
    password: environment.ANDIVUM_POSTGRES_PASSWORD,
    port: 5432,
  });
}

export function buildTestPostgresConnectionString(environment) {
  const database = environment.ANDIVUM_TEST_POSTGRES_DB;

  if (!database || !/^andivum_(?:test|ci)(?:_|$)/.test(database)) {
    return null;
  }

  return buildConnectionString({
    database,
    username: environment.ANDIVUM_TEST_POSTGRES_USER,
    password: environment.ANDIVUM_TEST_POSTGRES_PASSWORD,
    port: environment.ANDIVUM_TEST_POSTGRES_PORT ?? 5433,
  });
}

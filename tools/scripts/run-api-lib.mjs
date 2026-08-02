import { tmpdir } from "node:os";
import { join } from "node:path";

export const localHttpsCertificatePassword = "andivum-local-only";

export function buildLocalHttpsEnvironment(environment, options = {}) {
  const platform = options.platform ?? process.platform;

  if (platform !== "win32") {
    return { ...environment };
  }

  const certificatePath = options.certificatePath ?? join(
    tmpdir(),
    "andivum-local-ca",
    "localhost-server.pfx",
  );
  const password = options.password ?? localHttpsCertificatePassword;
  const pathKey = "ASPNETCORE_Kestrel__Certificates__Default__Path";
  const passwordKey = "ASPNETCORE_Kestrel__Certificates__Default__Password";

  return {
    ...environment,
    ...(environment[pathKey] ? {} : { [pathKey]: certificatePath }),
    ...(environment[passwordKey] ? {} : { [passwordKey]: password }),
  };
}

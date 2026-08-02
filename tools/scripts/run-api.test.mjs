import assert from "node:assert/strict";
import test from "node:test";

import { buildLocalHttpsEnvironment } from "./run-api-lib.mjs";

test("adds the shared local HTTPS certificate settings on Windows", () => {
  assert.deepEqual(
    buildLocalHttpsEnvironment(
      { ASPNETCORE_ENVIRONMENT: "Development" },
      {
        platform: "win32",
        certificatePath: "C:\\Temp\\andivum-local-ca\\localhost-server.pfx",
        password: "local-only",
      },
    ),
    {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_Kestrel__Certificates__Default__Path:
        "C:\\Temp\\andivum-local-ca\\localhost-server.pfx",
      ASPNETCORE_Kestrel__Certificates__Default__Password: "local-only",
    },
  );
});

test("does not add Windows certificate settings on other platforms", () => {
  const environment = { ASPNETCORE_ENVIRONMENT: "Development" };

  assert.deepEqual(
    buildLocalHttpsEnvironment(environment, {
      platform: "linux",
      certificatePath: "/tmp/localhost-server.pfx",
      password: "local-only",
    }),
    environment,
  );
});

test("keeps an explicit certificate configuration", () => {
  assert.deepEqual(
    buildLocalHttpsEnvironment(
      {
        ASPNETCORE_Kestrel__Certificates__Default__Path: "C:\\custom\\api.pfx",
        ASPNETCORE_Kestrel__Certificates__Default__Password: "custom",
      },
      {
        platform: "win32",
        certificatePath: "C:\\Temp\\andivum-local-ca\\localhost-server.pfx",
        password: "local-only",
      },
    ),
    {
      ASPNETCORE_Kestrel__Certificates__Default__Path: "C:\\custom\\api.pfx",
      ASPNETCORE_Kestrel__Certificates__Default__Password: "custom",
    },
  );
});

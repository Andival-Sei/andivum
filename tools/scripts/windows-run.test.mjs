import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import test from "node:test";

const root = resolve(import.meta.dirname, "../..");

test("Windows run command uses package-aware dotnet run and cloud env file", async () => {
  const [packageJson, script] = await Promise.all([
    readFile(resolve(root, "package.json"), "utf8"),
    readFile(resolve(root, "tools/scripts/run-windows.mjs"), "utf8"),
  ]);

  assert.match(packageJson, /"windows:run":\s*"node tools\/scripts\/run-windows\.mjs"/);
  assert.match(script, /loadDotEnv/);
  assert.match(script, /\.env\.andivum\.local/);
  assert.match(script, /spawnSync\(\s*["']dotnet["']/);
  assert.match(script, /["']run["']/);
  assert.match(script, /apps\/windows\/Andivum\.Windows\.csproj/);
  assert.match(script, /-p:Platform=x64/);
});

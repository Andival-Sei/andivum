import { access, readdir, readFile } from "node:fs/promises";
import { dirname, extname, resolve } from "node:path";

import { validateProductConfiguration } from "./product-config-lib.mjs";

const root = resolve(import.meta.dirname, "../..");
const requiredFiles = [
  "README.md",
  "AGENTS.md",
  "PROJECT_CONTEXT.md",
  "docs/DEVELOPMENT_PROCESS.md",
  "docs/TOOLCHAIN.md",
  "config/product.json",
  "config/product.schema.json",
  "docs/PRODUCT.md",
  "docs/ARCHITECTURE.md",
  "docs/DECISIONS.md",
  "docs/ROADMAP.md",
  "docs/templates/FEATURE_SPEC.md",
  "docs/templates/IMPLEMENTATION_PLAN.md",
  "docs/templates/ADR.md",
];

const errors = [];

async function collectMarkdownFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    if ([".git", "node_modules"].includes(entry.name)) {
      continue;
    }

    const path = resolve(directory, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await collectMarkdownFiles(path)));
    } else if (extname(entry.name) === ".md") {
      files.push(path);
    }
  }

  return files;
}

for (const file of requiredFiles) {
  try {
    await access(resolve(root, file));
  } catch {
    errors.push(`Отсутствует обязательный файл: ${file}`);
  }
}

const productPath = resolve(root, "config/product.json");
let product;

try {
  product = JSON.parse(await readFile(productPath, "utf8"));
} catch (error) {
  errors.push(`Не удалось прочитать config/product.json: ${error.message}`);
}

try {
  JSON.parse(
    await readFile(resolve(root, "config/product.schema.json"), "utf8"),
  );
} catch (error) {
  errors.push(`Не удалось прочитать product.schema.json: ${error.message}`);
}

if (product) {
  errors.push(...validateProductConfiguration(product));
}

for (const markdownPath of await collectMarkdownFiles(root)) {
  const markdown = await readFile(markdownPath, "utf8");
  const links = markdown.matchAll(/\[[^\]]+\]\(([^)]+)\)/g);

  for (const [, target] of links) {
    if (/^(?:https?:|mailto:|#)/.test(target)) {
      continue;
    }

    const localTarget = decodeURIComponent(target.split("#", 1)[0]);

    try {
      await access(resolve(dirname(markdownPath), localTarget));
    } catch {
      errors.push(
        `Неразрешимая локальная ссылка ${target} в ${
          markdownPath.slice(root.length + 1)
        }`,
      );
    }
  }
}

if (errors.length > 0) {
  console.error(errors.map((error) => `- ${error}`).join("\n"));
  process.exitCode = 1;
} else {
  console.log("Foundation-документация и product configuration согласованы.");
}

#!/usr/bin/env node

import { readFileSync } from "node:fs";

const [, , openApiPath, manifestPath, collectionPath] = process.argv;

if (!openApiPath || !manifestPath || !collectionPath) {
  console.error(
    "Usage: node tools/check-postman-coverage.mjs <openapi.json> <coverage-manifest.json> <collection.json>",
  );
  process.exit(2);
}

function readJson(path) {
  try {
    return JSON.parse(readFileSync(path, "utf8"));
  } catch (error) {
    console.error(`Cannot read JSON '${path}': ${error.message}`);
    process.exit(2);
  }
}

function duplicates(values) {
  const seen = new Set();
  const duplicateValues = new Set();

  for (const value of values) {
    if (seen.has(value)) {
      duplicateValues.add(value);
    }

    seen.add(value);
  }

  return [...duplicateValues].sort();
}

function collectOpenApiOperations(document) {
  const httpMethods = new Set([
    "get",
    "post",
    "put",
    "patch",
    "delete",
    "head",
    "options",
    "trace",
  ]);
  const operations = [];
  const missingOperationIds = [];

  for (const [route, pathItem] of Object.entries(document.paths ?? {})) {
    for (const [method, operation] of Object.entries(pathItem ?? {})) {
      if (!httpMethods.has(method.toLowerCase())) {
        continue;
      }

      const operationId = operation?.operationId?.trim();
      if (!operationId) {
        missingOperationIds.push(`${method.toUpperCase()} ${route}`);
        continue;
      }

      operations.push(operationId);
    }
  }

  return { operations, missingOperationIds };
}

function collectManifestOperations(manifest) {
  return (manifest.operations ?? []).map((entry) =>
    typeof entry === "string" ? entry.trim() : entry?.operationId?.trim(),
  );
}

function descriptionText(description) {
  if (typeof description === "string") {
    return description;
  }

  return description?.content ?? "";
}

function collectPostmanOperations(items, result = []) {
  for (const item of items ?? []) {
    if (Array.isArray(item.item)) {
      collectPostmanOperations(item.item, result);
    }

    if (!item.request) {
      continue;
    }

    const description = [
      descriptionText(item.description),
      descriptionText(item.request.description),
    ].join("\n");

    const matches = description.matchAll(
      /^\s*operationId\s*:\s*([A-Za-z0-9_.-]+)\s*$/gim,
    );

    for (const match of matches) {
      result.push(match[1]);
    }
  }

  return result;
}

function difference(left, right) {
  const rightSet = new Set(right);
  return [...new Set(left.filter((value) => !rightSet.has(value)))].sort();
}

function reportList(title, values) {
  if (values.length === 0) {
    return;
  }

  console.error(`${title}:`);
  for (const value of values) {
    console.error(`  - ${value}`);
  }
}

const openApi = readJson(openApiPath);
const manifest = readJson(manifestPath);
const collection = readJson(collectionPath);

const { operations: openApiOperations, missingOperationIds } =
  collectOpenApiOperations(openApi);
const manifestOperations = collectManifestOperations(manifest).filter(Boolean);
const postmanOperations = collectPostmanOperations(collection.item);

const failures = {
  "OpenAPI endpoints without operationId": missingOperationIds,
  "Duplicate OpenAPI operationIds": duplicates(openApiOperations),
  "Duplicate manifest operationIds": duplicates(manifestOperations),
  "Duplicate primary Postman operation markers": duplicates(postmanOperations),
  "OpenAPI operations missing from manifest": difference(
    openApiOperations,
    manifestOperations,
  ),
  "OpenAPI operations missing from Postman": difference(
    openApiOperations,
    postmanOperations,
  ),
  "Stale manifest operations": difference(manifestOperations, openApiOperations),
  "Stale Postman operation markers": difference(
    postmanOperations,
    openApiOperations,
  ),
};

let failed = false;
for (const [title, values] of Object.entries(failures)) {
  if (values.length > 0) {
    failed = true;
    reportList(title, values);
  }
}

if (failed) {
  process.exit(1);
}

console.log(
  `Postman coverage is complete: ${openApiOperations.length} OpenAPI operations.`,
);


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
  const routes = [];

  for (const [route, pathItem] of Object.entries(document.paths ?? {})) {
    for (const [method, operation] of Object.entries(pathItem ?? {})) {
      if (!httpMethods.has(method.toLowerCase())) {
        continue;
      }

      const operationId = operation?.operationId?.trim();
      routes.push(normalizeRoute(method, route));
      if (operationId) {
        operations.push(operationId);
      }
    }
  }

  return { operations, routes };
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

function collectPostmanCoverage(items, operations = [], routes = []) {
  for (const item of items ?? []) {
    if (Array.isArray(item.item)) {
      collectPostmanCoverage(item.item, operations, routes);
    }

    if (!item.request) {
      continue;
    }

    const method = item.request.method ?? "GET";
    const rawUrl = typeof item.request.url === "string"
      ? item.request.url
      : item.request.url?.raw;
    if (rawUrl) {
      routes.push(normalizeRoute(method, rawUrl));
    }

    const description = [
      descriptionText(item.description),
      descriptionText(item.request.description),
    ].join("\n");

    const matches = description.matchAll(
      /^\s*operationId\s*:\s*([A-Za-z0-9_.-]+)\s*$/gim,
    );

    for (const match of matches) {
      operations.push(match[1]);
    }
  }

  return { operations, routes };
}

function normalizeRoute(method, rawRoute) {
  const route = rawRoute
    .replace(/^\{\{baseUrl\}\}/i, "")
    .replace(/\{\{([^}]+)\}\}/g, "{$1}")
    .replace(/\{\*{0,2}([^}:?]+)(?::[^}?]+)?\??\}/g, "{$1}")
    .replace(/\?.*$/, "")
    .replace(/^\/*/, "/")
    .replace(/\/+$/, "") || "/";
  return `${method.toUpperCase()} ${route.toLowerCase()}`;
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

const { operations: openApiOperations, routes: openApiRoutes } =
  collectOpenApiOperations(openApi);
const manifestOperations = collectManifestOperations(manifest).filter(Boolean);
const { operations: postmanOperations, routes: postmanRoutes } =
  collectPostmanCoverage(collection.item);

const failures = {
  "Duplicate OpenAPI operationIds": duplicates(openApiOperations),
  "Duplicate manifest operationIds": duplicates(manifestOperations),
  "Duplicate primary Postman operation markers": duplicates(postmanOperations),
  "Duplicate Postman routes": duplicates(postmanRoutes),
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
  "OpenAPI routes missing from Postman": difference(openApiRoutes, postmanRoutes),
  "Stale Postman routes": difference(postmanRoutes, openApiRoutes),
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
  `Postman coverage is complete: ${openApiRoutes.length} OpenAPI routes and ${openApiOperations.length} named operations.`,
);

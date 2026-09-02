#!/usr/bin/env node

import { readFileSync, readdirSync } from "node:fs";
import { basename, join } from "node:path";

const [, , legacyRoot, openApiPath, outputMode] = process.argv;
if (!legacyRoot || !openApiPath) {
  console.error(
    "Usage: node tools/audit-legacy-route-parity.mjs <legacy-root> <openapi.json>",
  );
  process.exit(2);
}

const controllersRoot = join(legacyRoot, "Controllers");
const openApi = JSON.parse(readFileSync(openApiPath, "utf8"));
const httpMethods = new Set([
  "GET",
  "POST",
  "PUT",
  "PATCH",
  "DELETE",
  "HEAD",
  "OPTIONS",
]);
const currentOperations = new Set();
for (const [route, pathItem] of Object.entries(openApi.paths ?? {})) {
  for (const method of Object.keys(pathItem ?? {})) {
    const normalizedMethod = method.toUpperCase();
    if (httpMethods.has(normalizedMethod)) {
      currentOperations.add(operationKey(normalizedMethod, route));
    }
  }
}

const ignoredControllers = new Set(["CamexWebhook", "SandoogWebhook"]);
const actionPattern =
  /((?:\s*\[[^\]]+\]\s*)*)public\s+(?:async\s+)?(?:Task\s*<[^;{]+?>|IActionResult|JsonResult|ActionResult(?:\s*<[^;{]+?>)?)\s+(\w+)\s*\(/g;
const candidates = [];

for (const fileName of readdirSync(controllersRoot).sort()) {
  if (!fileName.endsWith("Controller.cs")) continue;

  const controllerName = basename(fileName, "Controller.cs");
  if (ignoredControllers.has(controllerName)) continue;

  const source = readFileSync(join(controllersRoot, fileName), "utf8");
  const classIndex = source.search(new RegExp(`class\\s+${controllerName}Controller\\b`));
  const classAttributes = classIndex < 0
    ? ""
    : trailingAttributes(source.slice(0, classIndex));
  const classRoutes = routeTemplates(classAttributes);

  for (const match of source.matchAll(actionPattern)) {
    const actionAttributes = match[1] ?? "";
    const actionName = match[2];
    const declaredMethods = methodTemplates(actionAttributes);
    const actionRoutes = routeTemplates(actionAttributes);

    if (declaredMethods.length > 0) {
      for (const declaredMethod of declaredMethods) {
        const templates = declaredMethod.template === null
          ? actionRoutes.length > 0
            ? actionRoutes
            : [null]
          : [declaredMethod.template];
        for (const template of templates) {
          for (const route of expandRoutes(
            controllerName,
            actionName,
            classRoutes,
            template,
          )) {
            candidates.push(candidate(
              controllerName,
              actionName,
              declaredMethod.method,
              route,
            ));
          }
        }
      }
      continue;
    }

    if (actionRoutes.length > 0) {
      for (const template of actionRoutes) {
        for (const route of expandRoutes(
          controllerName,
          actionName,
          classRoutes,
          template,
        )) {
          for (const method of ["GET", "POST"]) {
            candidates.push(candidate(controllerName, actionName, method, route));
          }
        }
      }
      continue;
    }

    const conventionalRoute = `/${controllerName}/${actionName}`;
    for (const method of ["GET", "POST"]) {
      candidates.push(candidate(
        controllerName,
        actionName,
        method,
        conventionalRoute,
      ));
    }
  }
}

const uniqueCandidates = [
  ...new Map(candidates.map((item) => [item.key, item])).values(),
];
const covered = uniqueCandidates.filter((item) =>
  currentOperations.has(item.key));
const missing = uniqueCandidates.filter((item) =>
  !currentOperations.has(item.key));

console.log(`Legacy operation candidates: ${uniqueCandidates.length}`);
console.log(`Exact current operation matches: ${covered.length}`);
console.log(`Missing operation candidates: ${missing.length}`);
console.log("CAMEX/Sandoog controllers: excluded");
if (outputMode === "--summary") {
  const grouped = new Map();
  for (const item of missing) {
    const items = grouped.get(item.controllerName) ?? [];
    items.push(item);
    grouped.set(item.controllerName, items);
  }
  const missingByController = [...grouped]
    .map(([controllerName, items]) => ({ controllerName, count: items.length }))
    .sort((left, right) =>
      right.count - left.count || left.controllerName.localeCompare(right.controllerName));
  for (const item of missingByController) {
    console.log(`${item.controllerName}: ${item.count}`);
  }
} else {
  for (const item of missing) {
    console.log(`${item.method} ${item.route} (${item.controllerName}.${item.actionName})`);
  }
}

process.exitCode = missing.length === 0 ? 0 : 1;

function candidate(controllerName, actionName, method, route) {
  return {
    controllerName,
    actionName,
    method,
    route,
    key: operationKey(method, route),
  };
}

function expandRoutes(
  controllerName,
  actionName,
  classRoutes,
  actionTemplate,
) {
  if (actionTemplate?.startsWith("/") || actionTemplate?.startsWith("~/")) {
    return [replaceTokens(actionTemplate, controllerName, actionName)];
  }

  if (classRoutes.length === 0) {
    if (actionTemplate === null || actionTemplate === "") {
      return [`/${controllerName}/${actionName}`];
    }
    return [replaceTokens(`/${actionTemplate}`, controllerName, actionName)];
  }

  return classRoutes.map((classRoute) => {
    const prefix = replaceTokens(classRoute, controllerName, actionName)
      .replace(/\/+$/, "");
    const suffix = actionTemplate === null
      ? ""
      : replaceTokens(actionTemplate, controllerName, actionName)
        .replace(/^\/+/, "");
    return `/${prefix.replace(/^\/+/, "")}${suffix ? `/${suffix}` : ""}`;
  });
}

function replaceTokens(template, controllerName, actionName) {
  return template
    .replace(/^~\//, "/")
    .replace(/\[controller\]/gi, controllerName)
    .replace(/\[action\]/gi, actionName);
}

function methodTemplates(attributes) {
  const result = [];
  const pattern =
    /\[Http(Get|Post|Put|Patch|Delete|Head|Options)(?:\s*\(\s*"([^"]*)"[^)]*\))?[^\]]*\]/gi;
  for (const match of attributes.matchAll(pattern)) {
    result.push({
      method: match[1].toUpperCase(),
      template: match[2] ?? null,
    });
  }
  return result;
}

function routeTemplates(attributes) {
  return [...attributes.matchAll(/\[Route\s*\(\s*"([^"]+)"[^)]*\)[^\]]*\]/gi)]
    .map((match) => match[1]);
}

function trailingAttributes(prefix) {
  const match = prefix.match(/((?:\s*\[[^\]]+\]\s*)+)$/);
  return match?.[1] ?? "";
}

function operationKey(method, route) {
  return `${method.toUpperCase()} ${normalizeRoute(route)}`;
}

function normalizeRoute(route) {
  return route
    .replace(/\{\*{0,2}([^}:?]+)(?::[^}?]+)?\??\}/g, "{$1}")
    .replace(/\/+$/, "")
    .toLowerCase() || "/";
}

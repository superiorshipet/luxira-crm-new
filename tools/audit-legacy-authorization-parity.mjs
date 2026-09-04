#!/usr/bin/env node

import { readFileSync, readdirSync, statSync } from "node:fs";
import { basename, join } from "node:path";

const [, , legacyRoot, currentRoot = "."] = process.argv;
if (!legacyRoot) {
  console.error("Usage: node tools/audit-legacy-authorization-parity.mjs <legacy-root> [current-root]");
  process.exit(2);
}

const legacy = scan(join(legacyRoot, "Controllers"), false);
const current = scan(join(currentRoot, "Features"), true);
const differences = [];

for (const [key, oldRoles] of legacy.actions) {
  const newRoles = current.actions.get(key);
  if (!newRoles) continue;
  if (signature(oldRoles) !== signature(newRoles)) differences.push({ key, oldRoles, newRoles });
}

console.log(`Comparable authorized actions: ${[...legacy.actions.keys()].filter(key => current.actions.has(key)).length}`);
console.log(`Authorization differences: ${differences.length}`);
for (const item of differences)
  console.log(`${item.key}: old=[${display(item.oldRoles)}] new=[${display(item.newRoles)}]`);

process.exitCode = differences.length === 0 ? 0 : 1;

function scan(root, recursive) {
  const files = walk(root, recursive).filter(file => file.endsWith("Controller.cs") || file.includes("Controller."));
  const classRoles = new Map();
  const declarations = new Map();
  for (const file of files) {
    const source = withoutLineComments(readFileSync(file, "utf8"));
    const found = [...source.matchAll(/public\s+(?:sealed\s+)?(?:partial\s+)?class\s+(\w+)Controller\b/g)];
    declarations.set(file, found);
    for (const match of found) {
      const prefix = source.slice(Math.max(0, match.index - 2000), match.index);
      const attributes = prefix.match(/((?:[ \t]*\[[^\r\n]*\][ \t]*\r?\n)+)[ \t]*$/)?.[1] ?? "";
      const roles = parseRoles(attributes, source);
      if (roles !== null) classRoles.set(match[1], roles);
    }
  }

  const actions = new Map();
  const actionPattern = /((?:\s*\[[^\]]+\]\s*(?:\/\/[^\r\n]*)?\s*)*)public\s+(?:async\s+)?(?:Task\s*<[^;{]+?>|IActionResult|JsonResult|ActionResult(?:\s*<[^;{]+?>)?)\s+(\w+)\s*\(/g;
  for (const file of files) {
    const source = withoutLineComments(readFileSync(file, "utf8"));
    for (const match of source.matchAll(actionPattern)) {
      const declared = declarations.get(file) ?? [];
      const controller = [...declared].reverse().find(item => item.index < match.index)?.[1] ?? controllerName(file, source);
      if (!controller) continue;
      const actionRoles = parseRoles(match[1], source);
      const effective = actionRoles ?? classRoles.get(controller) ?? null;
      if (effective !== null) actions.set(`${controller}.${match[2]}`, effective);
    }
  }
  return { actions };
}

function walk(root, recursive) {
  const result = [];
  for (const name of readdirSync(root)) {
    const path = join(root, name);
    if (statSync(path).isDirectory()) {
      if (recursive) result.push(...walk(path, true));
    } else result.push(path);
  }
  return result;
}

function controllerName(file, source) {
  const declared = source.match(/public\s+(?:sealed\s+)?(?:partial\s+)?class\s+(\w+)Controller\b/)?.[1];
  if (declared) return declared;
  return basename(file).match(/^(\w+)Controller(?:\.|\.cs)/)?.[1] ?? null;
}

function parseRoles(attributes, source) {
  const authorize = [...attributes.matchAll(/\[Authorize(?:\s*\(([^\]]*)\))?[^\]]*\]/g)];
  if (authorize.length === 0) return null;
  const roles = new Set();
  let unrestricted = false;
  for (const item of authorize) {
    const argument = item[1]?.match(/Roles\s*=\s*(?:"([^"]*)"|(\w+))/);
    const constant = argument?.[2] === undefined
      ? undefined
      : source.match(new RegExp(`const\\s+string\\s+${argument[2]}\\s*=\\s*"([^"]*)"`))?.[1];
    const value = argument?.[1] ?? constant;
    if (value === undefined) unrestricted = true;
    else for (const role of value.split(",")) roles.add(normalize(role));
  }
  return unrestricted && roles.size === 0 ? new Set(["*"]) : roles;
}

function normalize(role) {
  const value = role.trim();
  return value === "Administrator" ? "Admin" : value;
}

function signature(roles) { return [...roles].sort().join(","); }
function display(roles) { return [...roles].sort().join(","); }
function withoutLineComments(source) { return source.replace(/\/\/[^\r\n]*/g, ""); }

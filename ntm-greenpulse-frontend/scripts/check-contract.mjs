import { readFile } from "node:fs/promises";
import assert from "node:assert/strict";
import ts from "typescript";
import { parse } from "yaml";
const contract = parse(
  await readFile(
    new URL(
      "../../contracts/ikhethelo-digital-api.openapi.yaml",
      import.meta.url,
    ),
    "utf8",
  ),
);
const source = ts.createSourceFile(
  "models.ts",
  await readFile(new URL("../src/app/core/models.ts", import.meta.url), "utf8"),
  ts.ScriptTarget.Latest,
  true,
);
for (const [model, schema] of Object.entries({
  VeteranWrite: "NtmVeteranWrite",
  Veteran: "NtmVeteran",
  BusinessWrite: "NtmBusinessWrite",
  Business: "NtmBusiness",
  Dashboard: "NtmDashboard",
  Administrator: "Administrator",
})) {
  const declaration = source.statements.find(
    (node) => ts.isInterfaceDeclaration(node) && node.name.text === model,
  );
  assert.ok(declaration, model);
  const fields = declaration.members
    .filter(ts.isPropertySignature)
    .map((node) => node.name.getText(source))
    .sort();
  assert.deepEqual(
    fields,
    Object.keys(contract.components.schemas[schema].properties).sort(),
    schema,
  );
}
const api = ts.createSourceFile(
  "api.ts",
  await readFile(new URL("../src/app/core/api.ts", import.meta.url), "utf8"),
  ts.ScriptTarget.Latest,
  true,
);
for (const [name, schema] of [
  ["VETERAN_FIELDS", "NtmVeteranWrite"],
  ["BUSINESS_FIELDS", "NtmBusinessWrite"],
]) {
  const declaration = api.statements
    .filter(ts.isVariableStatement)
    .flatMap((s) => s.declarationList.declarations)
    .find((d) => d.name.getText(api) === name);
  let node = declaration.initializer;
  while (ts.isAsExpression(node) || ts.isSatisfiesExpression(node))
    node = node.expression;
  assert.deepEqual(
    node.elements.map((x) => x.text).sort(),
    Object.keys(contract.components.schemas[schema].properties).sort(),
    name,
  );
}
const ntmPaths = Object.entries(contract.paths).filter(([key]) =>
  key.includes("/ntm/"),
);
assert.equal(
  ntmPaths.reduce(
    (n, [, value]) =>
      n +
      Object.keys(value).filter((x) => ["get", "post", "put"].includes(x))
        .length,
    0,
  ),
  9,
);
console.log(
  `OpenAPI ${contract.info.version}: NTM/admin DTOs, full write whitelists and nine NTM operations verified.`,
);

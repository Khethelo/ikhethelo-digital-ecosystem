import { readFile } from 'node:fs/promises';
import { parse } from 'yaml';
import assert from 'node:assert/strict';
const contract = parse(await readFile(new URL('../../contracts/ikhethelo-digital-api.openapi.yaml', import.meta.url), 'utf8'));
const schemas = contract.components.schemas;
for (const [schema, fields] of Object.entries({ ApplicationSubmission: ['firstName','lastName','dateOfBirth','phoneNumber','email','termsAccepted','privacyConsent'], MemberUpdate: ['firstName','lastName','phoneNumber','email'], MemberStatusUpdate: ['status'], ReviewRequest: ['reviewNotes'] })) {
  assert.deepEqual(Object.keys(schemas[schema].properties).sort(), fields.sort(), `${schema} contract changed`);
  assert.equal(schemas[schema].additionalProperties, false);
}
assert.deepEqual(schemas.TokenResponse.properties.expiresIn.enum, [1800]);
assert.deepEqual(schemas.ApplicationStatus.enum.slice(0, 3), ['Pending','Approved','Rejected']);
assert.deepEqual(schemas.MemberStatus.enum, ['Active','Suspended','Inactive']);
for (const schema of ['Application','Member','ApplicationSubmission','MemberUpdate']) assert.equal(Object.keys(schemas[schema].properties).some(name => /tenantId/i.test(name)), false);
console.log(`OpenAPI ${contract.info.version}: membership payloads, statuses and token lifetime match frontend assumptions.`);

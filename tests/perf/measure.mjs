// Response-time measurements for Phase 14 step H4 (docs/analysis/production-hardening.md §4.5; results in docs/TESTING.md).
// Runs against a local API started on a DEMO volume database (generate-volume), with the Development sign-in bypass.
//
//   node tests/perf/measure.mjs <api base url> <samples.json> [only]
//
// samples.json is written by tests/perf/samples.sql (psql -At -f): ids of DEMO properties, barangays, municipalities,
// sections and owner surnames to vary the requests. Each case is run 20 times with different inputs, one at a time, after
// one warm-up call; the Property Profile fires its calls in parallel, as the browser does, and is timed as a whole.

import { readFileSync } from 'node:fs';

const [base, samplesPath, only] = process.argv.slice(2);
const s = JSON.parse(readFileSync(samplesPath, 'utf8'));
const headers = { 'X-Prime-Dev-Act-As': 'admin', 'Content-Type': 'application/json' };
const RUNS = Number(process.env.RUNS ?? 20);
const pick = (list, i) => list[(i * 7919) % list.length];

async function call(path, init) {
  const response = await fetch(base + path, { headers, ...init });
  const body = await response.text();
  if (!response.ok) throw new Error(`${init?.method ?? 'GET'} ${path} → ${response.status} ${body.slice(0, 300)}`);
  return body ? JSON.parse(body) : null;
}

// The calls the Property Profile makes on opening (frontend/prime-web/src/pages/properties), per unit for the unit sections.
async function profile(id) {
  const units = (await Promise.all([
    call(`/api/properties/${id}`), call(`/api/properties/${id}/rpus`), call(`/api/properties/${id}/owners`),
    call(`/api/properties/${id}/transactions`), call(`/api/properties/${id}/notices`), call(`/api/properties/${id}/exemptions`),
    call(`/api/properties/${id}/sworn-statements`), call(`/api/properties/${id}/notices-of-cancellation`),
    call(`/api/properties/${id}/parcels`),
  ]))[1];
  await Promise.all(units.flatMap((u) => [
    call(`/api/rpus/${u.id}/${u.rpuType === 'Building' ? 'building' : 'land'}`).catch(() => null),
    call(`/api/rpus/${u.id}/tax-declarations`), call(`/api/rpus/${u.id}/valuations`), call(`/api/rpus/${u.id}/assessments`),
  ]));
}

// A register is a dated run, then issued: the issue builds the rows and freezes them (the heavy part).
async function register(request) {
  const run = await call('/api/registers', { method: 'POST', body: JSON.stringify(request) });
  return call('/api/forms/issue', { method: 'POST', body: JSON.stringify({ formCode: run.formCode, subjectId: run.id }) });
}

const bbox = (x, y, size) => [x, y, x + size, y + size].map((v) => v.toFixed(5)).join(',');

const cases = {
  'property search: PIN fragment': (i) => call(`/api/properties?searchTerm=${encodeURIComponent(pick(s.pins, i).slice(0, 14))}&page=1&pageSize=20`),
  'property search: exact PIN': (i) => call(`/api/properties?searchTerm=${encodeURIComponent(pick(s.pins, i))}&page=1&pageSize=20`),
  'property search: no match': (i) => call(`/api/properties?searchTerm=zz${i}&page=1&pageSize=20`),
  'property list: barangay': (i) => call(`/api/properties?barangayId=${pick(s.barangays, i)}&page=1&pageSize=20`),
  'property list: municipality, page 200': (i) => call(`/api/properties?municipalityId=${pick(s.municipalities, i)}&page=200&pageSize=20`),
  'owner search: surname': (i) => call(`/api/taxpayers?searchTerm=${encodeURIComponent(pick(s.surnames, i))}&page=1&pageSize=20`),
  'property profile (all calls)': (i) => profile(pick(s.properties, i)),
  'tax map: barangay zoom': (i) => call(`/api/gis/parcels?bbox=${bbox(pick(s.points, i)[0], pick(s.points, i)[1], 0.04)}`),
  'tax map: municipality zoom': (i) => call(`/api/gis/parcels?bbox=${bbox(pick(s.points, i)[0] - 0.1, pick(s.points, i)[1] - 0.1, 0.3)}`),
  'tax map: click a parcel': (i) => call(`/api/gis/parcels/at?lon=${pick(s.points, i)[0] + 0.0005}&lat=${pick(s.points, i)[1] + 0.0005}`),
  'audit trail: page 1': () => call('/api/audit-logs?page=1&pageSize=50'),
  'audit trail: one table, page 100': () => call('/api/audit-logs?tableName=Property&page=100&pageSize=50'),
  'audit trail: property history': (i) => call(`/api/audit-logs?propertyId=${pick(s.properties, i)}&page=1&pageSize=50`),
  'audit trail: one record': (i) => call(`/api/audit-logs?recordId=${pick(s.properties, i)}&page=1&pageSize=50`),
  'SMV schedule rows (one per barangay and class)': () => call(`/api/smv/${s.smv}/schedules`),
  'approval queue: province': () => call('/api/approvals/awaiting'),
  'register: TMCR of a section, issued': (i) => register({ kind: 'TaxMapControlRoll', asOf: '2027-01-01', sectionId: pick(s.sections, i), barangayId: null }),
  'register: TMCR of a barangay, issued': (i) => register({ kind: 'TaxMapControlRoll', asOf: '2027-01-01', barangayId: pick(s.barangays, i) }),
  'register: assessment roll of a barangay, issued': (i) => register({ kind: 'AssessmentRollTaxable', asOf: '2027-01-01', barangayId: pick(s.barangays, i) }),
};

const percentile = (sorted, p) => sorted[Math.min(sorted.length - 1, Math.ceil((p / 100) * sorted.length) - 1)];

for (const [name, run] of Object.entries(cases)) {
  if (only && !name.includes(only)) continue;
  try {
    await run(RUNS); // warm-up, not counted
    const times = [];
    for (let i = 0; i < RUNS; i++) {
      const start = performance.now();
      await run(i);
      times.push(performance.now() - start);
    }
    times.sort((a, b) => a - b);
    console.log(`${name.padEnd(42)} p50 ${percentile(times, 50).toFixed(0).padStart(6)} ms   p95 ${percentile(times, 95).toFixed(0).padStart(6)} ms   max ${times.at(-1).toFixed(0).padStart(6)} ms`);
  } catch (error) {
    console.log(`${name.padEnd(42)} FAILED: ${error.message}`);
  }
}

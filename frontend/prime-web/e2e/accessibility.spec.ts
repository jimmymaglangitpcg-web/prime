import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { apiAs, e2eSet, expect, pageAs, runTag, test } from './support';

/**
 * Automated WCAG 2.1 AA checks (axe) on the main screens (docs/analysis/production-hardening.md §4.7, Q10). Serious and
 * critical findings fail the test; moderate and minor ones are listed in the report and in docs/TESTING.md. The manual
 * keyboard pass is recorded in docs/TESTING.md.
 */
const blocking = ['serious', 'critical'];

async function audit(page: Page, screen: string) {
  // The OpenLayers canvas and the map's attribution are third-party; the rest of the page is checked.
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).exclude('.ol-viewport').analyze();
  const lines = results.violations.map((v) =>
    `${v.impact ?? 'unknown'} ${v.id}: ${v.help} (${v.nodes.length}) — ${v.nodes.slice(0, 3).map((n) => n.target.join(' ')).join(' | ')}`);
  await test.info().attach(`axe — ${screen}`, { body: lines.join('\n') || 'no violations', contentType: 'text/plain' });
  if (process.env.AXE_REPORT) console.log(`\n## ${screen}\n${lines.join('\n') || 'no violations'}`);
  const failing = results.violations.filter((v) => blocking.includes(v.impact ?? ''));
  expect(failing.map((v) => `${v.impact} ${v.id}: ${v.help} — ${v.nodes.map((n) => n.target.join(' ')).slice(0, 3).join(' | ')}`), screen).toEqual([]);
}

test.describe('accessibility', () => {
  let propertyId: string;

  test.beforeAll(async ({ request }) => {
    const admin = apiAs(request, 'admin');
    const set = await e2eSet(admin);
    const tag = runTag();
    const property = await admin.post<{ id: string }>('/api/properties', {
      propertyIdentificationNumber: `DEMO-E2E-A11Y-${tag}`, provinceId: set.provinceId, municipalityId: set.municipality.id,
      barangayId: set.barangay.id, street: `DEMO E2E accessibility ${tag}`,
    });
    propertyId = property.id;
  });

  const screens: [string, () => string, (page: Page) => Promise<void>][] = [
    ['Dashboard', () => '/', async (p) => { await expect(p.getByText('Assessed value by classification')).toBeVisible(); }],
    ['Property search', () => '/properties', async (p) => { await expect(p.locator('.ant-table')).toBeVisible(); }],
    ['Register property', () => '/properties/new', async (p) => { await expect(p.getByRole('button', { name: 'Register Property' })).toBeVisible(); }],
    ['Property profile', () => `/properties/${propertyId}`, async (p) => { await expect(p.getByRole('tab', { name: /RPUs/ })).toBeVisible(); }],
    ['Taxpayer search', () => '/taxpayers', async (p) => { await expect(p.locator('.ant-table')).toBeVisible(); }],
    ['Awaiting my approval', () => '/approvals', async (p) => { await expect(p.getByRole('main')).toBeVisible(); }],
    ['Registers', () => '/registers', async (p) => { await expect(p.getByRole('button', { name: 'Create run' })).toBeVisible(); }],
    ['Tax map', () => '/gis', async (p) => { await expect(p.locator('.ol-viewport')).toBeVisible(); }],
    ['General revision', () => '/general-revision', async (p) => { await expect(p.getByRole('main')).toBeVisible(); }],
    ['Offices', () => '/admin/offices', async (p) => { await expect(p.getByRole('main')).toBeVisible(); }],
    ['Audit trail', () => '/admin/audit', async (p) => { await expect(p.locator('.ant-table')).toBeVisible(); }],
  ];

  for (const [screen, url, ready] of screens) {
    test(screen, async ({ browser }) => {
      const page = await pageAs(browser, 'admin');
      await page.goto(url());
      await ready(page);
      await page.waitForLoadState('networkidle');
      await audit(page, screen);
    });
  }
});

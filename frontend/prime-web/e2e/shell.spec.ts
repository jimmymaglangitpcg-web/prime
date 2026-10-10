import { apiAs, e2eSet, expect, pageAs, runTag, test } from './support';

/**
 * The application shell (docs/analysis/ui-theme.md §4.2–§4.3): the header's search opens the property search with the
 * typed text in the address, and the grouped menu opens the current screen's group.
 */
test('the header search finds a property and the menu opens its group', async ({ browser, request }) => {
  const admin = apiAs(request, 'admin');
  const set = await e2eSet(admin);
  const tag = runTag();
  const pin = `DEMO-E2E-SHELL-${tag}`;
  await admin.post('/api/properties', {
    propertyIdentificationNumber: pin, provinceId: set.provinceId, municipalityId: set.municipality.id, barangayId: set.barangay.id,
    street: `DEMO E2E shell ${tag}`,
  });

  const page = await pageAs(browser, 'checker');
  await page.goto('/');
  const search = page.getByRole('search').getByRole('searchbox');
  await search.fill(pin);
  await search.press('Enter');

  await expect(page).toHaveURL((url) => url.pathname === '/properties' && url.searchParams.get('q') === pin);
  await expect(page.getByRole('row').filter({ hasText: pin })).toBeVisible();
  await expect(page.getByRole('searchbox', { name: 'Search properties', exact: true })).toHaveValue(pin);
  // The current screen's group is open, its screen selected.
  await expect(page.getByRole('menuitem', { name: 'Properties' })).toBeVisible();
  await expect(page.getByRole('menuitem', { name: 'Owners & taxpayers' })).toBeVisible();

  // Opened from the address, as a bookmark would.
  await page.goto(`/properties?q=${encodeURIComponent(pin)}`);
  await expect(page.getByRole('row').filter({ hasText: pin })).toBeVisible();
});

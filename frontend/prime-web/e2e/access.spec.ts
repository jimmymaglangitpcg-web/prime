import { apiAs, choose, e2eSet, expect, pageAs, runTag, test, today } from './support';

/**
 * Who may do what (CLAUDE.md §46, §47, §117; docs/analysis/workflow-security.md): a refused permission, a maker-checker
 * refusal, a municipal user kept to their jurisdiction, and a sign-up approved by an administrator.
 */

interface Created { id: string; propertyIdentificationNumber: string }

test('a user without the permission is refused, and told why', async ({ browser, request }) => {
  const admin = apiAs(request, 'admin');
  const set = await e2eSet(admin);
  const tag = runTag();
  const created = await admin.post<Created>('/api/properties', {
    propertyIdentificationNumber: `DEMO-E2E-ACC-${tag}`, provinceId: set.provinceId, municipalityId: set.municipality.id,
    barangayId: set.barangay.id, street: `DEMO E2E access ${tag}`,
  });

  // The API refuses a write the viewer's role does not allow.
  const refused = await apiAs(request, 'viewer').postStatus('/api/properties', {
    propertyIdentificationNumber: `DEMO-E2E-ACC-V-${tag}`, provinceId: set.provinceId, municipalityId: set.municipality.id, barangayId: set.barangay.id,
  });
  expect(refused).toEqual({ status: 403, code: 'PERMISSION_DENIED' });

  // On screen: the viewer reads the property, the menu offers no administration, and a write is refused with the reason.
  const viewer = await pageAs(browser, 'viewer');
  await viewer.goto(`/properties/${created.id}`);
  await expect(viewer.getByText(created.propertyIdentificationNumber).first()).toBeVisible();
  await expect(viewer.getByRole('menuitem', { name: 'Properties' }).first()).toBeVisible();
  // The grouped menu (ui-theme.md §4.2): open Administration, which the viewer has for offices and configuration views.
  await viewer.getByRole('menuitem', { name: 'Administration' }).click();
  await expect(viewer.getByRole('menuitem', { name: 'Offices' })).toBeVisible();
  for (const entry of ['Content packs', 'Audit trail', /Sign-up requests/]) {
    await expect(viewer.getByRole('menuitem', { name: entry })).toHaveCount(0);
  }
  await viewer.goto('/properties/new');
  await viewer.getByLabel('Property Identification Number (PIN)').fill(`DEMO-E2E-ACC-V-${tag}`);
  await choose(viewer, 'Province', 'DEMO E2E Province');
  await choose(viewer, 'City/Municipality', set.municipality.name);
  await choose(viewer, 'Barangay', set.barangay.name);
  await viewer.getByRole('button', { name: 'Register Property' }).click();
  await expect(viewer.getByText(/do not have the permission/)).toBeVisible();
});

test('the creator of an assessment cannot also approve it', async ({ browser, request }) => {
  // The provincial assessor holds both assessment.prepare and assessment.approve, so only separation of duties stops them.
  const assessor = apiAs(request, 'checker');
  const set = await e2eSet(assessor);
  const tag = runTag();
  const created = await assessor.post<Created>('/api/properties', {
    propertyIdentificationNumber: `DEMO-E2E-MC-${tag}`, provinceId: set.provinceId, municipalityId: set.municipality.id,
    barangayId: set.barangay.id, street: `DEMO E2E maker-checker ${tag}`,
  });
  const rpu = await assessor.post<{ id: string }>('/api/rpus', { propertyId: created.id, rpuNumber: `DEMO-E2E-MC-RPU-${tag}`, rpuType: 'Land', effectivityDate: today() });
  await assessor.post('/api/land', { rpuId: rpu.id, area: 500, classificationId: set.classification.id, actualUseId: set.actualUse.id, isCornerLot: false });
  const valuation = await assessor.post<{ id: string }>(`/api/rpus/${rpu.id}/valuations`);
  const assessment = await assessor.post<{ id: string }>('/api/assessments', {
    valuationId: valuation.id, assessmentYear: Number(today().slice(0, 4)), effectiveDate: today(), previousAssessmentId: null,
    revisionReference: null, remarks: 'DEMO E2E maker-checker',
  });
  await assessor.post(`/api/assessments/${assessment.id}/submit-for-review`);

  const page = await pageAs(browser, 'checker');
  await page.goto(`/properties/${created.id}`);
  await page.getByRole('tab', { name: /RPUs/ }).click();
  await page.locator('tr.ant-table-row').filter({ hasText: `DEMO-E2E-MC-RPU-${tag}` }).locator('button.ant-table-row-expand-icon').click();
  await page.getByRole('button', { name: 'Approve', exact: true }).click();
  await expect(page.getByText("The assessment's creator cannot also approve it.")).toBeVisible();
  // Still in review: the decision is left to another user.
  await expect(page.getByRole('button', { name: 'Approve', exact: true })).toBeVisible();
  const after = await assessor.get<{ status: string }>(`/api/assessments/${assessment.id}`);
  expect(after.status).toBe('PendingReview');
});

test('a municipal user sees only the properties of their jurisdiction', async ({ browser, request }) => {
  const admin = apiAs(request, 'admin');
  const set = await e2eSet(admin);
  const tag = runTag();
  const me = await apiAs(request, 'mun-appraiser').get<{ municipalityIds: string[] | null }>('/api/me');
  const own = me.municipalityIds?.[0];
  expect(own, 'the DEMO municipal appraiser has an office with a jurisdiction').toBeTruthy();
  // The other E2E municipality is outside it, whichever DEMO municipality the office covers.
  expect(me.municipalityIds).not.toContain(set.otherMunicipality.id);
  const ownMunicipality = (await admin.get<{ id: string; provinceId: string }[]>('/api/reference/municipalities')).find((m) => m.id === own)!;
  const ownBarangay = (await admin.get<{ id: string }[]>(`/api/reference/barangays?municipalityId=${own}`))[0];
  const inside = await admin.post<Created>('/api/properties', {
    propertyIdentificationNumber: `DEMO-E2E-IN-${tag}`, provinceId: ownMunicipality.provinceId, municipalityId: own, barangayId: ownBarangay.id,
  });
  const outside = await admin.post<Created>('/api/properties', {
    propertyIdentificationNumber: `DEMO-E2E-OUT-${tag}`, provinceId: set.provinceId, municipalityId: set.otherMunicipality.id, barangayId: set.otherBarangay.id,
  });

  const page = await pageAs(browser, 'mun-appraiser');
  await page.goto('/properties');
  const search = page.getByPlaceholder(/search/i).first();
  await search.fill(`DEMO-E2E-IN-${tag}`);
  await search.press('Enter');
  await expect(page.getByText(inside.propertyIdentificationNumber)).toBeVisible();
  await search.fill(`DEMO-E2E-OUT-${tag}`);
  await search.press('Enter');
  await expect(page.locator('.ant-empty').first()).toBeVisible();
  await expect(page.getByText(outside.propertyIdentificationNumber)).toHaveCount(0);
  // Opened directly, the record outside the jurisdiction is not found, as if it did not exist.
  await page.goto(`/properties/${outside.id}`);
  await expect(page.getByText('No property was found with the given id.')).toBeVisible();
  expect((await apiAs(request, 'mun-appraiser').postStatus(`/api/rpus`, {
    propertyId: outside.id, rpuNumber: `DEMO-E2E-OUT-RPU-${tag}`, rpuType: 'Land', effectivityDate: today(),
  })).status).toBe(404);
});

test('a new user signs up and an administrator approves them', async ({ browser }) => {
  const tag = runTag();
  const name = `DEMO E2E Applicant ${tag}`;
  const applicant = await pageAs(browser, `applicant-${tag}`);
  await applicant.goto('/');
  await applicant.getByLabel('Full name').fill(name);
  await applicant.getByLabel('Position').fill('DEMO Assessment Clerk');
  await choose(applicant, 'Office', "DEMO Municipal Assessor's Office — DEMO E2E Municipality");
  await applicant.getByRole('combobox', { name: /Roles asked for/ }).click();
  await applicant.locator('.ant-select-item-option:visible').filter({ hasText: 'View Only' }).first().click();
  await applicant.keyboard.press('Escape');
  await applicant.getByRole('button', { name: 'Send request' }).click();
  await expect(applicant.getByRole('button', { name: 'Check again' })).toBeVisible();

  const admin = await pageAs(browser, 'admin');
  await admin.goto('/admin/sign-up-requests');
  const row = admin.locator('tr.ant-table-row').filter({ hasText: name });
  await row.getByRole('button', { name: 'Approve' }).click();
  await admin.getByRole('dialog', { name: `Approve — ${name}` }).getByRole('button', { name: 'Approve and activate' }).click();
  await expect(admin.getByText(`${name} can now use PRIME.`)).toBeVisible();

  // The applicant is in: PRIME opens instead of the request page.
  await applicant.reload();
  await expect(applicant.getByRole('menuitem', { name: 'Registry' })).toBeVisible();
});

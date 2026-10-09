import { apiAs, choose, e2eSet, expect, formDocument, pageAs, runTag, test, today } from './support';

/**
 * CLAUDE.md §74 through the screens: register a property and its owner, parcel and land unit; appraise and assess it;
 * a second user approves and posts; the Tax Declaration and FAAS; the Notice of Assessment; the assessment roll and
 * the Record of Assessment. DEMO arithmetic (seed-e2e): 500 sqm × 1,000 = market value 500,000; 20 % → assessed 100,000.
 */
test('a land property from registration to the assessment roll', async ({ browser, request }) => {
  test.setTimeout(240_000);
  const set = await e2eSet(apiAs(request, 'admin'));
  const tag = runTag();
  const pin = `DEMO-E2E-${tag}`;
  const maker = await pageAs(browser, 'admin');

  // 1. Register the property.
  await maker.goto('/properties/new');
  await maker.getByLabel('Property Identification Number (PIN)').fill(pin);
  await choose(maker, 'Province', 'DEMO E2E Province');
  await choose(maker, 'City/Municipality', set.municipality.name);
  await choose(maker, 'Barangay', set.barangay.name);
  await maker.getByLabel('Street').fill(`DEMO E2E street ${tag}`);
  await maker.getByLabel('Lot Number').fill('L-1');
  await maker.getByRole('button', { name: 'Register Property' }).click();
  await expect(maker).toHaveURL(/\/properties\/[0-9a-f-]{36}$/);
  await expect(maker.getByText(pin).first()).toBeVisible();

  // 2. Register the owner, through the party dialog's new-taxpayer form.
  await maker.getByRole('tab', { name: /Owners/ }).click();
  await maker.getByRole('button', { name: 'Add Party' }).click();
  const dialog = maker.getByRole('dialog', { name: 'Add Party to Property' });
  await dialog.getByRole('button', { name: 'Register a new taxpayer' }).click();
  await dialog.getByLabel('Last Name').fill(`DEMO E2E Owner ${tag}`);
  await dialog.getByLabel('First Name').fill('Juana');
  await dialog.getByRole('button', { name: /Register/ }).click();
  // The new taxpayer is chosen by name, not shown as its id.
  await expect(dialog.getByRole('combobox', { name: 'Taxpayer' }).locator('xpath=ancestor::div[contains(concat(" ", @class, " "), " ant-select ")][1]')).toContainText(`DEMO E2E Owner ${tag}`);
  await choose(maker, 'Ownership Type', set.ownershipType.name);
  await dialog.getByLabel('Ownership Percentage').fill('100');
  await dialog.getByRole('button', { name: 'Add', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(maker.getByRole('cell', { name: new RegExp(`DEMO E2E Owner ${tag}`) })).toBeVisible();

  // 3. Map the parcel.
  await maker.getByRole('tab', { name: /Parcels/ }).click();
  await maker.getByRole('button', { name: 'Add Parcel' }).click();
  const parcel = maker.getByRole('dialog', { name: 'Add Parcel' });
  // The property's location is proposed.
  await expect(parcel.getByRole('combobox', { name: /Barangay/ }).locator('xpath=ancestor::div[contains(concat(" ", @class, " "), " ant-select ")][1]')).toContainText(set.barangay.name);
  await parcel.getByLabel('Area (sqm)').fill('500');
  await parcel.getByLabel('Lot Number').fill('L-1');
  await parcel.getByRole('button', { name: 'Add Parcel' }).click();
  await expect(parcel).toBeHidden();

  // 4. Create the land RPU and record its land.
  await maker.getByRole('tab', { name: /RPUs/ }).click();
  await maker.getByRole('button', { name: 'Add RPU' }).click();
  const rpuDialog = maker.getByRole('dialog', { name: /Add Real Property Unit/ });
  await rpuDialog.getByLabel('RPU Number').fill(`DEMO-E2E-RPU-${tag}`);
  await choose(maker, 'RPU Type', 'Land');
  await rpuDialog.getByRole('button', { name: 'Add RPU' }).click();
  await expect(rpuDialog).toBeHidden();
  const rpuRow = maker.locator('tr.ant-table-row').filter({ hasText: `DEMO-E2E-RPU-${tag}` });
  await rpuRow.locator('button.ant-table-row-expand-icon').click();
  await maker.getByRole('button', { name: 'Add Land' }).click();
  const landDialog = maker.getByRole('dialog', { name: 'Add Land' });
  await landDialog.getByLabel('Area (sqm)').fill('500');
  await choose(maker, 'Classification', set.classification.name);
  await choose(maker, 'Actual Use', set.actualUse.name);
  await landDialog.getByRole('button', { name: /Add/ }).last().click();
  await expect(landDialog).toBeHidden();

  // 5. Appraise: value the unit and read the market value.
  await maker.getByRole('button', { name: 'Value and assess' }).click();
  const drawer = maker.getByRole('dialog', { name: 'Valuation' });
  await drawer.getByRole('button', { name: 'Value', exact: true }).click();
  await expect(drawer.getByText('500,000.00').first()).toBeVisible();

  // 6. Assess: draft the assessment (20 % → 100,000) and submit it.
  await drawer.getByRole('button', { name: 'Assess this valuation' }).click();
  const assess = maker.getByRole('dialog', { name: 'Assess this valuation' });
  await assess.getByRole('button', { name: 'Preview' }).click();
  await expect(assess.getByText('100,000.00').first()).toBeVisible();
  await assess.getByRole('button', { name: 'Create draft assessment' }).click();
  await expect(assess).toBeHidden();
  await drawer.getByRole('button', { name: 'Close' }).click();
  await maker.getByRole('button', { name: 'Submit for review' }).click();
  // The maker (an encoder, without assessment.approve) is offered no decision.
  await expect(maker.getByRole('button', { name: 'Approve', exact: true })).toHaveCount(0);

  // 7. A second user, the provincial assessor, approves and posts it.
  const propertyUrl = maker.url();
  const checker = await pageAs(browser, 'checker');
  await checker.goto(propertyUrl);
  await checker.getByRole('tab', { name: /RPUs/ }).click();
  await checker.locator('tr.ant-table-row').filter({ hasText: `DEMO-E2E-RPU-${tag}` }).locator('button.ant-table-row-expand-icon').click();
  await checker.getByRole('button', { name: 'Approve', exact: true }).click();
  await checker.getByRole('button', { name: 'Post', exact: true }).click();
  await checker.locator('.ant-popconfirm').getByRole('button', { name: /OK|Yes|Post/ }).click();
  await expect(checker.getByText('Posted').first()).toBeVisible();

  // 8. The Tax Declaration declaring the posted assessment: prepared by the maker, approved by the checker; then the
  //    TD and the FAAS (TD + assessment) are issued as documents.
  await maker.reload();
  await maker.getByRole('tab', { name: /RPUs/ }).click();
  await maker.locator('tr.ant-table-row').filter({ hasText: `DEMO-E2E-RPU-${tag}` }).locator('button.ant-table-row-expand-icon').click();
  await expect(maker.getByRole('button', { name: 'Post', exact: true })).toHaveCount(0);
  // The land panel shows the unit's values from its posted assessment.
  await expect(maker.getByText('100,000.00 (effective').first()).toBeVisible();
  await maker.getByRole('button', { name: 'Add Tax Declaration' }).click();
  const tdDialog = maker.getByRole('dialog', { name: 'Add Tax Declaration' });
  const tdNumber = `DEMO-E2E-TD-${tag}`;
  await tdDialog.getByLabel('Tax Declaration Number').fill(tdNumber);
  await choose(maker, 'Declares assessment', 'AV 100,000.00');
  await tdDialog.getByRole('button', { name: 'Add Tax Declaration' }).click();
  await expect(tdDialog).toBeHidden();
  const tdRow = (page: typeof maker) => page.locator('tr.ant-table-row').filter({ hasText: tdNumber });
  await tdRow(maker).getByRole('button', { name: 'Submit', exact: true }).click();
  await maker.locator('.ant-modal-confirm').getByRole('button', { name: 'OK' }).click();
  await expect(tdRow(maker).getByText('Pending review').or(tdRow(maker).getByText('PendingReview'))).toBeVisible();
  // The maker holds no td.approve, so no decision is offered.
  await expect(tdRow(maker).getByRole('button', { name: 'Approve', exact: true })).toHaveCount(0);
  await checker.reload();
  await checker.getByRole('tab', { name: /RPUs/ }).click();
  await checker.locator('tr.ant-table-row').filter({ hasText: `DEMO-E2E-RPU-${tag}` }).locator('button.ant-table-row-expand-icon').click();
  await tdRow(checker).getByRole('button', { name: 'Approve', exact: true }).click();
  await checker.locator('.ant-modal-confirm').getByRole('button', { name: 'OK' }).click();
  await expect(tdRow(checker).getByText('Approved')).toBeVisible();
  await tdRow(checker).getByRole('button', { name: 'FAAS' }).click();
  await expect(checker).toHaveURL(/\/documents\/[0-9a-f-]{36}$/);
  await expect(formDocument(checker).getByText(pin).first()).toBeVisible();
  await expect(formDocument(checker).getByText('100,000.00').first()).toBeVisible();

  // 9. The Notice of Assessment: generated from the posted assessment and issued.
  await maker.reload();
  await maker.getByRole('tab', { name: 'Notices' }).click();
  await maker.getByRole('button', { name: 'Generate notice' }).click();
  const noticeDialog = maker.getByRole('dialog', { name: 'Generate Notice of Assessment' });
  await choose(maker, 'RPU', `RPU DEMO-E2E-RPU-${tag}`);
  await choose(maker, 'Posted assessment', 'AV 100,000.00');
  await noticeDialog.getByRole('button', { name: 'Generate draft' }).click();
  await expect(noticeDialog).toBeHidden();
  await maker.getByRole('button', { name: 'Issue', exact: true }).click();
  await expect(maker.getByRole('button', { name: 'Record service' })).toBeVisible();

  // 10. The records: the taxable assessment roll of the barangay and the Record of Assessment list its TD and owner.
  for (const register of ['Assessment Roll — Taxable', 'Record of Assessment'] as const) {
    await maker.goto('/registers');
    await choose(maker, 'Register', register);
    if (register === 'Record of Assessment') {
      await choose(maker, 'Classification', set.classification.name);
      const day = today();
      await maker.getByPlaceholder('Start date').fill(`${day.slice(0, 4)}-01-01`);
      await maker.getByPlaceholder('End date').fill(day);
      await maker.keyboard.press('Enter');
    }
    await choose(maker, 'Province', 'DEMO E2E Province');
    await choose(maker, 'City/Municipality', set.municipality.name);
    await choose(maker, 'Barangay', set.barangay.name);
    // Tagged, so the run issued is this one, not an earlier run (whose issued form is frozen) listed before the refresh.
    await maker.getByLabel('Remarks').fill(`DEMO E2E ${tag}`);
    await maker.getByRole('button', { name: 'Create run' }).click();
    await maker.locator('tr.ant-table-row').filter({ hasText: register }).filter({ hasText: `DEMO E2E ${tag}` }).getByRole('button', { name: 'Issue' }).click();
    await expect(maker).toHaveURL(/\/documents\/[0-9a-f-]{36}$/);
    await expect(formDocument(maker).getByText(tdNumber).first()).toBeVisible();
    await expect(formDocument(maker).getByText(`DEMO E2E Owner ${tag}`).first()).toBeVisible();
  }
});

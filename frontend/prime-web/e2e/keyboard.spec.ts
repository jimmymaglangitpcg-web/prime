import { apiAs, e2eSet, expect, pageAs, runTag, test } from './support';

/**
 * Keyboard only (CLAUDE.md §83; production-hardening.md Q10): a property is registered with Tab, typing and Enter, and
 * every control the focus reaches shows that it has it. A person's manual pass of the main workflows is recorded in
 * docs/TESTING.md §5; this keeps the main form from regressing.
 */
test('a property is registered with the keyboard alone, with the focus always visible', async ({ browser, request }) => {
  const set = await e2eSet(apiAs(request, 'admin'));
  const tag = runTag();
  const page = await pageAs(browser, 'admin');
  await page.goto('/properties/new');
  const pin = page.getByLabel('Property Identification Number (PIN)');
  await pin.focus();

  const focusShows = () => page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null;
    if (!el || el === document.body) return 'nothing focused';
    // Ant Design marks a focused input, select or button with an outline or a box-shadow on it or its wrapper.
    for (let node: HTMLElement | null = el, depth = 0; node && depth < 4; node = node.parentElement, depth++) {
      const s = getComputedStyle(node);
      if ((s.outlineStyle !== 'none' && s.outlineWidth !== '0px') || s.boxShadow !== 'none') return 'visible';
    }
    return `no focus indicator on ${el.tagName.toLowerCase()}${el.id ? `#${el.id}` : ''}`;
  });

  await page.keyboard.type(`DEMO-E2E-KB-${tag}`);
  expect(await focusShows()).toBe('visible');
  // Province, municipality and barangay: type to filter, Enter to choose, Tab on.
  for (const choice of ['DEMO E2E Province', set.municipality.name, set.barangay.name]) {
    await page.keyboard.press('Tab');
    expect(await focusShows()).toBe('visible');
    await page.keyboard.type(choice);
    await expect(page.locator('.ant-select-item-option-active:visible')).toContainText(choice);
    await page.keyboard.press('Enter');
  }
  await page.keyboard.press('Tab'); // zone (optional)
  await page.keyboard.press('Tab');
  expect(await focusShows()).toBe('visible');
  await page.keyboard.type(`DEMO E2E keyboard ${tag}`); // street
  // On to the button and press it.
  for (let i = 0; i < 12 && !(await page.getByRole('button', { name: 'Register Property' }).evaluate((b) => b === document.activeElement)); i++) {
    await page.keyboard.press('Tab');
  }
  expect(await focusShows()).toBe('visible');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/properties\/[0-9a-f-]{36}$/);
  await expect(page.getByText(`DEMO-E2E-KB-${tag}`).first()).toBeVisible();
});

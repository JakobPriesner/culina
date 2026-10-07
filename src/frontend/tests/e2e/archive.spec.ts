import { expect, test, type Page } from '@playwright/test';

import {
  ensureAccount,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

test.describe.configure({ mode: 'serial' });

test.describe('an archive of everything', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;
  let title: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    // Restoring adds recipes; reusing a household would double its fixtures on every run.
    const account = unique(`archive-${testInfo.project.name}`).replace(/\s+/g, '-');
    await signInWithHousehold(page, await ensureAccount(browser, account));

    title = unique('Archived');

    await seedRecipe(page, {
      title,
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.']
    });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('downloads as a file, and puts it back', async () => {
    await page.goto('/me/household');

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('link', { name: /download everything|alles herunterladen/i }).click()
    ]);

    const archive = await download.createReadStream();
    const chunks: Buffer[] = [];

    for await (const chunk of archive) {
      chunks.push(chunk as Buffer);
    }

    const written = JSON.parse(Buffer.concat(chunks).toString('utf8'));

    expect(written.culina).toBe(1);
    expect(written.recipes.map((one: { title: string }) => one.title)).toContain(title);

    const restoredRecipe = written.recipes.find((one: { title: string }) => one.title === title);

    // A step's ingredient travels as a position, never an id: ids are per database, and a dangling
    // step is the failure this app exists to prevent.
    const reference = restoredRecipe.steps[0].segments.find(
      (one: { ingredient?: number }) => one.ingredient !== undefined
    );

    expect(reference.ingredient).toBe(0);

    await page.getByLabel(/choose an archive|archiv auswählen/i).setInputFiles({
      name: 'culina.json',
      mimeType: 'application/json',
      buffer: Buffer.concat(chunks)
    });

    await expect(page.getByRole('status')).toContainText(/restored|wiederhergestellt/i);

    await page.goto('/');
    await page.getByRole('searchbox').fill(title);

    await expect(page.getByRole('link', { name: new RegExp(title) })).toHaveCount(2);
  });
});

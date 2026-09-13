import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/**
 * Taking your recipes with you, and bringing them back.
 *
 * A backup nobody has restored is not a backup. This does the whole journey
 * through the screens a person would actually use.
 */
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

    await signInWithHousehold(page, await accountFor(browser, testInfo));

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
    await page.goto('/me');

    // A real download, through the browser's own machinery — which is what
    // gives the file a name and somewhere to land.
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

    // Readable: somebody with no Culina at all can open this and find their
    // recipes written out in words.
    expect(written.culina).toBe(1);
    expect(written.recipes.map((one: { title: string }) => one.title)).toContain(title);

    const restoredRecipe = written.recipes.find((one: { title: string }) => one.title === title);

    // A step's ingredient travels as a position, never an id: ids are assigned
    // by whichever database it lands in, and a step pointing at nothing is the
    // one failure this app exists to prevent.
    const reference = restoredRecipe.steps[0].segments.find(
      (one: { ingredient?: number }) => one.ingredient !== undefined
    );

    expect(reference.ingredient).toBe(0);

    // And back in. A restore adds; it never replaces what is already there.
    await page.getByLabel(/choose an archive|archiv auswählen/i).setInputFiles({
      name: 'culina.json',
      mimeType: 'application/json',
      buffer: Buffer.concat(chunks)
    });

    await expect(page.getByRole('status')).toContainText(/restored|wiederhergestellt/i);

    await page.goto('/');
    await page.getByRole('searchbox').fill(title);

    // Two of them now: the original and the one that came back.
    await expect(page.getByRole('link', { name: new RegExp(title) })).toHaveCount(2);
  });
});

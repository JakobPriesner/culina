import { expect, test, type Page } from '@playwright/test';

import { cookbookId, responsiveData } from './support/responsive';

/** A row without its tag or its time: the same recipe with those lines left out, so the row is as short as it gets. */
async function bare(page: Page, { tags, minutes }: { tags: boolean; minutes: boolean }) {
  await page.addStyleTag({
    content: `${tags ? '' : '.eyebrow,'}${minutes ? '' : '.meta,'} .never { display: none !important; }`
  });
}

test.describe('cookbook rows keep their remove button apart from the recipe @offline', () => {
  test.use({ serviceWorkers: 'block' });

  for (const width of [320, 390, 540]) {
    for (const [name, facts] of [
      ['with tags and time', { tags: true, minutes: true }],
      ['without tags or time', { tags: false, minutes: false }]
    ] as const) {
      test(`the remove button stays a clear target off the title at ${width}px ${name}`, async ({
        page
      }, testInfo) => {
        test.skip(testInfo.project.name !== 'desktop', 'Explicit phone viewport.');
        await page.setViewportSize({ width, height: 800 });
        await responsiveData(page, 'en', { nutritionLines: true });
        await page.goto(`/cookbooks/${cookbookId}`);
        await bare(page, facts);

        const rows = page.locator('ul.grid > li');
        await expect(rows.first()).toBeVisible();
        // A grid of cards says what a calorie figure means once, not on every row.
        await expect(rows.getByRole('button', { name: 'About calorie values' })).toHaveCount(0);
        const count = await rows.count();
        let checked = 0;
        for (let index = 0; index < count; index++) {
          const row = rows.nth(index);
          const remove = row.getByRole('button', { name: /^Take .* out of/i });
          if (!(await remove.isVisible())) continue;
          // Centred, clear of the bottom navigation, so a hit test sees the row and not the bar.
          await row.evaluate((el) => el.scrollIntoView({ block: 'center' }));
          checked++;

          const a = (await remove.boundingBox())!;
          expect(a.width).toBeGreaterThanOrEqual(32);
          expect(a.height).toBeGreaterThanOrEqual(32);

          // What a finger lands on at the button's centre is that button.
          const hit = await remove.evaluate(
            (el, [x, y]) => el.contains(document.elementFromPoint(x!, y!)),
            [a.x + a.width / 2, a.y + a.height / 2]
          );
          expect(hit).toBe(true);

          // The title is not covered by the remove button either.
          const title = (await row.locator('.title').boundingBox())!;
          expect(a.x >= title.x + title.width || a.y >= title.y + title.height).toBe(true);
        }
        expect(checked).toBeGreaterThan(0);
      });
    }
  }

  for (const locale of ['en', 'de'] as const) {
    for (const width of [320, 390]) {
      test(`the cookbook's more button never sits alone on a row at ${width}px in ${locale}`, async ({
        page
      }, testInfo) => {
        test.skip(testInfo.project.name !== 'desktop', 'Explicit phone viewport.');
        await page.setViewportSize({ width, height: 800 });
        await responsiveData(page, locale);
        await page.goto(`/cookbooks/${cookbookId}`);

        const actions = page.locator('.head .actions').first();
        const buttons = actions.getByRole('button');
        await expect(buttons.first()).toBeVisible();
        const more = (await actions
          .getByRole('button', { name: /^(More|Weitere)/ })
          .boundingBox())!;
        const rows = await buttons.evaluateAll((all) =>
          all.map((button) => Math.round(button.getBoundingClientRect().y))
        );
        // The menu has company on its row: the other circles beside it.
        expect(rows.filter((y) => Math.abs(y - more.y) < 4).length).toBeGreaterThan(1);
        expect(more.width).toBeGreaterThanOrEqual(44);
      });
    }
  }
});

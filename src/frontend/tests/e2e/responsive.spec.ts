import { expect, test, type Page } from '@playwright/test';
import { cookbookId, expectReflow, recipeId, responsiveData } from './support/responsive';

// Boundary pairs catch layouts that fit a phone and a laptop but break in between.
const widths = [320, 390, 639, 640, 767, 768, 1023, 1024, 1279, 1280, 1536, 1920, 2560, 3440];

test.describe('responsive production layouts @offline', () => {
  test.use({ serviceWorkers: 'block' });

  for (const locale of ['de', 'en'] as const) {
    for (const width of widths) {
      test(`${locale} pages reflow at ${width}px`, async ({ page }, testInfo) => {
        // Widths are explicit, so running the matrix twice adds no coverage.
        test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport matrix.');
        await page.setViewportSize({ width, height: 900 });
        await responsiveData(page, locale);
        await page.emulateMedia({ reducedMotion: 'reduce' });
        for (const path of [
          '/login',
          '/register',
          '/',
          '/cookbooks',
          `/cookbooks/${cookbookId}`,
          '/shopping',
          '/plan',
          '/me',
          '/me/appearance',
          '/me/household',
          '/recipes/new',
          `/recipes/${recipeId}`,
          `/recipes/${recipeId}/edit`,
          `/recipes/${recipeId}/cook`
        ]) {
          await test.step(path, async () => {
            await page.goto(path);
            await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
            await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
            await expectReflow(page);
            if (path === '/login' || path === '/register') return;
            const focusedCooking = path.endsWith('/cook') && width < 1024;
            await expect(page.locator('nav.nav:visible')).toHaveCount(focusedCooking ? 0 : 1);
            if (focusedCooking) {
              await expect(page.locator('.shell > header.header')).toBeHidden();
            } else {
              await expect(page.locator(width < 1024 ? 'nav.bottom' : 'nav.top')).toBeVisible();
            }
            // A recipe on a phone has no shell header: its back bar is sticky instead.
            if (path === `/recipes/${recipeId}` && width < 832) {
              await expect(page.locator('.shell > header.header')).toBeHidden();
            } else {
              // A name, on one line: at 1024px its dot once wrapped below it.
              const wordmark = page.locator('header .wordmark');
              expect(
                await wordmark.evaluate(
                  (element) =>
                    element.getBoundingClientRect().height <
                    1.5 * parseFloat(getComputedStyle(element).fontSize)
                )
              ).toBe(true);
            }
            if (
              locale === 'de' &&
              [320, 768, 1280].includes(width) &&
              ['/', '/shopping', '/plan', `/recipes/${recipeId}`].includes(path)
            ) {
              await page.screenshot({
                path: testInfo.outputPath(`${width}-${path.replaceAll('/', '_') || 'home'}.png`)
              });
            }
            if (path === '/shopping' || path.endsWith('/edit')) {
              const name = page.locator(
                path === '/shopping' ? '#shopping-add-name' : '#add-ingredient-name'
              );
              await expect(name).toBeVisible();
              expect((await name.boundingBox())!.width).toBeGreaterThanOrEqual(150);
            }
            if (path === '/' || path === '/plan') {
              const grid = page.locator(path === '/' ? 'ul.grid' : 'ol.week');
              const columns = await grid.evaluate(
                (el) => getComputedStyle(el).gridTemplateColumns.split(' ').length
              );
              if (path === '/plan') {
                for (const name of await page.locator('.card .name').all()) {
                  expect((await name.boundingBox())!.width).toBeGreaterThanOrEqual(100);
                }
              }
              if (path === '/' && width >= 1920) {
                // Large screens add columns to the grid while headings keep the page's width.
                expect(columns).toBeGreaterThan(width >= 2560 ? 4 : 3);
                const box = (await grid.boundingBox())!;
                expect(box.width).toBeGreaterThan(Math.min(width, 2400) * 0.8);
              } else {
                expect(columns).toBe(
                  width < 640 ? 1 : width < 1024 ? 2 : path === '/plan' && width >= 1280 ? 7 : 3
                );
              }
            }
          });
        }
      });
    }
  }

  test('narrow editor, planner picker and cooking controls remain usable', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await responsiveData(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(`/recipes/${recipeId}/edit`);
    await page
      .getByRole('button', {
        name: /Sonnenblumenkernvollkornbrot bearbeiten/i
      })
      .click();
    await expectReflow(page);
    await expect(page.locator('#ingredient-0-name')).toBeVisible();
    expect((await page.locator('#ingredient-0-name').boundingBox())!.width).toBeGreaterThan(200);
    await page.goto('/plan');
    await page.getByRole('button', { name: /^Weiter →$/i }).click();
    await expectReflow(page);
    await page
      .getByRole('button', { name: /Hinzufügen/i })
      .first()
      .click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expectReflow(page);
    await page.keyboard.press('Escape');
    await page.goto(`/recipes/${recipeId}/cook`);
    const next = page.getByRole('button', { name: /nächster schritt/i });
    await expect(next).toBeEnabled();
    await next.scrollIntoViewIfNeeded();
    await expect(next).toBeInViewport({ ratio: 1 });
    expect((await next.boundingBox())!.height).toBeGreaterThanOrEqual(56);
    const button = (await next.boundingBox())!;
    expect(button.y + button.height).toBeLessThanOrEqual(page.viewportSize()!.height);
    await expect(page.locator('nav.bottom')).toBeHidden();
    const stop = page.locator('article.cooking > footer');
    await stop.scrollIntoViewIfNeeded();
    const stopBox = (await stop.boundingBox())!;
    const controls = (await page.locator('.controls:has(.moves)').boundingBox())!;
    expect(stopBox.y + stopBox.height).toBeLessThanOrEqual(controls.y);
    await next.click();
    await expect(page.getByText('Schritt 2 von 4', { exact: true })).toBeVisible();
    await expectReflow(page);
  });

  for (const [width, height] of [
    [844, 390],
    [667, 375]
  ] as const) {
    test(`planner picker keeps its results usable at ${width}x${height}`, async ({
      page
    }, testInfo) => {
      test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport.');
      await page.setViewportSize({ width, height });
      await responsiveData(page);
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await page.goto('/plan');
      await page.getByRole('button', { name: /^Weiter →$/i }).click();
      await page
        .getByRole('button', { name: /Hinzufügen/i })
        .first()
        .click();
      const dialog = page.getByRole('dialog');
      const results = dialog.locator('ul.results');
      // Searching swaps the empty suggestions the fixture answers with for its six recipes.
      await dialog.getByRole('searchbox').fill('brot');
      await expect(results.getByRole('button').first()).toBeVisible();

      // The body scrolls as a whole, so the list is as tall as its rows and nothing inside is clipped away.
      const list = await results.evaluate((element) => ({
        client: element.clientHeight,
        scroll: element.scrollHeight
      }));
      expect(list.client).toBeGreaterThan(100);
      expect(list.client).toBeGreaterThanOrEqual(list.scroll);

      // Every choice and the last result can be scrolled to and clicked, and the close control stays put.
      await expect(dialog.getByRole('button', { name: /schließen/i })).toBeInViewport();
      await dialog.getByRole('radio').last().scrollIntoViewIfNeeded();
      await expect(dialog.getByRole('radio').last()).toBeInViewport();
      const last = results.getByRole('button').last();
      await last.scrollIntoViewIfNeeded();
      await expect(last).toBeInViewport();
      await last.click();
      await expect(dialog).toBeHidden();
      await expectReflow(page);
    });
  }

  for (const [width, height] of [
    [320, 568],
    [844, 390]
  ] as const) {
    test(`filter dialog footer leaves the choices room at ${width}x${height}`, async ({
      page
    }, testInfo) => {
      test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport.');
      await page.setViewportSize({ width, height });
      await responsiveData(page);
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await page.goto('/');
      await page.getByRole('button', { name: /^Filtern/ }).click();
      const dialog = page.getByRole('dialog');
      const footerActions = dialog.locator('footer');
      const done = footerActions.getByRole('button', { name: 'Fertig' });
      await expect(done).toBeInViewport();

      const footer = (await footerActions.boundingBox())!;
      const panel = (await dialog.locator('.panel').first().boundingBox())!;
      const body = (await dialog.locator('.body').first().boundingBox())!;
      // The footer is a minor share of the dialog and the choices keep a few rows.
      expect(footer.height / panel.height).toBeLessThan(0.3);
      expect(body.height).toBeGreaterThanOrEqual(height >= 500 ? 280 : 150);

      // Every action keeps its name and a touch-sized target.
      for (const name of ['Filter zurücksetzen', 'Diese Suche speichern', 'Fertig']) {
        const box = (await footerActions.getByRole('button', { name }).boundingBox())!;
        expect(box.height).toBeGreaterThanOrEqual(44);
        expect(box.x + box.width).toBeLessThanOrEqual(footer.x + footer.width);
      }
      await expectReflow(page);
    });
  }

  for (const [width, height, deckLimit] of [
    [320, 568, 500],
    [390, 844, 500],
    [667, 375, 340]
  ] as const) {
    test(`the suggestion deck leaves room for the library at ${width}x${height}`, async ({
      page
    }, testInfo) => {
      test.skip(testInfo.project.name !== 'desktop', 'Explicit phone viewport.');
      await page.setViewportSize({ width, height });
      await responsiveData(page, 'de', { suggestions: 5 });
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await page.goto('/');

      const deck = page.locator('.deck');
      await expect(deck.getByRole('link', { name: /Rezept öffnen/ }).first()).toBeVisible();
      const deckBox = (await deck.boundingBox())!;
      expect(deckBox.height).toBeLessThanOrEqual(deckLimit);

      // The whole deck, controls included, stays touch-sized.
      for (const control of [
        deck.getByRole('link', { name: /Rezept öffnen/ }).first(),
        deck.getByRole('button', { name: /nicht mehr vorschlagen/ }).first(),
        deck.getByRole('button', { name: 'Nächster Vorschlag' }),
        deck.getByRole('button', { name: 'Vorheriger Vorschlag' })
      ]) {
        const box = (await control.boundingBox())!;
        expect(box.height).toBeGreaterThanOrEqual(44);
      }

      // A short landscape phone puts the photograph beside the copy rather than stacking them.
      const copy = (await deck.locator('.copy').first().boundingBox())!;
      const photo = (await deck.locator('.photo').first().boundingBox())!;
      if (width > height) {
        expect(photo.x).toBeGreaterThanOrEqual(copy.x + copy.width - 1);
      } else {
        expect(photo.y + photo.height).toBeLessThanOrEqual(copy.y + 1);
      }
      await expectReflow(page);
    });
  }

  for (const width of [320, 360, 375, 390, 430]) {
    for (const path of ['/', `/cookbooks/${cookbookId}`]) {
      test(`the filter button shares a row at ${width}px on ${path.slice(0, 10)}`, async ({
        page
      }, testInfo) => {
        test.skip(testInfo.project.name !== 'desktop', 'Explicit phone viewport.');
        await page.setViewportSize({ width, height: 800 });
        await responsiveData(page, 'de');
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.goto(path);

        const toolbar = page.locator('.toolbar');
        const search = toolbar.getByRole('searchbox');
        const filter = toolbar.getByRole('button', { name: /^Filtern/ });
        await expect(filter).toBeVisible();
        const searchBox = (await search.boundingBox())!;
        const filterBox = (await filter.boundingBox())!;

        // The filter sits beside the count rather than alone on a row, and stays touch-sized.
        const count = (await toolbar.locator('.summary').boundingBox())!;
        expect(Math.abs(count.y - filterBox.y)).toBeLessThan(filterBox.height);
        expect(filterBox.y).toBeGreaterThanOrEqual(searchBox.y + searchBox.height);
        expect(filterBox.height).toBeGreaterThanOrEqual(44);
        expect(searchBox.width).toBeGreaterThanOrEqual(width - 48);
        expect((await toolbar.boundingBox())!.height).toBeLessThan(150);
        await expectReflow(page);
      });
    }
  }

  test('a recipe starts within the readable part of a phone screen', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'Explicit phone viewport.');
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(`/recipes/${recipeId}`);

    const title = page.getByRole('heading', { level: 1 });
    const servings = page.locator('article.surface > .servings');
    const resume = page.getByRole('link', { name: /Gerade am Kochen/ });
    await expect(title).toBeVisible();
    await expect(resume).toBeVisible();
    await expect(page.getByRole('button', { name: 'Kochen starten' })).toHaveCount(0);
    // The shell header gives way to the recipe's own sticky back bar.
    await expect(page.locator('.shell > header.header')).toBeHidden();

    // The photo is big on purpose (4:3, at most 18rem); the title and servings must still clear the bar.
    const hero = (await page.locator('article.surface > .hero').boundingBox())!;
    expect(hero.height).toBeLessThanOrEqual(288);
    const resumeBox = (await resume.boundingBox())!;
    const titleBox = (await title.boundingBox())!;
    expect(titleBox.y + titleBox.height).toBeLessThan(resumeBox.y);
    const servingsBox = (await servings.boundingBox())!;
    expect(servingsBox.y + servingsBox.height).toBeLessThan(resumeBox.y);
    await expectReflow(page);
    await page.screenshot({ path: testInfo.outputPath('recipe-reading-390.png') });
  });

  for (const locale of ['de', 'en'] as const) {
    test(`${locale} cooking actions stay compact across phone and tablet widths`, async ({
      page
    }, testInfo) => {
      test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport and text-size matrix.');
      await responsiveData(page, locale, { longSteps: true });
      await page.emulateMedia({ reducedMotion: 'reduce' });
      for (const [width, fontSize] of [
        [320, 16],
        [390, 16],
        [639, 16],
        [640, 16],
        [768, 16],
        [1024, 16],
        [1280, 16],
        [768, 24]
      ] as const) {
        await page.setViewportSize({ width, height: 1000 });
        await page.goto(`/recipes/${recipeId}/cook`);
        await page.evaluate((size) => {
          document.documentElement.style.fontSize = `${size}px`;
        }, fontSize);
        const controls = page.locator('.controls:has(.moves)');
        const next = controls.getByRole('button', { name: /nächster schritt|next step/i });
        await expect(next).toBeEnabled();
        await next.scrollIntoViewIfNeeded();
        await expect(next).toBeInViewport({ ratio: 1 });
        const box = (await next.boundingBox())!;
        expect(box.width).toBeGreaterThanOrEqual(100);
        expect(box.height).toBeLessThanOrEqual(fontSize * 5);
        expect((await controls.boundingBox())!.height).toBeLessThanOrEqual(fontSize * 13);
        // Reflow checks cannot catch a word broken into a tall stack of letters.
        const words = await next.locator('.advance-label').evaluate((element) => {
          const text = element.firstChild!;
          return [...text.textContent!.matchAll(/\S+/g)].map((word) => {
            const range = document.createRange();
            range.setStart(text, word.index!);
            range.setEnd(text, word.index! + word[0].length);
            return range.getClientRects().length;
          });
        });
        expect(words.every((lines) => lines === 1)).toBe(true);
        await expectReflow(page);
        if (locale === 'de' && [390, 640].includes(width)) {
          await page.screenshot({ path: testInfo.outputPath(`cooking-actions-${width}.png`) });
        }
        await next.click();
        await expect(
          page.getByText(locale === 'de' ? 'Schritt 2 von 4' : 'Step 2 of 4', { exact: true })
        ).toBeVisible();
        await controls.getByRole('button', { name: /vorheriger schritt|previous step/i }).click();
        await expect(
          page.getByText(locale === 'de' ? 'Schritt 1 von 4' : 'Step 1 of 4', { exact: true })
        ).toBeVisible();
      }
    });
  }

  test('mobile cooking hides app navigation and returns to the recipe without losing its place', async ({
    page
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport transitions.');
    await responsiveData(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.setViewportSize({ width: 390, height: 844 });
    const ended: string[] = [];
    page.on('request', (request) => {
      if (request.method() === 'DELETE' && /cook-sessions|timers/.test(request.url())) {
        ended.push(request.url());
      }
    });
    await page.goto(`/recipes/${recipeId}/cook?yield=4`);
    await page.getByRole('button', { name: /nächster schritt/i }).click();
    await expect(page.getByText('Schritt 2 von 4', { exact: true })).toBeVisible();

    for (const width of [390, 1023, 1024, 390]) {
      await page.setViewportSize({ width, height: 844 });
      const header = page.locator('.shell > header.header');
      if (width < 1024) {
        await expect(header).toBeHidden();
        await expect(page.locator('nav.nav:visible')).toHaveCount(0);
        await expect
          .poll(() =>
            page.locator('.shell').evaluate((element) => {
              const style = getComputedStyle(element);
              return [
                style.getPropertyValue('--header-inset'),
                style.getPropertyValue('--bar-inset')
              ];
            })
          )
          .toEqual(['0px', '0px']);
      } else {
        await expect(header).toBeVisible();
        await expect(page.locator('nav.top')).toBeVisible();
      }
      await expectReflow(page);
    }

    const back = page.getByRole('link', { name: 'Zurück zum Rezept' });
    await back.scrollIntoViewIfNeeded();
    await expect(back).toBeInViewport({ ratio: 1 });
    expect((await back.boundingBox())!.height).toBeGreaterThanOrEqual(44);
    await back.click();
    await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}\\?yield=4$`));
    // The recipe page sheds the shell header on a phone; its own back bar takes over.
    await expect(page.locator('.shell > header.header')).toBeHidden();
    await expect(page.locator('nav.bottom')).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.getByRole('link', { name: /weiterkochen/i }).click();
    await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}/cook\\?yield=4$`));
    await expect(page.getByText('Schritt 2 von 4', { exact: true })).toBeVisible();
    await expect(page.locator('.shell > header.header')).toBeHidden();
    expect(ended).toEqual([]);
  });

  for (const [width, height] of [
    [320, 568],
    [390, 844],
    [844, 390],
    [1024, 900],
    [1280, 900]
  ] as const) {
    test(`start cooking stays visible and settles after the recipe at ${width}px`, async ({
      page
    }, testInfo) => {
      test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport matrix.');
      await expectStartCookingReachable(page, width, height);
    });
  }

  test('mobile cooking action is visible on arrival and settles while scrolling', async ({
    page
  }) => {
    const viewport = page.viewportSize()!;
    await expectStartCookingReachable(page, viewport.width, viewport.height);
  });

  for (const [width, height] of [
    [320, 568],
    [375, 812],
    [390, 844],
    [430, 932]
  ] as const) {
    test(`the step being cooked is readable above the controls at ${width}px`, async ({
      page
    }, testInfo) => {
      test.skip(testInfo.project.name !== 'desktop', 'Explicit viewport matrix.');
      await page.setViewportSize({ width, height });
      await responsiveData(page);
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await page.goto(`/recipes/${recipeId}/cook`);
      const next = page.getByRole('button', { name: /nächster schritt/i });
      await expect(next).toBeEnabled();

      await expectCurrentStepReadable(page);
      for (let step = 2; step <= 4; step++) {
        await next.click();
        await expect(page.getByText(`Schritt ${step} von 4`, { exact: true })).toBeVisible();
        await expectCurrentStepReadable(page);
      }
      await page.setViewportSize({ width: height, height: width });
      await expectCurrentStepReadable(page);
      await page.screenshot({ path: testInfo.outputPath(`cook-${width}.png`) });
    });
  }

  test('short landscape keeps forms, dialogs and cooking reachable', async ({ page }) => {
    await page.setViewportSize({ width: 844, height: 390 });
    await responsiveData(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    for (const path of ['/login', '/register', '/shopping', `/recipes/${recipeId}/cook`]) {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await expectReflow(page);
    }
    const next = page.getByRole('button', { name: /nächster schritt/i });
    await next.scrollIntoViewIfNeeded();
    await expect(next).toBeInViewport({ ratio: 1 });
    await page.goto('/plan');
    await page
      .getByRole('button', { name: /Hinzufügen/i })
      .first()
      .click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    const box = (await dialog.boundingBox())!;
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.y + box.height).toBeLessThanOrEqual(390);
    await expect(dialog.getByRole('button', { name: /schließen/i })).toBeInViewport({ ratio: 1 });
    await expectReflow(page);
  });
});

/**
 * The current step starts clear of the header and ends clear of the controls and bottom navigation. Polled, and measured only after two frames:
 * a resize is answered on the next frame, and scrolling before the page's own rescue lands fights it on slow runners.
 */
async function expectCurrentStepReadable(page: Page) {
  await page.evaluate(
    () => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done)))
  );

  const measure = () =>
    page.evaluate(() => {
      const top = (selector: string) => {
        const box = document.querySelector(selector)?.getBoundingClientRect();
        return box && box.height > 0 ? box.top : innerHeight;
      };
      const step = document.querySelector('.step.current')!.getBoundingClientRect();
      return {
        top: step.top,
        bottom: step.bottom,
        // A header scrolled off the top clears nothing below the screen's edge.
        clearTop: Math.max(
          0,
          document.querySelector('header.header')!.getBoundingClientRect().bottom
        ),
        clearBottom: Math.min(top('.controls:has(.moves)'), top('nav.bottom'), innerHeight)
      };
    });

  await expect
    .poll(async () => {
      const at = await measure();
      return at.top >= at.clearTop && at.top < at.clearBottom ? 'readable' : JSON.stringify(at);
    })
    .toBe('readable');

  const at = await measure();
  await page.evaluate((by) => scrollBy(0, by), Math.max(0, at.bottom - at.clearBottom));
  const scrolled = await measure();
  expect(scrolled.bottom).toBeLessThanOrEqual(scrolled.clearBottom + 1);
}

async function expectStartCookingReachable(page: Page, width: number, height: number) {
  await page.setViewportSize({ width, height });
  await responsiveData(page, 'de', { activeCooking: false });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/recipes/${recipeId}`);

  const start = page.getByRole('button', { name: 'Kochen starten', exact: true });
  const action = page.locator('article.surface > .cook-action > footer.foot');
  await expect(start).toBeVisible();
  await expect(start).toBeInViewport({ ratio: 1 });
  expect(await action.evaluate((element) => getComputedStyle(element).position)).toBe(
    width < 1024 ? 'fixed' : 'relative'
  );

  const actionBox = (await action.boundingBox())!;
  const bottom = width < 1024 ? (await page.locator('nav.bottom').boundingBox())!.y : height;
  expect(actionBox.y + actionBox.height).toBeLessThanOrEqual(bottom);

  if (width < 1024) {
    await page.setViewportSize({ width, height: height - 100 });
    await expect(start).toBeInViewport({ ratio: 1 });
    await expect
      .poll(async () => {
        const button = (await action.boundingBox())!;
        return button.y + button.height - (await page.locator('nav.bottom').boundingBox())!.y;
      })
      .toBeLessThanOrEqual(1);
    await page.setViewportSize({ width, height });
    await expect.poll(async () => (await action.boundingBox())!.y).toBeCloseTo(actionBox.y, 0);
  }

  await page.evaluate(() => window.scrollTo(0, 150));
  await expect(start).toBeInViewport({ ratio: 1 });
  expect((await action.boundingBox())!.y).toBeCloseTo(actionBox.y, 0);

  await page.locator('article.surface > .body').evaluate((element, dockedTop) => {
    const gap = parseFloat(getComputedStyle(element.parentElement!).rowGap);
    window.scrollTo(
      0,
      window.scrollY + element.getBoundingClientRect().bottom + gap - (dockedTop - 80)
    );
  }, actionBox.y);
  await expect.poll(async () => (await action.boundingBox())!.y).toBeLessThan(actionBox.y);
  const restingBox = (await action.boundingBox())!;
  await page.evaluate(() => window.scrollBy(0, 60));
  await expect.poll(async () => (await action.boundingBox())!.y).toBeCloseTo(restingBox.y - 60, 0);

  await page.evaluate(() => window.scrollTo(0, 0));
  await expect(start).toBeInViewport({ ratio: 1 });
  expect((await action.boundingBox())!.y).toBeCloseTo(actionBox.y, 0);
  await expectReflow(page);
  await start.click();
  await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}/cook`));
}

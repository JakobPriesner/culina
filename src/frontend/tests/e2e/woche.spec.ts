import { expect, test, type Page } from '@playwright/test';
import { expectReflow, expectWholeWords, responsiveData } from './support/responsive';

/**
 * Woche holds the week's plan and the household's list (docs/woche-design.md), rendered with the layout fixtures:
 * the four tabs, the Plan | Liste switch on phones, and the list beside the week from 80rem.
 */

/** A person who keeps a diary, with nothing logged; after `responsiveData`, whose catch-all it overrides. */
async function diaryOn(page: Page) {
  await page.route('**/api/v1/users/me/settings/diary', (route) =>
    route.fulfill({
      json: {
        enabled: true,
        currentConsentVersion: 1,
        consentVersion: 1,
        headline: 'eaten',
        version: 1
      }
    })
  );
  await page.route('**/api/v1/users/me/diary?**', (route) => {
    const url = new URL(route.request().url());
    const date = url.searchParams.get('to') ?? url.searchParams.get('from')!;

    return route.fulfill({
      json: {
        days: [{ date, entries: [], totals: {}, atLeast: false, estimated: false, typedKcal: 0 }]
      }
    });
  });
}

const isoDay = (day: Date) =>
  `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

/**
 * A week of two dinners, Tuesday's Linsen-Dal (`e-1`, its line Paprika) and Thursday's Ofengemüse (`e-2`, its line
 * Feta), and the requests that put them on the list, take them off it and unplan them; after `responsiveData`.
 * `listed` starts with Linsen-Dal's shopping done. Returns what was asked of the list.
 */
async function weekToShop(page: Page, { listed = false } = {}) {
  await wholeWeek(page);
  const asked: unknown[] = [];
  const onList = new Set<string>(listed ? ['e-1'] : []);
  const unplanned = new Set<string>();
  const monday = (() => {
    const now = new Date();
    now.setDate(now.getDate() - ((now.getDay() + 6) % 7));
    return isoDay(now);
  })();
  const dayOf = (index: number) => {
    const day = new Date(`${monday}T12:00:00`);
    day.setDate(day.getDate() + index);
    return isoDay(day);
  };
  const tuesday = dayOf(1);
  const meals = [
    { entryId: 'e-1', title: 'Linsen-Dal', day: 1, line: 'Paprika', section: 'produce' },
    { entryId: 'e-2', title: 'Ofengemüse', day: 3, line: 'Feta', section: 'dairy_eggs' }
  ];
  const list = () => ({
    listId: 'list-1',
    version: 1 + onList.size,
    items: meals
      .filter((meal) => onList.has(meal.entryId))
      .map((meal) => ({
        itemId: `item-${meal.entryId}`,
        name: meal.line,
        quantity: 3,
        unit: null,
        section: meal.section,
        isChecked: false,
        isManual: false,
        sources: [
          {
            recipeId: '00000000-0000-4000-8000-000000000001',
            recipeTitle: meal.title,
            quantity: 3,
            unit: null,
            planEntryId: meal.entryId,
            plannedDate: dayOf(meal.day),
            plannedSlot: 'dinner'
          }
        ]
      }))
  });
  const week = (from: string) => ({
    from,
    days: Array.from({ length: 7 }, (_, i) => {
      const day = new Date(`${from}T12:00:00`);
      day.setDate(day.getDate() + i);
      return {
        date: isoDay(day),
        meals:
          from !== monday
            ? []
            : meals
                .filter((meal) => meal.day === i && !unplanned.has(meal.entryId))
                .map((meal) => ({
                  entryId: meal.entryId,
                  recipeId: '00000000-0000-4000-8000-000000000001',
                  title: meal.title,
                  totalMinutes: 30,
                  recipeServings: 2,
                  servings: 2,
                  slot: 'dinner',
                  isOnShoppingList: onList.has(meal.entryId)
                }))
      };
    })
  });

  await page.route('**/api/v1/households/*/meal-plan?**', (route) =>
    route.fulfill({ json: week(new URL(route.request().url()).searchParams.get('from') ?? monday) })
  );
  await page.route('**/api/v1/households/*/meal-plan/*', (route) => {
    unplanned.add(route.request().url().split('/').pop()!);
    return route.fulfill({ json: week(monday) });
  });
  await page.route('**/api/v1/households/*/shopping-list', (route) =>
    route.fulfill({ json: list() })
  );
  await page.route('**/api/v1/households/*/shopping-list/meals', (route) => {
    const body = route.request().postDataJSON() as { entryId?: string };
    asked.push(body);
    for (const meal of meals)
      if (!body.entryId || body.entryId === meal.entryId) onList.add(meal.entryId);
    return route.fulfill({ json: list() });
  });
  await page.route('**/api/v1/households/*/shopping-list/meals/*', (route) => {
    asked.push({ withdrawn: route.request().url().split('/').pop() });
    onList.delete(route.request().url().split('/').pop()!);
    return route.fulfill({ json: list() });
  });

  return { asked, tuesday };
}

/** Nothing planned in any week, and a list that is empty or holds one line of its own; after `responsiveData`. */
async function emptyWeek(page: Page, { listInUse = false } = {}) {
  await page.route('**/api/v1/households/*/meal-plan?**', (route) => {
    const from = new URL(route.request().url()).searchParams.get('from')!;
    return route.fulfill({
      json: {
        from,
        days: Array.from({ length: 7 }, (_, i) => {
          const day = new Date(`${from}T12:00:00`);
          day.setDate(day.getDate() + i);
          return { date: isoDay(day), meals: [] };
        })
      }
    });
  });
  await page.route('**/api/v1/households/*/shopping-list', (route) =>
    route.fulfill({
      json: {
        listId: 'list-1',
        version: 1,
        items: listInUse
          ? [
              {
                itemId: 'milk',
                name: 'Milch',
                quantity: 1,
                unit: 'l',
                section: 'dairy_eggs',
                isChecked: false,
                isManual: true,
                sources: []
              }
            ]
          : []
      }
    })
  );
}

async function settled(page: Page) {
  await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
}

/** The plan as the whole week, its days gone unfolded: the fixtures' meals are on fixed weekdays, whatever today is. */
async function wholeWeek(page: Page) {
  await page.addInitScript(() => localStorage.setItem('culina.plan.view', 'week'));
}

async function unfolded(page: Page) {
  await settled(page);
  const earlier = page.getByRole('button', { name: /Früher diese Woche|Earlier this week/ });
  if ((await earlier.count()) > 0) await earlier.click();
}

test.describe('Woche @offline', () => {
  test.use({ serviceWorkers: 'block' });

  for (const width of [320, 390]) {
    for (const locale of ['de', 'en'] as const) {
      test(`the bar has four whole tabs and Woche holds the plan and the list at ${width}px (${locale})`, async ({
        page
      }) => {
        await page.setViewportSize({ width, height: 844 });
        await responsiveData(page, locale, { activeCooking: false });
        await diaryOn(page);
        await page.goto('/');
        await settled(page);

        const nav = page.locator('nav.bottom');
        const links = nav.getByRole('link');
        await expect(links).toHaveCount(4);
        await expect(
          nav.getByRole('link', { name: locale === 'de' ? 'Ernährung' : 'Nutrition' })
        ).toBeVisible();
        await expectWholeWords(links);
        await page.screenshot({ path: test.info().outputPath(`home-${locale}-${width}.png`) });

        await links.filter({ hasText: locale === 'de' ? 'Woche' : 'Week' }).click();
        await expect(page).toHaveURL(/\/week\/plan/);
        await settled(page);
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
        await page.screenshot({ path: test.info().outputPath(`plan-${locale}-${width}.png`) });

        // The switch says what is still to find (the fixture's one line), and leads to the list; the Woche tab then
        // returns there.
        const listSegment = page
          .getByRole('group', { name: locale === 'de' ? 'Woche' : 'Week' })
          .getByRole('button', { name: locale === 'de' ? 'Einkaufsliste' : 'Shopping list' });
        await expect(listSegment).toHaveText(
          locale === 'de' ? 'Einkaufsliste · 1' : 'Shopping list · 1'
        );
        await expectWholeWords(listSegment);
        await listSegment.click();
        await expect(page).toHaveURL(/\/week\/shopping/);
        await settled(page);
        await expect(nav.locator('[aria-current="page"]')).toHaveText(
          locale === 'de' ? 'Woche' : 'Week'
        );
        await page.screenshot({ path: test.info().outputPath(`list-${locale}-${width}.png`) });

        await links.filter({ hasText: locale === 'de' ? 'Rezepte' : 'Recipes' }).click();
        await links.filter({ hasText: locale === 'de' ? 'Woche' : 'Week' }).click();
        await expect(page).toHaveURL(/\/week\/shopping/);
        await expectReflow(page);
      });
    }
  }

  for (const width of [1280, 1440]) {
    test(`the list sits beside the week at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await responsiveData(page, 'de', { activeCooking: false });
      await diaryOn(page);
      await page.goto('/week/plan');
      await settled(page);
      const pane = page.getByRole('complementary', { name: 'Einkauf' });
      const paneHeading = pane.getByRole('heading', {
        level: 2,
        name: 'Einkaufsliste',
        exact: true
      });
      await expect(paneHeading).toBeVisible();
      // One place: the pane's heading is the page heading's size, on its line, never larger.
      const size = (heading: typeof paneHeading) =>
        heading.evaluate((element) => Number.parseFloat(getComputedStyle(element).fontSize));
      const pageHeading = page.getByRole('heading', { level: 1 });
      expect(await size(paneHeading)).toBeLessThanOrEqual(await size(pageHeading));
      const top = async (heading: typeof paneHeading) => (await heading.boundingBox())!.y;
      expect(Math.abs((await top(paneHeading)) - (await top(pageHeading)))).toBeLessThanOrEqual(4);
      // Beside the pane the week keeps columns wide enough for whole day names.
      await expectWholeWords(page.locator('ol.week h2.name'));
      await page.screenshot({
        path: test.info().outputPath(`plan-wide-${width}.png`),
        fullPage: true
      });
    });
  }

  test('what the week still needs is at the top of the pane, and its lines arrive there', async ({
    page
  }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    const { asked } = await weekToShop(page);
    await page.goto('/week/plan');
    await unfolded(page);

    const pane = page.getByRole('complementary', { name: 'Einkauf' });
    const seam = pane.getByText('Diese Woche: 2 Mahlzeiten noch nicht auf der Einkaufsliste');
    await expect(seam).toBeInViewport();
    // The week's own bar is not drawn beside the pane: the seam does its job, where the lines arrive.
    await expect(
      page.getByRole('button', { name: '2 Mahlzeiten auf die Einkaufsliste' })
    ).toHaveCount(0);

    await pane.getByRole('button', { name: 'Auf die Einkaufsliste' }).click();
    await expect(pane.locator('li.row.arrived', { hasText: 'Paprika' })).toBeVisible();
    await expect(seam).toHaveCount(0);
    expect(asked).toHaveLength(1);
    // The list is on screen: no toast offers to open it.
    await expect(page.getByRole('button', { name: 'Liste öffnen' })).toHaveCount(0);
    await page.screenshot({ path: test.info().outputPath('seam-1280.png') });

    // Next week has nothing planned, so nothing is missing.
    await page.getByRole('button', { name: 'Eine Woche weiter', exact: true }).click();
    await expect(pane.getByText(/noch nicht auf der Einkaufsliste/)).toHaveCount(0);
  });

  test('beside the week, a meal leads to its lines and a line back to its day', async ({
    page
  }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    const { tuesday } = await weekToShop(page, { listed: true });
    await page.goto('/week/plan');
    await unfolded(page);
    const pane = page.getByRole('complementary', { name: 'Einkauf' });

    await page
      .locator('[data-plan-day]', { hasText: 'Linsen-Dal' })
      .getByRole('button', { name: 'Auf der Einkaufsliste' })
      .click();
    await expect(pane.locator('li.row.marked', { hasText: 'Paprika' })).toBeInViewport();

    // The pane does not repeat the week beside it: nothing else is on this list.
    await expect(pane.getByText(/Außerdem auf der Liste|Mahlzeiten und Rezepte/)).toHaveCount(0);

    await pane.getByRole('button', { name: 'Aktionen für Paprika' }).click();
    await page.getByRole('button', { name: '1 Rezept', exact: true }).click();
    await pane.getByRole('link', { name: /Abendessen/ }).click();
    await expect(page).toHaveURL(new RegExp(`tag=${tuesday}`));
    await expect(page.locator(`[data-plan-day="${tuesday}"]`)).toHaveClass(/sought/);
  });

  test('pointing at a meal lights its lines, and a line its meal', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'A mouse and its hover.');
    await page.setViewportSize({ width: 1280, height: 800 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    await weekToShop(page, { listed: true });
    await page.goto('/week/plan');
    await unfolded(page);
    const card = page.locator('[data-plan-day] .card', { hasText: 'Linsen-Dal' });
    const line = page
      .getByRole('complementary', { name: 'Einkauf' })
      .locator('li.row', { hasText: 'Paprika' });

    await card.hover();
    await expect(line).toHaveClass(/related/);
    await page.mouse.move(5, 5);
    await expect(line).not.toHaveClass(/related/);

    await line.hover();
    await expect(card).toHaveClass(/related/);
    await page.mouse.move(5, 5);
    await expect(card).not.toHaveClass(/related/);

    // The keyboard gets the same: focus in the card lights its lines.
    await card.getByRole('link', { name: 'Linsen-Dal' }).focus();
    await expect(line).toHaveClass(/related/);
  });

  test('beside the list, unplanning a listed meal asks at its lines, not in a toast', async ({
    page
  }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    const { asked } = await weekToShop(page, { listed: true });
    await page.goto('/week/plan');
    await unfolded(page);
    const pane = page.getByRole('complementary', { name: 'Einkauf' });

    // A mouse finds the cross by pointing at the card; a finger has it drawn.
    await page.locator('[data-plan-day] .card', { hasText: 'Linsen-Dal' }).hover();
    await page.getByRole('button', { name: 'Linsen-Dal vom Plan nehmen' }).click();
    await expect(pane.getByText('Linsen-Dal ist nicht mehr geplant.')).toBeVisible();
    await expect(pane.locator('li.row.marked', { hasText: 'Paprika' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Von der Liste nehmen' })).toHaveCount(0);

    await pane.getByRole('button', { name: '1 Zeile entfernen' }).click();
    await expect(pane.locator('li.row', { hasText: 'Paprika' })).toHaveCount(0);
    await expect(pane.getByText('Linsen-Dal ist nicht mehr geplant.')).toHaveCount(0);
    expect(asked).toContainEqual({ withdrawn: 'e-1' });
  });

  test('beside the list a meal goes on it from its card, or dropped onto it', async ({
    page
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'A mouse drag.');
    await page.setViewportSize({ width: 1280, height: 800 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    const { asked } = await weekToShop(page);
    await page.goto('/week/plan');
    await unfolded(page);
    const pane = page.getByRole('complementary', { name: 'Einkauf' });

    await page.getByRole('button', { name: 'Ofengemüse auf die Einkaufsliste' }).click();
    await expect(pane.locator('li.row', { hasText: 'Feta' })).toBeVisible();
    expect(asked).toContainEqual(expect.objectContaining({ entryId: 'e-2' }));

    // Carried onto the list, Linsen-Dal stays on Tuesday and its shopping goes on.
    const card = page.locator('[data-plan-day] .card', { hasText: 'Linsen-Dal' });
    const from = (await card.boundingBox())!;
    const to = (await pane.boundingBox())!;
    await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
    await page.mouse.down();
    await page.mouse.move(from.x + from.width / 2 + 40, from.y + from.height / 2 + 40, {
      steps: 4
    });
    await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 8 });
    await expect(pane).toHaveClass(/over/);
    await page.mouse.up();
    await expect(pane.locator('li.row', { hasText: 'Paprika' })).toBeVisible();
    expect(asked).toContainEqual(expect.objectContaining({ entryId: 'e-1' }));
    await expect(page.locator('[data-plan-day]', { hasText: 'Linsen-Dal' })).toHaveCount(1);
  });

  test('beside the list the week is rows that fit a laptop screen, the days gone folded', async ({
    page
  }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    await weekToShop(page, { listed: true });
    await page.goto('/week/plan');
    await settled(page);

    // As it opens: today first, the days before it one line, and the rest of the week on the first screen.
    const days = page.locator('[data-plan-day]');
    await expect(days.last()).toBeInViewport({ ratio: 1 });
    await expect(
      page.getByRole('complementary', { name: 'Einkauf' }).locator('li.row', { hasText: 'Paprika' })
    ).toBeInViewport();
    await page.screenshot({ path: test.info().outputPath('rows-1280.png') });

    await unfolded(page);
    await expect(days).toHaveCount(7);
    await expect(page.locator('[data-plan-row]')).toHaveCount(7);
    await expectWholeWords(page.locator('[data-plan-day] h2'));
  });

  test('an empty week and an empty list say so once, for the place', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    await emptyWeek(page);
    await page.goto('/week/plan');
    await unfolded(page);

    await expect(page.getByRole('heading', { name: 'Noch nichts geplant' })).toBeVisible();
    const pane = page.getByRole('complementary', { name: 'Einkauf' });
    await expect(pane.getByRole('heading', { name: 'Deine Einkaufsliste ist leer' })).toHaveCount(
      0
    );
    await expect(pane.getByPlaceholder('z. B. 200 g Butter')).toBeVisible();
    await page.screenshot({ path: test.info().outputPath('empty-1280.png') });

    await page.getByRole('button', { name: 'Rezept planen' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
  });

  test('an empty week beside a list in use is a quiet line', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    await emptyWeek(page, { listInUse: true });
    await page.goto('/week/plan');
    await unfolded(page);

    await expect(page.getByText('Noch nichts geplant')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Noch nichts geplant' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Rezept planen' })).toBeVisible();
  });

  test('the week loads in the shape it arrives in, beside the list too', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    let answer: () => void = () => {};
    const held = new Promise<void>((resolve) => (answer = resolve));
    await page.route('**/api/v1/households/*/meal-plan?**', async (route) => {
      await held;
      await route.fallback();
    });
    await page.goto('/week/plan');
    const skeleton = page.locator('ol.week[aria-busy="true"]');
    await expect(skeleton).toHaveClass(/rows/);
    await expect(skeleton.locator('li.row')).toHaveCount(7);
    answer();
    await unfolded(page);
    await expect(page.locator('[data-plan-row]')).toHaveCount(7);
  });

  test('on a phone Plan and Liste each come back where they were left', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 700 });
    await responsiveData(page, 'de', { activeCooking: false, longShoppingLine: true });
    await diaryOn(page);
    await wholeWeek(page);
    await page.goto('/week/plan');
    await unfolded(page);
    const switchTo = (name: string) =>
      page.getByRole('group', { name: 'Woche' }).getByRole('button', { name }).click();

    await page.evaluate(() => window.scrollTo({ top: 500 }));
    await expect.poll(() => page.evaluate(() => Math.round(window.scrollY))).toBe(500);
    await switchTo('Einkaufsliste');
    await expect(page).toHaveURL(/\/week\/shopping$/);
    // A first visit to the list starts at its top.
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
    await page.evaluate(() => window.scrollTo({ top: 120 }));
    await expect.poll(() => page.evaluate(() => Math.round(window.scrollY))).toBe(120);

    await switchTo('Essensplan');
    await expect(page).toHaveURL(/\/week\/plan$/);
    await settled(page);
    await expect.poll(() => page.evaluate(() => Math.round(window.scrollY))).toBe(500);

    await switchTo('Einkaufsliste');
    await expect.poll(() => page.evaluate(() => Math.round(window.scrollY))).toBe(120);
  });

  test('on a phone a meal on the list opens the list at its lines', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    await weekToShop(page, { listed: true });
    await page.goto('/week/plan');
    await unfolded(page);
    await page
      .locator('[data-plan-day]', { hasText: 'Linsen-Dal' })
      .getByRole('link', { name: 'Auf der Einkaufsliste' })
      .click();
    await expect(page).toHaveURL(/\/week\/shopping\?mahlzeit=e-1$/);
    await expect(page.locator('li.row.marked', { hasText: 'Paprika' })).toBeVisible();
    await page.goBack();
    await expect(page).toHaveURL(/\/week\/plan$/);
  });

  test('on a phone the list says what this week still needs', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    await weekToShop(page);
    await page.goto('/week/shopping');
    await settled(page);
    await expect(
      page.getByText('Diese Woche: 2 Mahlzeiten noch nicht auf der Einkaufsliste')
    ).toBeVisible();
    await expectReflow(page);
  });

  test('between 64 and 80rem the switch stays and the week keeps the width', async ({ page }) => {
    await page.setViewportSize({ width: 1100, height: 900 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    await page.goto('/week/plan');
    await unfolded(page);
    await expect(page.getByRole('group', { name: 'Woche' })).toBeVisible();
    await expect(page.getByRole('complementary', { name: 'Einkauf' })).toHaveCount(0);
    await page.screenshot({ path: test.info().outputPath('plan-1100.png') });
  });

  test('on a wide screen the header keeps one row', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryOn(page);
    await page.goto('/');
    await settled(page);
    const header = (await page.locator('header .header-inner').boundingBox())!;
    expect(header.height).toBeLessThan(120);
    await page.screenshot({ path: test.info().outputPath('home-wide.png') });
  });

  for (const [width, height, firstDayBelow] of [
    [320, 700, 430],
    [390, 844, 400],
    [720, 540, 340],
    [844, 390, 330]
  ] as const) {
    test(`the plan's first day starts early at ${width}x${height}`, async ({ page }) => {
      await page.setViewportSize({ width, height });
      await responsiveData(page, 'de', { activeCooking: false });
      await diaryOn(page);
      await page.goto('/week/plan');
      await settled(page);
      await page.getByRole('button', { name: 'Alles klar' }).click();
      await page.evaluate(() => window.scrollTo(0, 0));

      const first = page.locator('[data-plan-day]').first();
      expect((await first.boundingBox())!.y).toBeLessThan(firstDayBelow);

      // Back and forward stay with the dates they move, at 390; the header bar keeps Plan | Liste.
      if (width === 390) {
        const dates = (await page
          .getByRole('button', { name: /Zu einem Datum springen/ })
          .boundingBox())!;
        const next = (await page.getByRole('button', { name: 'Einen Tag weiter' }).boundingBox())!;
        expect(Math.abs(dates.y - next.y)).toBeLessThan(dates.height);
      }
    });
  }
});

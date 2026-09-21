import { devices, expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/**
 * A week of what this household means to cook.
 *
 * Seven days and deliberately not a calendar: a week is the unit people plan
 * in, because they shop at the weekend for the week that follows. Its whole
 * payoff is the last step — the plan writes the shopping list, through exactly
 * the same merge a single recipe goes through.
 */
test.describe.configure({ mode: 'serial' });

test.describe('planning a week', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;
  let who: { email: string; password: string };
  let title: string;
  let ingredient: string;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    who = await accountFor(browser, testInfo);

    await signInWithHousehold(page, who);

    title = unique('Planned');
    // Named uniquely, because these suites share one instance: asserting on
    // "200 g" would be asserting on whatever the browser next door is doing.
    ingredient = unique('Butter');

    await seedRecipe(page, {
      title,
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: ingredient }],
      steps: ['Melt {0}.']
    });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('puts a recipe on a day, and the week writes the shopping list', async () => {
    // Through the navigation, which is where the week lives now. The library
    // used to carry a link of its own and does not since the shortlist took
    // the top of that page.
    await page.goto('/');
    await page.getByRole('link', { name: /^(week|woche)$/i }).click();

    await expect(page).toHaveURL(/\/plan$/);

    // Seven days, planned or not: a week with holes in it is a week the screen
    // has to fill in itself.
    await expect(
      page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2 }) })
    ).toHaveCount(7);

    await page
      .getByRole('button', { name: /^\+ (add|hinzufügen)$/i })
      .first()
      .click();

    const sheet = page.getByRole('dialog');

    await expect(sheet).toBeVisible();
    await sheet.getByRole('searchbox').fill(title);
    await sheet.getByRole('button', { name: title }).click();

    await expect(sheet).toBeHidden();
    await expect(page.getByRole('link', { name: title })).toBeVisible();

    // The payoff. It goes through the same call a single recipe does, so the
    // merging that makes a list worth having cannot be subtly different here.
    await page.getByRole('button', { name: /add the week|woche auf die/i }).click();
    await expect(page.getByRole('status').getByText(/shopping list|einkaufsliste/i)).toBeVisible();

    await page.goto('/shopping');
    await expect(page.getByText(ingredient)).toBeVisible();
  });

  test('moves a meal to another day by dragging it there', async () => {
    await page.goto('/plan');

    await expect(page.getByRole('link', { name: title })).toBeVisible();

    const onto = await dayOtherThan(page, await dayHolding(page, title));

    await dragCardOnto(page, title, onto);

    // The card itself, on the day it was let go over. Asserting on a toast
    // would be asserting that something was said, not that anything moved.
    await expect.poll(() => dayHolding(page, title)).toBe(onto);

    // And it survives the round trip, rather than only the optimistic draw.
    await page.reload();
    await expect.poll(() => dayHolding(page, title)).toBe(onto);
  });

  test('puts it back when the move is undone', async () => {
    await page.goto('/plan');

    await expect(page.getByRole('link', { name: title })).toBeVisible();

    const from = await dayHolding(page, title);
    const onto = await dayOtherThan(page, from);

    await dragCardOnto(page, title, onto);
    await expect.poll(() => dayHolding(page, title)).toBe(onto);

    await page.getByRole('button', { name: /^(undo|rückgängig)$/i }).click();

    // All the way back, including after a reload: an undo that only redraws is
    // an undo that lies.
    await expect.poll(() => dayHolding(page, title)).toBe(from);

    await page.reload();
    await expect.poll(() => dayHolding(page, title)).toBe(from);
  });

  test('moves a meal without a pointer, through the grip', async () => {
    await page.goto('/plan');

    await expect(page.getByRole('link', { name: title })).toBeVisible();

    const onto = await dayOtherThan(page, await dayHolding(page, title));
    const which = await indexOfDay(page, onto);

    // The grip is a real button, so a keyboard reaches it and a screen reader
    // announces it. It is the only path either of them has, and it is the same
    // move the drag makes.
    await page
      .getByRole('button', { name: new RegExp(`${title}.*(another day|anderen Tag)`, 'i') })
      .click();

    const sheet = page.getByRole('dialog');

    await expect(sheet).toBeVisible();

    await sheet.getByRole('radio').nth(which).check();
    await sheet.getByRole('button', { name: /^(move|verschieben)$/i }).click();

    await expect(sheet).toBeHidden();
    await expect.poll(() => dayHolding(page, title)).toBe(onto);
  });

  test('waits for a held finger before it carries anything', async ({ browser }) => {
    // The gesture the whole design turns on, and the only one `page.mouse`
    // cannot tell you about. A phone has no hover and no spare button: the same
    // finger that drags a meal is the one that scrolls the week, so the drag
    // has to wait to be sure, and a finger that sets off straight away has to
    // keep scrolling.
    const phone = await browser.newContext({ ...devices['Pixel 7'] });
    const screen = await phone.newPage();

    try {
      await signInWithHousehold(screen, who);
      await screen.goto('/plan');
      await expect(screen.getByRole('link', { name: title })).toBeVisible();

      const from = await dayHolding(screen, title);

      // A finger that sets off immediately is scrolling, not dragging.
      await touchDragOnto(screen, title, await dayOtherThan(screen, from), { holdMs: 0 });
      await expect.poll(() => dayHolding(screen, title)).toBe(from);

      // The same finger, held first, carries it.
      const onto = await dayOtherThan(screen, from);

      await touchDragOnto(screen, title, onto, { holdMs: 500 });
      await expect.poll(() => dayHolding(screen, title)).toBe(onto);
    } finally {
      await phone.close();
    }
  });

  test('takes a meal off the plan again', async () => {
    await page.goto('/plan');

    await expect(page.getByRole('link', { name: title })).toBeVisible();

    // Named for what it does, not just for the meal: a card carries two
    // controls that mention the title, and the other one moves it.
    await page
      .getByRole('button', { name: new RegExp(`(take ${title} off|${title} vom plan)`, 'i') })
      .click();

    // That this meal is gone, not that the week is. These suites share one
    // instance, so an earlier run's Thursday is still somebody's Thursday.
    await expect(page.getByRole('link', { name: title })).toBeHidden();
  });
});

/** Which day of the week a meal is on, as the date the planner marks it with. */
async function dayHolding(page: Page, title: string): Promise<string> {
  const days = page.locator('[data-plan-day]');

  for (let index = 0; index < (await days.count()); index += 1) {
    const day = days.nth(index);

    if (await day.getByRole('link', { name: title }).isVisible()) {
      return (await day.getAttribute('data-plan-day')) ?? '';
    }
  }

  return '';
}

/** Where a day sits in the week, which is also its place in the move sheet. */
async function indexOfDay(page: Page, date: string): Promise<number> {
  const days = page.locator('[data-plan-day]');

  for (let index = 0; index < (await days.count()); index += 1) {
    if ((await days.nth(index).getAttribute('data-plan-day')) === date) {
      return index;
    }
  }

  throw new Error(`${date} is not a day of the week on screen.`);
}

/** Any day but the one a meal is already on. */
async function dayOtherThan(page: Page, date: string): Promise<string> {
  const dates = await page
    .locator('[data-plan-day]')
    .evaluateAll((all) => all.map((day) => (day as HTMLElement).dataset['planDay'] ?? ''));

  return dates.find((one) => one !== date) ?? '';
}

/**
 * Picks a card up and puts it down on another day.
 *
 * Moved in steps rather than in one jump, because that is what a hand does and
 * what the drag reads: a single move past the threshold would test that the
 * drop works and not that anything was ever followed. Both ends are brought on
 * screen first — the page moves the pointer in viewport coordinates, and the
 * planner starts below the fold.
 */
async function dragCardOnto(page: Page, title: string, date: string): Promise<void> {
  const card = page.getByRole('link', { name: title });
  const target = page.locator(`[data-plan-day="${date}"]`);

  await target.scrollIntoViewIfNeeded();
  await card.scrollIntoViewIfNeeded();

  const from = await card.boundingBox();
  const onto = await target.boundingBox();
  const viewport = page.viewportSize();

  if (!from || !onto || !viewport) {
    throw new Error('The card and the day it is going to both have to be on screen.');
  }

  const start = { x: from.x + from.width / 2, y: from.y + from.height / 2 };

  // The middle of the day, kept clear of the edges of the screen, where a real
  // drag would start scrolling the week rather than hovering a day.
  const to = {
    x: onto.x + onto.width / 2,
    y: Math.max(96, Math.min(onto.y + onto.height / 2, viewport.height - 96))
  };

  await page.mouse.move(start.x, start.y);
  await page.mouse.down();

  for (let step = 1; step <= 6; step += 1) {
    await page.mouse.move(
      start.x + ((to.x - start.x) * step) / 6,
      start.y + ((to.y - start.y) * step) / 6
    );
  }

  await page.mouse.up();
}

/**
 * A finger, which is not a mouse.
 *
 * Dispatched through the browser's own touch input rather than Playwright's
 * pointer helpers, because the thing under test is what the drag does with a
 * real `pointerType` of `touch` — and that is exactly what a synthesised mouse
 * gesture cannot tell you.
 */
async function touchDragOnto(
  page: Page,
  title: string,
  date: string,
  { holdMs }: { holdMs: number }
): Promise<void> {
  const card = page.getByRole('link', { name: title });
  const target = page.locator(`[data-plan-day="${date}"]`);

  await target.scrollIntoViewIfNeeded();
  await card.scrollIntoViewIfNeeded();

  const from = await card.boundingBox();
  const onto = await target.boundingBox();
  const viewport = page.viewportSize();

  if (!from || !onto || !viewport) {
    throw new Error('The card and the day it is going to both have to be on screen.');
  }

  const start = { x: from.x + from.width / 2, y: from.y + from.height / 2 };
  const to = {
    x: onto.x + onto.width / 2,
    y: Math.max(96, Math.min(onto.y + onto.height / 2, viewport.height - 96))
  };

  const touch = await page.context().newCDPSession(page);
  const at = (x: number, y: number) => ({ x: Math.round(x), y: Math.round(y) });

  await touch.send('Input.dispatchTouchEvent', {
    type: 'touchStart',
    touchPoints: [at(start.x, start.y)]
  });

  if (holdMs > 0) {
    await page.waitForTimeout(holdMs);
  }

  for (let step = 1; step <= 6; step += 1) {
    await touch.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [
        at(start.x + ((to.x - start.x) * step) / 6, start.y + ((to.y - start.y) * step) / 6)
      ]
    });
  }

  await touch.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  await touch.detach();
}

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
 * Plan a week; the payoff is that the plan writes the shopping list through the usual merge, never
 * twice for one meal.
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
    await page.goto('/');
    await page.getByRole('link', { name: /^(week|woche)$/i }).click();

    await expect(page).toHaveURL(/\/plan$/);

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

    await page.getByRole('button', { name: weekToList }).click();
    await expect(page.getByRole('link', { name: title })).toContainText(onTheList);
    await expect(page.getByText(everythingOnTheList)).toBeVisible();
    await expect(page.getByRole('button', { name: weekToList })).toBeHidden();

    await page.goto('/shopping');
    await expect(page.getByText(ingredient)).toBeVisible();
  });

  test('does not buy a recipe twice when it went on the list from its own page first', async () => {
    const flour = unique('Mehl');
    const waffles = unique('Waffles');
    const recipeId = await seedRecipe(page, {
      title: waffles,
      yieldAmount: 2,
      ingredients: [{ quantity: 250, unit: 'g', name: flour }]
    });

    await page.goto(`/recipes/${recipeId}`);
    await page.getByRole('button', { name: /shopping list|einkaufsliste/i }).click();
    await expect(page.getByText(/added to|hinzugefügt/i)).toBeVisible();

    await page.goto('/plan');
    await page
      .getByRole('button', { name: /^\+ (add|hinzufügen)$/i })
      .last()
      .click();

    const sheet = page.getByRole('dialog');

    await sheet.getByRole('searchbox').fill(waffles);
    await sheet.getByRole('button', { name: waffles }).click();
    await expect(sheet).toBeHidden();

    await page.getByRole('button', { name: weekToList }).click();
    await expect(page.getByRole('link', { name: waffles })).toContainText(onTheList);

    // Doubling here used to be silent.
    await page.goto('/shopping');
    await expect(page.getByRole('listitem').filter({ hasText: flour })).toContainText(/250\s*g/);
  });

  test('plans directly from a scaled recipe and keeps that serving count', async () => {
    const plannedTitle = unique('Scaled plan');
    const recipeId = await seedRecipe(page, {
      title: plannedTitle,
      yieldAmount: 2,
      ingredients: [{ quantity: 100, unit: 'g', name: unique('Rice') }],
      steps: ['Cook it.']
    });

    await page.goto(`/recipes/${recipeId}?yield=5`);
    await page.getByRole('button', { name: /more actions|weitere aktionen/i }).click();
    await page.getByRole('button', { name: /add to plan|einplanen/i }).click();

    const sheet = page.getByRole('dialog', { name: new RegExp(plannedTitle) });

    await expect(sheet).toBeVisible();
    await sheet.getByRole('radio').first().check();

    await Promise.all([
      page.waitForResponse(
        (response) =>
          response.url().endsWith('/meal-plan') && response.request().method() === 'POST'
      ),
      sheet.getByRole('button', { name: /add to week|zur woche hinzufügen/i }).click()
    ]);

    await page.goto('/plan');

    await expect(page.getByRole('link', { name: new RegExp(plannedTitle) })).toContainText(
      /5 servings|5 Portionen/i
    );
  });

  test('moves a meal to another day by dragging it there', async () => {
    await page.goto('/plan');

    await expect(page.getByRole('link', { name: title })).toBeVisible();

    const onto = await dayOtherThan(page, await dayHolding(page, title));

    await dragCardOnto(page, title, onto);

    await expect.poll(() => dayHolding(page, title)).toBe(onto);

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

    // The undo is drawn before it is saved, so the reload has to wait for the save or it cancels
    // it.
    await Promise.all([
      page.waitForResponse(
        (response) =>
          response.url().includes('/meal-plan/') && response.request().method() === 'PATCH'
      ),
      page.getByRole('button', { name: /^(undo|rückgängig)$/i }).click()
    ]);

    await expect.poll(() => dayHolding(page, title)).toBe(from);

    await page.reload();
    await expect.poll(() => dayHolding(page, title)).toBe(from);
  });

  test('moves a meal without a pointer, through the grip', async () => {
    await page.goto('/plan');

    await expect(page.getByRole('link', { name: title })).toBeVisible();

    const onto = await dayOtherThan(page, await dayHolding(page, title));
    const which = await indexOfDay(page, onto);

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
    // A phone has no hover: the same finger drags a meal and scrolls the week, so the drag has to
    // wait to be sure.
    const phone = await browser.newContext({ ...devices['Pixel 7'] });
    const screen = await phone.newPage();

    try {
      await signInWithHousehold(screen, who);
      await screen.goto('/plan');
      await expect(screen.getByRole('link', { name: title })).toBeVisible();

      const from = await dayHolding(screen, title);

      await touchDragOnto(screen, title, await dayOtherThan(screen, from), { holdMs: 0 });
      await expect.poll(() => dayHolding(screen, title)).toBe(from);

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

    // The card carries two controls that mention the title; the other one moves it.
    await page
      .getByRole('button', { name: new RegExp(`(take ${title} off|${title} vom plan)`, 'i') })
      .click();

    await expect(page.getByRole('link', { name: title })).toBeHidden();

    await page.getByRole('button', { name: /^(remove them|entfernen)$/i }).click();
    await expect(
      page.getByText(/off the shopping list|von der einkaufsliste entfernt/i)
    ).toBeVisible();

    await page.goto('/shopping');
    await expect(page.getByText(ingredient)).toBeHidden();
  });
});

const weekToList =
  /^put \d+ meals? on the shopping list$|^\d+ mahlzeit(en)? auf die einkaufsliste$/i;
const onTheList = /on the shopping list|auf der einkaufsliste/i;
const everythingOnTheList =
  /everything planned this week is on|alles, was diese woche geplant ist/i;

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

async function indexOfDay(page: Page, date: string): Promise<number> {
  const days = page.locator('[data-plan-day]');

  for (let index = 0; index < (await days.count()); index += 1) {
    if ((await days.nth(index).getAttribute('data-plan-day')) === date) {
      return index;
    }
  }

  throw new Error(`${date} is not a day of the week on screen.`);
}

async function dayOtherThan(page: Page, date: string): Promise<string> {
  const dates = await page
    .locator('[data-plan-day]')
    .evaluateAll((all) => all.map((day) => (day as HTMLElement).dataset['planDay'] ?? ''));

  return dates.find((one) => one !== date) ?? '';
}

/**
 * Picks a card up and puts it down on another day, in steps like a hand; both ends are scrolled
 * into view first.
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

  // Kept clear of the screen edges, where a real drag would start scrolling.
  const to = {
    x: onto.x + onto.width / 2,
    y: Math.max(96, Math.min(onto.y + onto.height / 2, viewport.height - 96))
  };

  // Watched before the button comes up: the move is sent immediately and the optimistic redraw
  // would let a reload beat the request.
  const moved = page.waitForResponse(
    (response) => response.url().includes('/meal-plan/') && response.request().method() === 'PATCH'
  );

  await page.mouse.move(start.x, start.y);
  await page.mouse.down();

  for (let step = 1; step <= 6; step += 1) {
    await page.mouse.move(
      start.x + ((to.x - start.x) * step) / 6,
      start.y + ((to.y - start.y) * step) / 6
    );
  }

  await page.mouse.up();
  await moved;
}

/**
 * A finger, not a mouse: dispatched through the browser's touch input so `pointerType` is `touch`.
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

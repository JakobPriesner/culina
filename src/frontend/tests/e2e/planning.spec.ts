import { devices, expect, test, type Locator, type Page } from '@playwright/test';

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

/**
 * Picks a recipe in the plan sheet by its title. Until the search answers, the sheet offers to add the words as a
 * new recipe, whose button names them too: clicking that planned an empty recipe of the same name.
 */
async function pick(sheet: Locator, title: string) {
  await sheet.getByRole('searchbox').fill(title);
  await sheet.getByRole('button', { name: title }).and(sheet.locator(':not(.stub)')).click();
}

/** A day's own add button, named for its day: "Add for Sunday", "Für Sonntag hinzufügen". */
const addOnDay = /^(add for \S+|für \S+ hinzufügen)$/i;

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
    await inDays(page);

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

    // The days on screen: the week with its days already over folded, or a run from today on a phone.
    await expect(
      page
        .getByRole('listitem')
        .filter({ has: page.getByRole('heading', { level: 2 }) })
        .first()
    ).toBeVisible();

    // Today: the first day on screen, the days already over folded away.
    await page.getByRole('button', { name: addOnDay }).first().click();

    const sheet = page.getByRole('dialog');

    await expect(sheet).toBeVisible();
    await pick(sheet, title);

    await expect(sheet).toBeHidden();
    await expect(cardOf(page, title)).toBeVisible();

    await page.getByRole('button', { name: weekToList }).click();
    await expect(cardOf(page, title)).toContainText(onTheList);
    if (!listBeside(page)) await expect(page.getByText(everythingOnTheList)).toBeVisible();
    await expect(page.getByRole('button', { name: weekToList })).toBeHidden();

    await page.goto('/shopping');
    // Exact: the planned-meals list names the same line again, with its amount.
    await expect(page.getByText(ingredient, { exact: true })).toBeVisible();
  });

  test('does not buy a recipe twice when it went on the list from its own page first', async () => {
    const flour = unique('Mehl');
    const waffles = unique('Waffles');
    const recipeId = await seedRecipe(page, {
      title: waffles,
      yieldAmount: 2,
      ingredients: [{ quantity: 250, unit: 'g', name: flour }]
    });

    await addToList(page, recipeId);

    await page.goto('/plan');
    await page.getByRole('button', { name: addOnDay }).last().click();

    const sheet = page.getByRole('dialog');

    await pick(sheet, waffles);
    await expect(sheet).toBeHidden();

    // Planning it makes the listing this meal's shopping: the week has nothing left to offer for it.
    await expect(cardOf(page, waffles)).toContainText(onTheList);
    await expect(page.getByRole('button', { name: weekToList })).toHaveCount(0);

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
    await page.getByRole('button', { name: /^(add to plan|einplanen|plan|planen)$/i }).click();

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

    await expect(cardOf(page, new RegExp(plannedTitle))).toContainText(/5 servings|5 Portionen/i);
  });

  test('moves a meal to another day by dragging it there', async () => {
    await openWeek(page);

    await expect(cardOf(page, title)).toBeVisible();

    const onto = await dayOtherThan(page, await settledDay(page, title));

    await dragCardOnto(page, title, onto);

    await expect.poll(() => dayHolding(page, title)).toBe(onto);

    await page.reload();
    await expect.poll(() => dayHolding(page, title)).toBe(onto);
  });

  test('puts it back when the move is undone', async () => {
    await openWeek(page);

    await expect(cardOf(page, title)).toBeVisible();

    const from = await settledDay(page, title);
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

    await expect(cardOf(page, title)).toBeVisible();

    const onto = await dayOtherThan(page, await settledDay(page, title));

    await page
      .getByRole('button', {
        name: new RegExp(`(edit or move ${title}|${title} bearbeiten oder verschieben)`, 'i')
      })
      .click();

    const sheet = page.getByRole('dialog');

    await expect(sheet).toBeVisible();

    // The sheet also edits servings and the meal; the day is its own group, its days around the meal's, each named by
    // its whole date.
    await sheet
      .getByRole('group', { name: /^(day|tag)$/i })
      .getByRole('radio', { name: new RegExp(`^${await longDate(page, onto)}:`) })
      .check();
    await sheet.getByRole('button', { name: /^(save|speichern)$/i }).click();

    await expect(sheet).toBeHidden();
    await expect.poll(() => dayHolding(page, title)).toBe(onto);
  });

  test('waits for a held finger before it carries anything', async ({ browser }) => {
    // A phone has no hover: the same finger drags a meal and scrolls the week, so the drag has to
    // wait to be sure.
    const phone = await browser.newContext({ ...devices['Pixel 7'] });
    const screen = await phone.newPage();

    try {
      await inDays(screen);
      await signInWithHousehold(screen, who);
      await openWeek(screen);
      await expect(cardOf(screen, title)).toBeVisible();

      const from = await settledDay(screen, title);

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

    await expect(cardOf(page, title)).toBeVisible();

    // The card carries two controls that mention the title; the other one moves it. On a laptop they show on hover.
    await cardOf(page, title).hover();
    await page
      .getByRole('button', { name: new RegExp(`(take ${title} off|${title} vom plan)`, 'i') })
      .click();

    await expect(cardOf(page, title)).toBeHidden();

    // Its lines on the list go too, once asked: beside the list at the lines themselves, else in the toast.
    if (listBeside(page)) {
      await page
        .getByRole('button', { name: /^(remove \d+ lines?|\d+ zeilen? entfernen)$/i })
        .click();
      await expect(page.getByText(/is no longer planned|ist nicht mehr geplant/i)).toHaveCount(0);
    } else {
      await page.getByRole('button', { name: /^(remove them|entfernen)$/i }).click();
      await expect(
        page.getByText(/off the shopping list|von der einkaufsliste entfernt/i)
      ).toBeVisible();
    }

    await page.goto('/shopping');
    await expect(page.getByText(ingredient)).toBeHidden();
  });
});

/** What the week still needs, onto the list: the bar under the week, or beside the list the line at its top. */
const weekToList =
  /^put \d+ meals? on the shopping list$|^\d+ mahlzeit(en)? auf die einkaufsliste$|^add to shopping list$|^auf die einkaufsliste$/i;
const onTheList = /on the shopping list|auf der einkaufsliste/i;
const everythingOnTheList =
  /everything planned this week is on|alles, was diese woche geplant ist/i;

/** The recipe's whole ingredient list onto the shopping list, through its sheet. */
async function addToList(page: Page, recipeId: string) {
  await page.goto(`/recipes/${recipeId}`);
  await page
    .getByRole('button', {
      name: /^(add to the shopping list|auf die einkaufsliste|shop|einkaufen)$/i
    })
    .click();
  await page
    .getByRole('button', { name: /^(add \d+ ingredients?|\d+ zutat(en)? hinzufügen)$/i })
    .click();
  await expect(page.getByText(/added to|hinzugefügt/i)).toBeVisible();
}

/** A meal's card on the days, found by its title link. */
function cardOf(page: Page, title: string | RegExp): Locator {
  return page
    .locator('[data-plan-day] .card')
    .filter({ has: page.getByRole('link', { name: title, exact: typeof title === 'string' }) });
}

/** From 80rem the list sits beside the week, and its top line says what the week still needs instead of a bar. */
const listBeside = (page: Page) => page.viewportSize()!.width >= 1280;

/**
 * The plan as the coming days from today, whatever the screen: in the week view the days already over fold into one
 * line, and on a Sunday that leaves no other day to move a meal to.
 */
async function inDays(page: Page): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('culina.plan.view', 'days'));
}

/** The days on screen, loaded: today's heading is drawn with them. */
async function openWeek(page: Page): Promise<void> {
  await page.goto('/plan');
  await expect(page.getByRole('heading', { level: 2, name: /^(today|heute)\b/i })).toBeVisible();
}

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

/** The day a card is on once it is on one: `dayHolding` alone answers '' while the week is still loading. */
async function settledDay(page: Page, title: string): Promise<string> {
  await expect.poll(() => dayHolding(page, title)).not.toBe('');

  return dayHolding(page, title);
}

/** A date as the meal sheet names its day: "Montag, 26. Oktober", in the page's language. */
function longDate(page: Page, date: string): Promise<string> {
  return page.evaluate(
    (day) =>
      new Intl.DateTimeFormat(document.documentElement.lang, {
        weekday: 'long',
        day: 'numeric',
        month: 'long'
      }).format(new Date(`${day}T12:00`)),
    date
  );
}

async function dayOtherThan(page: Page, date: string): Promise<string> {
  const dates = await page
    .locator('[data-plan-day]')
    .evaluateAll((all) => all.map((day) => (day as HTMLElement).dataset['planDay'] ?? ''));

  return dates.find((one) => one !== date) ?? '';
}

/**
 * The card in the middle of the screen, clear of the sticky header (a press under it never reaches the card) and of the
 * floating dock. Scrolled at once rather than smoothly, then read only once it holds still: a finger's swipe just
 * before keeps the page coasting, and boxes read mid-scroll miss both ends of the drag.
 */
async function bringIntoReach(page: Page, card: Locator): Promise<void> {
  await expect
    .poll(async () => {
      await card.evaluate((element) =>
        element.scrollIntoView({ block: 'center', behavior: 'instant' })
      );
      const before = (await card.boundingBox())?.y;
      await page.waitForTimeout(200);
      return before === (await card.boundingBox())?.y;
    })
    .toBe(true);
}

/**
 * Where to let go over a day: just inside its edge nearest the card, so a day grown tall with meals is still met on
 * screen, next to the card, rather than at a middle that may lie under the dock or past the screen's end.
 */
function dropPoint(
  from: { y: number },
  onto: { x: number; y: number; width: number; height: number }
) {
  const inside = Math.min(48, onto.height / 2);

  return {
    x: onto.x + onto.width / 2,
    y: onto.y > from.y ? onto.y + inside : onto.y + onto.height - inside
  };
}

/**
 * Picks a card up and puts it down on another day, in steps like a hand; both ends are scrolled
 * into view first.
 */
async function dragCardOnto(page: Page, title: string, date: string): Promise<void> {
  const card = cardOf(page, title);
  const target = page.locator(`[data-plan-day="${date}"]`);

  await bringIntoReach(page, card);

  // By its title: the card's middle can be its list or cook control, which stay clicks.
  const from = await card.getByRole('link', { name: title, exact: true }).boundingBox();
  const onto = await target.boundingBox();
  const viewport = page.viewportSize();

  if (!from || !onto || !viewport) {
    throw new Error('The card and the day it is going to both have to be on screen.');
  }

  const start = { x: from.x + from.width / 2, y: from.y + from.height / 2 };

  // Kept clear of the screen edges, where a real drag would start scrolling.
  const to = dropPoint(from, onto);

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
  const card = cardOf(page, title);
  const target = page.locator(`[data-plan-day="${date}"]`);

  await bringIntoReach(page, card);

  // By its title: the card's middle can be its list or cook control, which stay clicks.
  const from = await card.getByRole('link', { name: title, exact: true }).boundingBox();
  const onto = await target.boundingBox();
  const viewport = page.viewportSize();

  if (!from || !onto || !viewport) {
    throw new Error('The card and the day it is going to both have to be on screen.');
  }

  const start = { x: from.x + from.width / 2, y: from.y + from.height / 2 };
  const to = dropPoint(from, onto);

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

import { expect, test, type Page } from '@playwright/test';
import { expectReflow, expectWholeWords, responsiveData } from './support/responsive';

/**
 * The food diary over the layout fixtures, with a small diary server of its own: a tab for everyone, whose page offers
 * the switch while it is off; on, logging and removing an entry add up on the page at once.
 */
interface Entry {
  entryId: string;
  date: string;
  meal: string;
  source: string;
  label: string;
  amount: number;
  unit: string;
  figures: { kcal: number | null; protein?: number | null };
  atLeast: boolean;
  estimated: boolean;
  version: number;
  recipeId?: string;
}

async function diaryServer(page: Page, enabled: boolean) {
  const entries: Entry[] = [];
  const settings = {
    enabled,
    currentConsentVersion: 1,
    consentVersion: enabled ? 1 : null,
    consentedAt: enabled ? '2026-10-01T08:00:00Z' : null,
    kcalTarget: 2000,
    proteinTarget: 100,
    headline: 'eaten',
    version: 1
  };

  const dayOf = (date: string) => {
    const mine = entries.filter((entry) => entry.date === date);
    const kcal = mine.reduce((sum, entry) => sum + (entry.figures.kcal ?? 0), 0);

    return {
      date,
      entries: mine,
      totals: { kcal: mine.length ? kcal : null, protein: null, carbohydrate: null, fat: null },
      atLeast: false,
      estimated: false,
      typedKcal: mine
        .filter((entry) => entry.source === 'typed')
        .reduce((sum, entry) => sum + (entry.figures.kcal ?? 0), 0)
    };
  };

  await page.route('**/api/v1/users/me/settings/diary', (route) => {
    if (route.request().method() === 'PUT') Object.assign(settings, route.request().postDataJSON());
    return route.fulfill({ json: settings });
  });

  await page.route('**/api/v1/users/me/diary**', async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();

    if (!settings.enabled) {
      return route.fulfill({
        status: 403,
        contentType: 'application/problem+json',
        json: { code: 'diary.disabled', status: 403 }
      });
    }

    if (method === 'GET') {
      const from = url.searchParams.get('from')!;
      const to = url.searchParams.get('to') ?? from;
      const days = [];

      for (
        let day = new Date(`${from}T12:00:00`);
        day <= new Date(`${to}T12:00:00`);
        day.setDate(day.getDate() + 1)
      ) {
        days.push(dayOf(day.toISOString().slice(0, 10)));
      }

      return route.fulfill({ json: { days } });
    }

    if (method === 'POST') {
      const body = route.request().postDataJSON();
      const entry: Entry = {
        entryId: crypto.randomUUID(),
        date: body.date,
        meal: body.meal,
        source: body.kcal != null ? 'typed' : body.recipeId ? 'recipe' : 'food',
        label: body.label ?? (body.recipeId ? 'Geplantes Abendessen' : 'Haferflocken'),
        amount: body.amount ?? body.portions ?? 1,
        unit: body.unit ?? 'portion',
        ...(body.recipeId ? { recipeId: body.recipeId } : {}),
        figures: { kcal: body.kcal ?? 186, protein: body.protein ?? null },
        atLeast: false,
        estimated: false,
        version: 1
      };

      entries.push(entry);

      return route.fulfill({ status: 201, json: { entry } });
    }

    if (method === 'PATCH') {
      const body = route.request().postDataJSON();
      const entry = entries.find((one) => one.entryId === url.pathname.split('/').pop())!;

      entry.meal = body.meal ?? entry.meal;
      entry.label = body.label ?? entry.label;
      if (body.kcal != null) entry.figures = { kcal: body.kcal, protein: body.protein ?? null };
      entry.version += 1;

      return route.fulfill({ json: { entry } });
    }

    if (method === 'DELETE') {
      const id = url.pathname.split('/').pop();
      entries.splice(
        entries.findIndex((entry) => entry.entryId === id),
        1
      );

      return route.fulfill({ status: 204 });
    }

    return route.fulfill({ status: 404 });
  });
}

/** A day as the week strip names it to a screen reader. */
const dayName = (day: Date) =>
  new Intl.DateTimeFormat('de-DE', { weekday: 'long', day: 'numeric', month: 'long' }).format(day);

test.describe('food diary @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('off, it keeps its tab and stays out of the home page', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryServer(page, false);
    await page.goto('/');
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

    const nav = page.locator('nav.bottom');
    await expect(nav.getByRole('link')).toHaveCount(4);
    await expect(nav.getByRole('link', { name: 'Ernährung' })).toBeVisible();
    await expect(page.getByText('Ernährung: heute noch nichts eingetragen')).toHaveCount(0);

    await nav.getByRole('link', { name: 'Ernährung' }).click();
    await expect(page.getByRole('button', { name: 'Einschalten' })).toBeVisible();
  });

  for (const width of [320, 390]) {
    test(`a tab, and an entry logged and removed adds up at once (${width}px)`, async ({
      page
    }) => {
      await page.setViewportSize({ width, height: 844 });
      await responsiveData(page, 'de', { activeCooking: false });
      await diaryServer(page, true);
      await page.goto('/');
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);

      const nav = page.locator('nav.bottom');
      const tab = nav.getByRole('link', { name: 'Ernährung' });
      await expectWholeWords(nav.getByRole('link'));
      await expect(page.getByText('Ernährung: heute noch nichts eingetragen')).toBeVisible();

      await tab.click();
      await expect(page).toHaveURL(/\/diary$/);
      await expect(page.getByRole('heading', { level: 1, name: 'Heute' })).toBeVisible();
      await expect(page.getByText('unter deinem Richtwert', { exact: false })).toHaveCount(0);
      await expect(page.getByText('Dein Richtwert: 2.000 kcal')).toBeVisible();

      // A café lunch, typed: it shows as the person's own, and the day adds it up.
      await page
        .getByRole('region', { name: 'Mittagessen' })
        .getByRole('button', { name: /Eintragen/ })
        .click();
      await page.getByRole('button', { name: 'Eigene Angaben' }).click();
      await page.getByLabel('Was war es?').fill('Café-Lunch');
      await page.getByLabel('Kilokalorien').fill('650');
      await page.getByRole('dialog').getByRole('button', { name: 'Eintragen' }).click();

      const row = page
        .getByRole('region', { name: 'Mittagessen' })
        .getByRole('button', { name: /Café-Lunch/ });
      await expect(row).toBeVisible();
      await expect(row).toContainText('von dir eingetragen');
      await expect(page.getByText('1.350 kcal bis zum Richtwert')).toBeVisible();
      await expect(
        page.getByRole('img', { name: /650 kcal bei einem Richtwert von 2\.000 kcal/ })
      ).toBeVisible();
      await expectReflow(page);
      await page.screenshot({
        path: test.info().outputPath(`diary-${width}.png`),
        fullPage: true
      });

      // A typed figure is corrected where it stands, and the day adds up the new one.
      await row.click();
      await page.getByRole('dialog').getByLabel('Kilokalorien').fill('600');
      await page.getByRole('dialog').getByRole('button', { name: 'Speichern' }).click();
      await expect(page.getByText('1.400 kcal bis zum Richtwert')).toBeVisible();

      await row.click();
      await page.getByRole('dialog').getByRole('button', { name: 'Entfernen' }).click();
      await expect(row).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'Rückgängig' }).first()).toBeVisible();
    });
  }

  test("the week strip opens yesterday, and yesterday's breakfast repeats today in one tap", async ({
    page
  }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryServer(page, true);
    await page.goto('/diary');
    await expect(page.getByRole('heading', { level: 1, name: 'Heute' })).toBeVisible();

    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);
    const strip = page.getByRole('group', { name: /^Woche vom/ });
    // Monday's yesterday is in the week before.
    if (yesterday.getDay() === 0) await strip.getByRole('button', { name: 'Woche davor' }).click();
    await strip.getByRole('button', { name: dayName(yesterday) }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Gestern' })).toBeVisible();

    await page
      .getByRole('region', { name: 'Frühstück' })
      .getByRole('button', { name: /Eintragen/ })
      .click();
    await page.getByRole('button', { name: 'Eigene Angaben' }).click();
    await page.getByLabel('Was war es?').fill('Porridge');
    await page.getByLabel('Kilokalorien').fill('380');
    await page.getByRole('dialog').getByRole('button', { name: 'Eintragen' }).click();
    await expect(
      page.getByRole('region', { name: 'Frühstück' }).getByRole('button', { name: /Porridge/ })
    ).toBeVisible();

    if (yesterday.getDay() === 0) await strip.getByRole('button', { name: 'Woche danach' }).click();
    await strip.getByRole('button', { name: dayName(new Date()) }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Heute' })).toBeVisible();

    const breakfast = page.getByRole('region', { name: 'Frühstück' });
    await breakfast.getByRole('button', { name: 'Wie gestern: Porridge' }).click();
    await expect(breakfast.getByRole('button', { name: /Porridge/ })).toBeVisible();
    await expect(breakfast.getByRole('button', { name: /Wie gestern/ })).toHaveCount(0);
    await expect(page.getByText('1 Eintrag eingetragen')).toBeVisible();
  });

  test("the plan's meal of today is logged or skipped in one tap", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryServer(page, true);
    await page.goto('/diary');

    const dinner = page.getByRole('region', { name: 'Abendessen' });
    const planned = dinner.getByRole('button', { name: /als gegessen eintragen$/ });
    await expect(planned).toBeVisible();

    await dinner.getByRole('button', { name: /überspringen$/ }).click();
    await expect(planned).toHaveCount(0);
    await page.getByRole('button', { name: 'Rückgängig' }).first().click();
    await expect(planned).toBeVisible();

    await planned.click();
    await expect(planned).toHaveCount(0);
    await expect(dinner.getByRole('button', { name: /Portion/ })).toBeVisible();
  });

  test('the settings switch carries its one sentence of agreement', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await responsiveData(page, 'de', { activeCooking: false });
    await diaryServer(page, false);
    await page.goto('/me/diary');

    await expect(page.getByRole('switch', { name: 'Ernährung' })).toHaveAttribute(
      'aria-checked',
      'false'
    );
    await expect(
      page.getByText('Deine Einträge sind Gesundheitsdaten und nur für dich sichtbar.', {
        exact: false
      })
    ).toBeVisible();

    await page.getByRole('switch', { name: 'Ernährung' }).click();
    await expect(page.getByRole('switch', { name: 'Ernährung' })).toHaveAttribute(
      'aria-checked',
      'true'
    );
    await expect(page.getByLabel('Kilokalorien am Tag')).toHaveValue('2000');
    await page.screenshot({ path: test.info().outputPath('diary-settings.png'), fullPage: true });
  });
});

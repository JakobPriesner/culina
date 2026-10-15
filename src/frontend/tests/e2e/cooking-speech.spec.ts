import { expect, test, type Page } from '@playwright/test';

import {
  accountFor,
  needsBackend,
  seedRecipe,
  signInWithHousehold,
  skipReason,
  unique
} from './support/culina';

/** The kitchen reads a step aloud: on request, and on advancing when the cook asks for it. A real voice cannot be heard here, so the engine is a recorder. */
test.describe.configure({ mode: 'serial' });

test.describe('hearing a step', () => {
  test.skip(needsBackend, skipReason);

  let page: Page;

  test.beforeAll(async ({ browser }, testInfo) => {
    if (needsBackend) {
      return;
    }

    page = await browser.newPage();

    await page.addInitScript(() => {
      const spoken: string[] = [];
      const engine = {
        speak: (utterance: { text: string; onend?: () => void }) =>
          void spoken.push(utterance.text),
        cancel: () => {},
        getVoices: () => []
      };

      Object.defineProperty(window, 'speechSynthesis', { value: engine, configurable: true });
      Object.defineProperty(window, '__spoken', { value: spoken });
    });

    await signInWithHousehold(page, await accountFor(browser, testInfo));
  });

  test.afterAll(async () => {
    await page?.close();
  });

  test('reads the step in words, and reads the next one on advancing once asked to', async () => {
    const recipeId = await seedRecipe(page, {
      title: unique('Spoken'),
      yieldAmount: 2,
      ingredients: [{ quantity: 200, unit: 'g', name: 'Butter' }],
      steps: ['Melt {0}.', 'Stir in 2 tbsp sugar at 180°C.']
    });

    const spoken = () =>
      page.evaluate(() => (window as unknown as { __spoken: string[] }).__spoken);

    await page.goto(`/recipes/${recipeId}`);
    await page.getByRole('button', { name: /^(start cooking|kochen starten)$/i }).click();
    await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}/cook`));

    await page.getByRole('button', { name: /^(read step aloud|schritt vorlesen)$/i }).click();

    await expect.poll(spoken).toEqual(['Melt 200 grams Butter.']);
    await expect(
      page.getByRole('button', { name: /^(stop reading|vorlesen beenden)$/i })
    ).toBeVisible();

    await page.getByRole('button', { name: /^(kitchen controls|küchensteuerung)$/i }).click();
    await page
      .getByRole('switch', { name: /auto-read on advance|beim weiterschalten vorlesen/i })
      .click();
    await page.keyboard.press('Escape');

    await page.getByRole('button', { name: /^(next step|nächster schritt)$/i }).click();

    await expect
      .poll(spoken)
      .toEqual(['Melt 200 grams Butter.', 'Stir in 2 tablespoons sugar at 180 degrees Celsius.']);
  });
});

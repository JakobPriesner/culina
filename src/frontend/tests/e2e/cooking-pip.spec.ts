import { expect, test } from '@playwright/test';
import { expectReflow, recipeId, responsiveData } from './support/responsive';

test.describe('floating kitchen companion @offline', () => {
  test.use({ serviceWorkers: 'block' });

  test('shares the cooking session, timers and appearance with a real PiP window', async ({
    page,
    context
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'Document PiP is a desktop capability.');
    await responsiveData(page, 'en');
    const policyErrors: string[] = [];
    const watchPolicy = (text: string) => {
      if (/violates.*Content Security Policy|Refused.*policy/i.test(text)) policyErrors.push(text);
    };
    page.on('console', (message) => watchPolicy(message.text()));
    context.on('page', (child) => child.on('console', (message) => watchPolicy(message.text())));
    await page.route(
      new RegExp(`^http://localhost:4173/recipes/${recipeId}/cook(?:\\?.*)?$`),
      async (route) => {
        const response = await route.fetch();
        await route.fulfill({
          response,
          body: (await response.text())
            .replaceAll('<script>', '<script nonce="__CULINA_NONCE__">')
            .replaceAll('__CULINA_NONCE__', 'culinaPipTest'),
          headers: {
            ...response.headers(),
            'Content-Security-Policy':
              "default-src 'self'; script-src 'self' 'nonce-culinaPipTest'; style-src 'self' 'nonce-culinaPipTest'; style-src-attr 'unsafe-hashes' 'sha256-S8qMpvofolR8Mpjy4kQvEm7m1q8clzU4dfDH0AmvZjo='; font-src 'self'; img-src 'self' data: blob:; base-uri 'none'; object-src 'none'"
          }
        });
      }
    );
    await page.route(`**/api/v1/recipes/${recipeId}`, (route) =>
      route.fulfill({
        json: {
          recipeId,
          householdId: '00000000-0000-4000-8000-000000000002',
          title: 'Butter sauce',
          language: 'en',
          yieldAmount: 2,
          yieldKind: 'servings',
          groups: [
            { ingredients: [{ ingredientId: 'butter', name: 'butter', quantity: 200, unit: 'g' }] }
          ],
          tags: [],
          version: 1,
          steps: [
            {
              stepId: 's1',
              title: 'Simmer',
              durationSeconds: 120,
              uses: ['butter'],
              segments: [
                { type: 'text', value: 'Melt ' },
                {
                  type: 'ingredient',
                  recipeIngredientId: 'butter',
                  name: 'butter',
                  quantity: 200,
                  unit: 'g'
                },
                { type: 'text', value: ' slowly.' }
              ]
            },
            {
              stepId: 's2',
              title: 'Serve',
              uses: [],
              segments: [{ type: 'text', value: 'Serve warm.' }]
            }
          ]
        },
        headers: { ETag: '"v1"' }
      })
    );
    await page.addInitScript(() =>
      localStorage.setItem(
        'culina.timers.session-1',
        JSON.stringify([
          { stepIndex: 0, endsAt: Date.now() + 600_000, label: 'Simmer' },
          { stepIndex: 1, endsAt: Date.now() + 600_000, label: 'Bake' }
        ])
      )
    );
    await page.goto(`/recipes/${recipeId}/cook?yield=4`);
    await page.getByRole('button', { name: 'Kitchen controls' }).click();
    const toggle = page.getByRole('button', { name: 'Float cooking window' });
    await expect(toggle).toBeVisible();
    const opened = context.waitForEvent('page');
    await toggle.click();
    const child = await opened;
    await page
      .getByRole('dialog', { name: 'Kitchen controls' })
      .getByRole('button', { name: 'Close', exact: true })
      .click();
    child.on('console', (message) => watchPolicy(message.text()));
    await expect(child.getByRole('heading', { name: 'Butter sauce', level: 1 })).toBeVisible();
    await expect(child.getByText('400 g butter')).toBeVisible();
    await page.getByRole('button', { name: 'One more', exact: true }).click();
    await expect(child.getByText('500 g butter')).toBeVisible();
    await child.getByRole('button', { name: 'Next step', exact: true }).click();
    await expect(page.getByText('Step 2 of 2', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Previous step' }).click();
    await expect(child.getByText('Step 1 of 2', { exact: true })).toBeVisible();
    await child.getByRole('button', { name: 'Pause timer' }).first().click();
    await expect(page.getByRole('button', { name: 'Resume timer' })).toBeVisible();
    await page.getByRole('button', { name: 'Resume timer' }).click();
    await expect(child.getByRole('button', { name: 'Pause timer' })).toHaveCount(2);
    await page.getByRole('button', { name: 'Kitchen controls' }).click();
    for (const mode of ['glare', 'oled', 'normal']) {
      await page.getByLabel('Kitchen display').selectOption(mode);
      if (mode === 'normal')
        await expect(child.locator('html')).not.toHaveAttribute('data-kitchen-lighting');
      else await expect(child.locator('html')).toHaveAttribute('data-kitchen-lighting', mode);
      await expect(child.locator('body')).toHaveCSS(
        'background-color',
        mode === 'glare'
          ? 'rgb(255, 255, 255)'
          : mode === 'oled'
            ? 'rgb(0, 0, 0)'
            : 'rgb(250, 249, 246)'
      );
      await expectReflow(child);
    }
    await page
      .getByRole('dialog', { name: 'Kitchen controls' })
      .getByRole('button', { name: 'Close', exact: true })
      .click();
    await child.setViewportSize({ width: 420, height: 560 });
    await expect(child.locator('body')).toHaveCSS('background-color', 'rgb(250, 249, 246)');
    await child.screenshot({ path: testInfo.outputPath('companion.png'), fullPage: true });
    await page
      .locator('nav:visible')
      .getByRole('link', { name: /shopping/i })
      .click();
    await expect(page.getByRole('heading', { name: 'Shopping', level: 1 })).toBeVisible();
    await child.getByRole('button', { name: 'Next step', exact: true }).click();
    await child.getByRole('button', { name: 'Back to finish' }).click();
    await expect(page).toHaveURL(new RegExp(`/recipes/${recipeId}/cook`));
    await expect(page.getByText('Step 2 of 2', { exact: true })).toBeVisible();
    await expect.poll(() => child.isClosed()).toBe(true);
    await page.getByRole('button', { name: 'Kitchen controls' }).click();
    const again = context.waitForEvent('page');
    await page.getByRole('button', { name: 'Float cooking window' }).click();
    await again;
    const reopened = context
      .pages()
      .find((candidate) => candidate !== page && !candidate.isClosed());
    expect(reopened).toBeDefined();
    await expect(reopened!.getByText('Step 2 of 2', { exact: true })).toBeVisible();
    await reopened!.close();
    await expect(page.getByRole('button', { name: 'Float cooking window' })).toBeVisible();
    await expect(page.getByText('Step 2 of 2', { exact: true })).toBeVisible();
    expect(policyErrors).toEqual([]);
  });

  test('keeps cooking usable when the browser has no Document PiP API', async ({ page }) => {
    await responsiveData(page, 'de');
    await page.addInitScript(() =>
      Object.defineProperty(window, 'documentPictureInPicture', {
        configurable: true,
        value: undefined
      })
    );
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto(`/recipes/${recipeId}/cook`);
    await expect(page.getByRole('button', { name: 'Kochfenster schweben lassen' })).toHaveCount(0);
    await page.getByRole('button', { name: 'Nächster Schritt', exact: true }).click();
    await expect(page.getByText('Schritt 2 von 4', { exact: true })).toBeVisible();
    await expectReflow(page);
  });
});

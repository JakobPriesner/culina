import { expect, test } from '@playwright/test';
import { cookbookId, expectReflow, recipeId, responsiveData } from './support/responsive';

test.describe('responsive transitions @offline', () => {
  test.use({ serviceWorkers: 'block', reducedMotion: 'reduce' });

  test('cooking keeps its step and controls when the viewport changes', async ({ page }) => {
    await responsiveData(page, 'en');
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto(`/recipes/${recipeId}/cook`);
    const next = page.getByRole('button', { name: 'Next step', exact: true });
    await next.click();
    for (const size of [
      { width: 390, height: 844 },
      { width: 844, height: 390 },
      { width: 1024, height: 768 },
      { width: 320, height: 568 }
    ]) {
      await page.setViewportSize(size);
      await expect(page.getByText('Step 2 of 4', { exact: true })).toBeVisible();
      await next.scrollIntoViewIfNeeded();
      await expect(next).toBeInViewport({ ratio: 1 });
      await expectReflow(page);
      const navigation = page.locator('nav.nav:visible');
      await expect(navigation).toHaveCount(1);
      if (size.width < 1024) {
        const button = (await next.boundingBox())!;
        expect(button.y + button.height).toBeLessThanOrEqual((await navigation.boundingBox())!.y);
      }
    }
  });

  test('all cookbook shelf cards can be reached without widening the page', async ({ page }) => {
    await responsiveData(page, 'en');
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto('/');
    const shelf = page.getByRole('region', { name: 'Cookbooks', exact: true });
    const links = shelf.getByRole('link', { name: /^Open / });
    await expect(links).toHaveCount(6);
    await links.first().focus();
    for (const [index, link] of (await links.all()).entries()) {
      if (index > 0) await page.keyboard.press('Tab');
      await expect(link).toBeFocused();
      await expect(link).toBeInViewport({ ratio: 1 });
      await expectReflow(page);
    }
    expect(await shelf.locator('ul').evaluate((el) => el.scrollLeft)).toBeGreaterThan(0);
  });

  test('a cookbook form survives rotation and its content remains reachable', async ({ page }) => {
    await responsiveData(page, 'en');
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto('/cookbooks');
    await page.getByRole('button', { name: 'New cookbook', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'New cookbook', exact: true });
    const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
    await name.fill('A cookbook I am still writing');
    for (const size of [
      { width: 844, height: 390 },
      { width: 1280, height: 900 },
      { width: 320, height: 568 }
    ]) {
      await page.setViewportSize(size);
      await expect(name).toHaveValue('A cookbook I am still writing');
      await expect(dialog.getByRole('button', { name: 'Close', exact: true })).toBeInViewport({
        ratio: 1
      });
      const save = dialog.getByRole('button', { name: 'Make it', exact: true });
      await save.scrollIntoViewIfNeeded();
      await expect(save).toBeInViewport({ ratio: 1 });
      await expectReflow(page);
    }
  });

  test('ingredient popovers fit a phone and a short landscape viewport', async ({ page }) => {
    await responsiveData(page, 'en', { extraIngredients: 22 });
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto(`/recipes/${recipeId}/edit`);
    await page.getByRole('button', { name: 'Add an ingredient', exact: true }).first().click();
    for (const size of [
      { width: 320, height: 720 },
      { width: 844, height: 390 },
      { width: 320, height: 568 }
    ]) {
      await page.setViewportSize(size);
      const popover = page.locator(':popover-open');
      await expect(popover).toBeVisible();
      await expect(popover).toBeInViewport({ ratio: 1 });
      await expectReflow(page);
      const box = (await popover.boundingBox())!;
      expect(box.y).toBeGreaterThanOrEqual(0);
      expect(box.y + box.height).toBeLessThanOrEqual(size.height);
      await popover.getByRole('checkbox').last().scrollIntoViewIfNeeded();
      await expect(popover.getByRole('checkbox').last()).toBeInViewport({ ratio: 1 });
    }
    await page.keyboard.press('Escape');
    await page.addStyleTag({ content: 'html { font-size: 200%; }' });
    await page.getByRole('button', { name: 'Add an ingredient', exact: true }).first().click();
    await expect(page.locator(':popover-open')).toBeInViewport({ ratio: 1 });
    await expectReflow(page);
    const last = page.locator(':popover-open').getByRole('checkbox').last();
    await last.scrollIntoViewIfNeeded();
    await expect(last).toBeInViewport({ ratio: 1 });
  });

  test('enlarged text reflows the form, recipe and cookbook tools', async ({ page }) => {
    await responsiveData(page, 'en');
    for (const width of [320, 640, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      for (const path of [
        '/login',
        '/shopping',
        `/recipes/${recipeId}/edit`,
        `/cookbooks/${cookbookId}`
      ]) {
        await test.step(`${path} at ${width}px with 200% text`, async () => {
          await page.goto(path);
          await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
          await page.addStyleTag({ content: 'html { font-size: 200%; }' });
          await expectReflow(page);
        });
      }
    }
  });
});

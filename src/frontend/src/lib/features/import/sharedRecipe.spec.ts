import 'fake-indexeddb/auto';
import { beforeEach, expect, it, vi } from 'vitest';
import {
  forgetSharedRecipe,
  keepSharedRecipe,
  recallSharedRecipe,
  sharedAddress,
  validSharedPhotos
} from './sharedRecipe';

beforeEach(() => {
  vi.useRealTimers();
});

it('keeps the caption with its link and pages across a handoff and reload', async () => {
  const form = new FormData();
  form.set('text', '120 g beans\nFry. https://example.com/recipe');
  form.set('url', 'https://example.com/recipe');
  form.set('title', 'Beans');
  form.append('photos', new File(['image'], 'recipe.png', { type: 'image/png' }));
  const id = await keepSharedRecipe(form);
  const first = await recallSharedRecipe(id);
  expect(first?.text).toContain('120 g beans');
  expect(first?.photos).toHaveLength(1);
  expect(await recallSharedRecipe(id)).toEqual(first);
  await forgetSharedRecipe(id);
  expect(await recallSharedRecipe(id)).toBeNull();
});

it('expires abandoned recipes after a day', async () => {
  const form = new FormData();
  form.set('text', 'Beans');
  const id = await keepSharedRecipe(form);
  vi.spyOn(Date, 'now').mockReturnValue(Date.now() + 25 * 60 * 60 * 1000);
  expect(await recallSharedRecipe(id)).toBeNull();
  vi.restoreAllMocks();
});

it('rejects oversized and unsupported files before storing them', async () => {
  expect(validSharedPhotos([new File(['video'], 'recipe.mp4', { type: 'video/mp4' })])).toBe(false);
  const oversized = { type: 'image/png', size: 11 * 1024 * 1024 } as File;
  expect(validSharedPhotos([oversized])).toBe(false);
  const form = new FormData();
  form.append('photos', new File(['video'], 'recipe.mp4', { type: 'video/mp4' }));
  await expect(keepSharedRecipe(form)).rejects.toThrow('unsupported-media');
});

it('finds a shared URL inside a caption and refuses non-web addresses', () => {
  expect(sharedAddress('', 'Beans\nhttps://example.com/recipe')).toBe('https://example.com/recipe');
  expect(sharedAddress('javascript:alert(1)', '')).toBe('');
  expect(sharedAddress('//example.com', '')).toBe('');
});

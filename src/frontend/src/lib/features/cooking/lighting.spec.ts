import { beforeEach, expect, it, vi } from 'vitest';
import { createKitchenLighting } from './lighting.svelte';
beforeEach(() => localStorage.clear());
it('remembers a kitchen choice across app launches', () => {
  const first = createKitchenLighting();
  first.choose('oled');
  const second = createKitchenLighting();
  second.load();
  expect(second.mode).toBe('oled');
});
it('ignores invalid choices and unavailable storage', () => {
  localStorage.setItem('culina.kitchenLighting', 'invalid');
  const lighting = createKitchenLighting();
  lighting.load();
  lighting.choose('unexpected');
  expect(lighting.mode).toBe('normal');
  const blocked = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
    throw new Error('blocked');
  });
  expect(() => lighting.choose('glare')).not.toThrow();
  expect(lighting.mode).toBe('glare');
  blocked.mockRestore();
});

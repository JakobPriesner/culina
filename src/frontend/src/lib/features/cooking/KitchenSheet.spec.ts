import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';

import type { RecipeReading } from '$features/recipes/types';
import { renderWithProviders } from '$lib/test/render';

import KitchenSheet from './KitchenSheet.svelte';
import { speech } from './speech.svelte';

const recipe = { id: 'r1', title: 'Risotto' } as RecipeReading;

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
  };
  localStorage.clear();
  speech.setAutoRead(false);
  vi.stubGlobal('SpeechSynthesisUtterance', class {});
  vi.stubGlobal('speechSynthesis', { speak: vi.fn(), cancel: vi.fn(), getVoices: () => [] });
});

afterEach(() => vi.unstubAllGlobals());

it('turns auto-read on and remembers it', async () => {
  renderWithProviders(KitchenSheet, { props: { open: true, recipe, onclose: () => {} } });

  const toggle = screen.getByRole('switch', { name: 'Auto-read on advance' });
  expect(toggle.getAttribute('aria-checked')).toBe('false');

  await userEvent.click(toggle);

  expect(toggle.getAttribute('aria-checked')).toBe('true');
  expect(localStorage.getItem('culina.speech.autoRead')).toBe('true');
});

it('leaves the toggle out where the browser cannot speak', () => {
  vi.stubGlobal('speechSynthesis', undefined);

  renderWithProviders(KitchenSheet, { props: { open: true, recipe, onclose: () => {} } });

  expect(screen.queryByRole('switch', { name: 'Auto-read on advance' })).toBeNull();
});

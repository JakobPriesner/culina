import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import Greeting from './Greeting.svelte';
import { renderWithProviders } from './render';

/* Proves the harness: a component renders, is queried by role and accessible name (never CSS class), and module state is reset between tests. */
describe('the test harness', () => {
  it('renders a component with a theme applied', () => {
    renderWithProviders(Greeting);

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Culina');
    expect(document.documentElement.dataset['theme']).toBe('warm-paper');
    expect(document.documentElement.dataset['mode']).toBe('light');
  });

  it('renders in dark mode when asked', () => {
    renderWithProviders(Greeting, { mode: 'dark' });

    expect(document.documentElement.dataset['mode']).toBe('dark');
  });
});

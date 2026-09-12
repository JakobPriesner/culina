import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import Page from '../../routes/+page.svelte';
import { renderWithProviders } from './render';

/*
 * Proves the harness itself works: a component renders, it is queried the way
 * every other suite must query it — by role and accessible name, never by CSS
 * class — and module state is put back between tests.
 */
describe('the test harness', () => {
  it('renders a route component with a theme applied', () => {
    renderWithProviders(Page);

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Culina');
    expect(document.documentElement.dataset['theme']).toBe('warm-paper');
    expect(document.documentElement.dataset['mode']).toBe('light');
  });

  it('renders in dark mode when asked', () => {
    renderWithProviders(Page, { mode: 'dark' });

    expect(document.documentElement.dataset['mode']).toBe('dark');
  });
});

import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import ImportProgress from './ImportProgress.svelte';
import type { ImportRun } from './types';
import { renderWithProviders } from '$lib/test/render';

/*
 * The end of the flow, which is where the whole design either lands or does
 * not: four hundred recipes arriving into a library is invisible, and the
 * cookbook is what makes it something somebody can go and look at.
 */
const run = (over: Partial<ImportRun> = {}): ImportRun => ({
  total: 40,
  done: 40,
  imported: 38,
  skipped: 1,
  failures: ['Oma’s Kuchen'],
  cookbookId: 'cb1',
  cookbookName: 'recipes.example.com · 17 September 2026',
  finished: true,
  ...over
});

describe('an import as it runs', () => {
  it('counts in recipes, not in requests', () => {
    renderWithProviders(ImportProgress, {
      props: { run: run({ done: 15, finished: false }), ondone: () => {} }
    });

    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuetext', '15 of 40');
  });

  it('leaves the way out until it has finished', () => {
    renderWithProviders(ImportProgress, {
      props: { run: run({ done: 15, finished: false }), ondone: () => {} }
    });

    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('ends at the cookbook everything landed on', () => {
    renderWithProviders(ImportProgress, { props: { run: run(), ondone: () => {} } });

    expect(screen.getByRole('link', { name: /Open recipes\.example\.com/ })).toHaveAttribute(
      'href',
      '/cookbooks/cb1'
    );
  });

  it('names what could not be read, rather than counting it', () => {
    renderWithProviders(ImportProgress, { props: { run: run(), ondone: () => {} } });

    // Twelve that failed is a statistic; twelve titles is a list somebody can
    // act on.
    expect(screen.getByText('Oma’s Kuchen')).toBeInTheDocument();
  });

  it('does not call what was already here a failure', () => {
    renderWithProviders(ImportProgress, { props: { run: run(), ondone: () => {} } });

    expect(screen.getByText('Already had')).toBeInTheDocument();
    expect(screen.getByText('Brought over')).toBeInTheDocument();
  });
});

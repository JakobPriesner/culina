import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import ImportProgress from './ImportProgress.svelte';
import type { AppError } from '$api';
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
  held: [],
  cookbookId: 'cb1',
  cookbookName: 'recipes.example.com · 17 September 2026',
  finished: true,
  lost: null,
  ...over
});

/** What the stream said when it stopped, in the shape the API layer reports. */
const lost = (code: string, detail: string): AppError => ({
  code,
  detail,
  status: 404,
  requestId: 'abc123',
  fields: [],
  retryAfterSeconds: null
});

describe('an import as it runs', () => {
  it('counts in recipes, not in requests', () => {
    renderWithProviders(ImportProgress, {
      props: {
        run: run({ done: 15, finished: false }),
        ondone: () => {},
        onlook: () => {},
        onanyway: () => {}
      }
    });

    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuetext', '15 of 40');
  });

  it('offers the way out while it is still going', () => {
    renderWithProviders(ImportProgress, {
      props: {
        run: run({ done: 15, finished: false }),
        ondone: () => {},
        onlook: () => {},
        onanyway: () => {}
      }
    });

    // The import belongs to the server, so watching it is optional — and the
    // shelf it is filling exists from the first second.
    expect(screen.getByRole('link', { name: /Open recipes\.example\.com/ })).toHaveAttribute(
      'href',
      '/cookbooks/cb1'
    );
  });

  it('says a dropped stream is a dropped stream, not a finished import', () => {
    renderWithProviders(ImportProgress, {
      props: {
        run: run({
          done: 15,
          finished: false,
          lost: lost('import.import_not_found', 'That import is no longer being followed.')
        }),
        ondone: () => {},
        onlook: () => {}
      }
    });

    expect(screen.getByRole('button', { name: 'Check again' })).toBeInTheDocument();
    expect(screen.queryByText('Import complete')).not.toBeInTheDocument();
  });

  it('quotes why it stopped, because the reasons need different reactions', () => {
    renderWithProviders(ImportProgress, {
      props: {
        run: run({
          done: 15,
          finished: false,
          lost: lost('import.import_not_found', 'That import is no longer being followed.')
        }),
        ondone: () => {},
        onlook: () => {}
      }
    });

    // "The connection went" is worth waiting through; "that import is gone" is
    // not, and a screen that says only "something stopped" cannot tell anybody
    // which of the two they are looking at.
    expect(screen.getByText(/That import is no longer being followed/)).toBeInTheDocument();
    expect(screen.getByText(/abc123/)).toBeInTheDocument();
  });

  it('ends at the cookbook everything landed on', () => {
    renderWithProviders(ImportProgress, {
      props: { run: run(), ondone: () => {}, onlook: () => {}, onanyway: () => {} }
    });

    expect(screen.getByRole('link', { name: /Open recipes\.example\.com/ })).toHaveAttribute(
      'href',
      '/cookbooks/cb1'
    );
  });

  it('names what could not be read, rather than counting it', () => {
    renderWithProviders(ImportProgress, {
      props: { run: run(), ondone: () => {}, onlook: () => {}, onanyway: () => {} }
    });

    // Twelve that failed is a statistic; twelve titles is a list somebody can
    // act on.
    expect(screen.getByText('Oma’s Kuchen')).toBeInTheDocument();
  });

  it('does not call what was already here a failure', () => {
    renderWithProviders(ImportProgress, {
      props: { run: run(), ondone: () => {}, onlook: () => {}, onanyway: () => {} }
    });

    expect(screen.getByText('Already imported')).toBeInTheDocument();
    expect(screen.getByText('Imported')).toBeInTheDocument();
  });
});

describe('recipes held back because one like them is already here', () => {
  const held = run({
    held: [
      {
        externalId: '7',
        title: 'Spaghetti Bolognese',
        recipeId: 'r-mine',
        looksLike: { title: 'Spaghetti Bolognese', sharedIngredients: 4, cookCount: 12 }
      },
      {
        externalId: '9',
        title: 'Omas Käsekuchen',
        recipeId: 'r-cake',
        looksLike: { title: 'Käsekuchen', sharedIngredients: 0, cookCount: 0 }
      }
    ]
  });

  it('says what each looks like, and lets it be compared with the one already here', () => {
    renderWithProviders(ImportProgress, {
      props: { run: held, ondone: () => {}, onlook: () => {}, onanyway: () => {} }
    });

    expect(
      screen.getByText(/Looks like “Spaghetti Bolognese” · Made 12× · 4 ingredients in common/)
    ).toBeInTheDocument();
    // Nothing to count is nothing said: no "Made 0×", no "0 in common".
    expect(screen.getByText(/^Looks like “Käsekuchen”/)).toBeInTheDocument();
    expect(screen.queryByText(/Made 0×/)).not.toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: 'Compare' })[0]).toHaveAttribute(
      'href',
      '/recipes/r-mine'
    );
  });

  it('brings nothing over until somebody chooses', async () => {
    const onanyway = vi.fn();

    renderWithProviders(ImportProgress, {
      props: { run: held, ondone: () => {}, onlook: () => {}, onanyway }
    });

    // Never automatic: a household may want two Bolognese, but only somebody
    // who has looked at both can say so.
    expect(screen.getByRole('checkbox', { name: 'Spaghetti Bolognese' })).not.toBeChecked();
    expect(screen.getByRole('button', { name: /anyway/ })).toBeDisabled();

    await userEvent.click(screen.getByRole('checkbox', { name: 'Omas Käsekuchen' }));
    await userEvent.click(screen.getByRole('button', { name: 'Import 1 anyway' }));

    expect(onanyway).toHaveBeenCalledWith(['9']);
  });
});

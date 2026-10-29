import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';

import ArrangeCookbookSheet from './ArrangeCookbookSheet.svelte';
import { moveLine } from './shelfOrder';
import { cookbooks } from './stores/cookbooks.svelte';

const detail = (version: number) => ({
  cookbookId: 'c1',
  householdId: 'h1',
  name: 'Sonntag',
  kind: 'manual',
  recipeCount: 3,
  coverRecipeIds: [],
  coverPictures: [],
  createdBy: 'u1',
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
  version
});

const recipe = (recipeId: string, title: string) => ({
  recipeId,
  householdId: 'h1',
  title,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  updatedAt: '2026-10-01T00:00:00Z'
});

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      'Content-Type': status < 400 ? 'application/json' : 'application/problem+json'
    }
  });

/** A shelf of three, read in that order; the order PUT answers as `answer` says. */
function server(answer: 'saves' | 'stale', shelves: string[][] = [['Braten', 'Suppe', 'Kuchen']]) {
  const sent: { body: unknown; ifMatch: string | null }[] = [];
  let read = 0;

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      const url = new URL(input.url);

      if (input.method === 'PUT') {
        sent.push({ body: await input.json(), ifMatch: input.headers.get('If-Match') });

        return answer === 'saves'
          ? json(detail(4))
          : json({ title: 'x', status: 412, code: 'request.version_mismatch' }, 412);
      }

      if (url.pathname === '/api/v1/cookbooks/c1') return json(detail(3));

      const titles = shelves[Math.min(read++, shelves.length - 1)]!;

      return json({
        items: titles.map((title) => recipe(`r-${title}`, title)),
        total: titles.length,
        nextCursor: null
      });
    })
  );

  return sent;
}

const titles = () =>
  screen
    .getAllByRole('listitem')
    .map((item) => item.textContent?.replace(/\s+/g, ' ').trim().replace(/^\d+ /, ''));

beforeEach(async () => {
  cookbooks.reset();
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

describe('arranging a cookbook', () => {
  it('moves a recipe with its buttons, keeps focus on it, and saves the whole order at the version read', async () => {
    const sent = server('saves');
    await cookbooks.load('c1');
    const onarranged = vi.fn();
    const onclose = vi.fn();
    const user = userEvent.setup();

    renderWithProviders(ArrangeCookbookSheet, {
      props: { open: true, householdId: 'h1', cookbookId: 'c1', onclose, onarranged }
    });

    await screen.findByText('Kuchen');
    expect(screen.getByRole('button', { name: 'Save order' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Move Kuchen up' }));
    expect(titles()).toEqual(['Braten', 'Kuchen', 'Suppe']);
    expect(screen.getByRole('button', { name: 'Move Kuchen up' })).toHaveFocus();
    expect(screen.getByText('Kuchen, now 2 of 3')).toBeInTheDocument();

    // At the top the way up is closed, so focus goes to the way back down.
    await user.keyboard('{Enter}');
    expect(titles()).toEqual(['Kuchen', 'Braten', 'Suppe']);
    expect(screen.getByRole('button', { name: 'Move Kuchen up' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Move Kuchen down' })).toHaveFocus();

    await user.click(screen.getByRole('button', { name: 'Save order' }));

    await waitFor(() => expect(onarranged).toHaveBeenCalled());
    expect(sent).toEqual([
      { body: { recipeIds: ['r-Kuchen', 'r-Braten', 'r-Suppe'] }, ifMatch: '"v3"' }
    ]);
    expect(onclose).toHaveBeenCalled();
    expect(cookbooks.open?.version).toBe(4);
  });

  it('shows the shelf as it is now when somebody changed it meanwhile', async () => {
    server('stale', [
      ['Braten', 'Suppe'],
      ['Braten', 'Suppe', 'Neu']
    ]);
    await cookbooks.load('c1');
    const onarranged = vi.fn();
    const user = userEvent.setup();

    renderWithProviders(ArrangeCookbookSheet, {
      props: { open: true, householdId: 'h1', cookbookId: 'c1', onclose: vi.fn(), onarranged }
    });

    await user.click(await screen.findByRole('button', { name: 'Move Suppe up' }));
    await user.click(screen.getByRole('button', { name: 'Save order' }));

    expect(await screen.findByText(/Somebody changed this cookbook meanwhile/)).toBeInTheDocument();
    await waitFor(() => expect(titles()).toEqual(['Braten', 'Suppe', 'Neu']));
    expect(onarranged).not.toHaveBeenCalled();
  });
});

describe('moving one line', () => {
  it('swaps with its neighbour, and stays put at either end', () => {
    expect(moveLine(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b']);
    expect(moveLine(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c']);

    const lines = ['a', 'b'];

    expect(moveLine(lines, 0, -1)).toBe(lines);
    expect(moveLine(lines, 1, 1)).toBe(lines);
  });
});

import { fireEvent, screen, waitFor, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import SearchOverlay from './SearchOverlay.svelte';
import { resetAllStores } from '$shell/stores';
import { renderWithProviders } from '#lib/test/render.js';

/* The keys of the one search field (focus never leaves it) and the offer to keep a search as a cookbook. */
const goto = vi.hoisted(() => vi.fn());

vi.mock('$app/navigation', () => ({ goto }));

const summary = (id: string, title: string) => ({
  recipeId: id,
  householdId: 'h1',
  title,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  updatedAt: '2026-09-12T00:00:00Z'
});

const lentils = [summary('r1', 'Lentil soup'), summary('r2', 'Lentil salad')];

/** In the order the list groups them, so walking with the arrows walks down the screen. */
const lentilCompletions = [
  { kind: 'recipe', label: 'Lentil dal', recipeId: 'r9' },
  { kind: 'ingredient', label: 'Lentils', recipeCount: 3 },
  { kind: 'tag', label: 'Soup', slug: 'soup', recipeCount: 2 }
];

const chip = (kind: string, value: string, text: string, start: number) => ({
  kind,
  value,
  text,
  start,
  end: start + text.length
});

/** One small household: lentil recipes, a soup tag, a pasta that a time limit can find and a dish only a diet describes. */
function recipesFor(url: URL) {
  const query = url.searchParams.get('query') ?? '';

  if (url.searchParams.getAll('tag').includes('soup') || query.startsWith('lent')) {
    return { items: lentils, total: 2 };
  }

  if (query === 'pasta under 20 minutes') {
    return {
      items: [summary('r3', 'Pasta al limone')],
      total: 1,
      interpretation: {
        freeText: 'pasta',
        applied: [chip('time', '20', 'under 20 minutes', 6)]
      }
    };
  }

  if (query === 'vegetarian') {
    return {
      items: [summary('r4', 'Ratatouille')],
      total: 1,
      interpretation: { freeText: '', applied: [chip('diet', 'vegetarian', 'vegetarian', 0)] }
    };
  }

  return { items: [], total: 0 };
}

const json = (body: unknown) =>
  Promise.resolve(
    new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    })
  );

beforeEach(() => {
  resetAllStores();
  localStorage.clear();
  goto.mockClear();

  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      const url = new URL(input.url);
      const query = (url.searchParams.get('query') ?? '').trim().toLowerCase();

      switch (url.pathname) {
        case '/api/v1/recipes':
          return json({ nextCursor: null, ...recipesFor(url) });
        case '/api/v1/households/h1/completions':
          return json({ items: query.startsWith('lent') ? lentilCompletions : [] });
        case '/api/v1/cookbooks':
          return json({ items: [], nextCursor: null, total: 0 });
        case '/api/v1/households/h1/shopping-list':
          return json({ listId: 'l1', items: [], version: 1 });
        case '/api/v1/tags':
          return json({ items: [] });
        default:
          return Promise.resolve(new Response(null, { status: 404 }));
      }
    })
  );

  // jsdom has no top layer, so showModal is the open state; nor scrollIntoView, which the arrow keys call.
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };

  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };

  Element.prototype.scrollIntoView = vi.fn();
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  delete (Element.prototype as Partial<Element>).scrollIntoView;
});

function renderOverlay() {
  const onclose = vi.fn();

  renderWithProviders(SearchOverlay, {
    props: { open: true, householdId: 'h1', userId: 'u1', onclose }
  });

  return { onclose, field: screen.getByRole('combobox', { name: 'Search recipes' }) };
}

/** Types into the field and waits until both the completions and the results are listed. */
async function search(text: string) {
  const user = userEvent.setup();
  const rendered = renderOverlay();

  await user.type(rendered.field, text);

  await screen.findByRole('option', { name: /Lentil dal/ });
  await screen.findByRole('option', { name: /Lentil salad/ });

  return { user, ...rendered };
}

const selected = () => screen.getByRole('option', { selected: true });

describe('the search overlay', () => {
  describe('arrow keys', () => {
    it('walk the options while the field keeps focus, wrapping at either end', async () => {
      const { user, field } = await search('lentil');

      expect(screen.queryByRole('option', { selected: true })).not.toBeInTheDocument();

      await user.keyboard('{ArrowDown}');

      expect(selected()).toHaveAccessibleName(/Lentil dal/);
      expect(field).toHaveAttribute('aria-activedescendant', selected().id);
      expect(field).toHaveFocus();

      await user.keyboard('{ArrowUp}');

      expect(selected()).toHaveAccessibleName(/Lentil salad/);

      await user.keyboard('{ArrowDown}');

      expect(selected()).toHaveAccessibleName(/Lentil dal/);
    });

    it('start at the last option when going up from nothing', async () => {
      const { user } = await search('lentil');
      const options = screen.getAllByRole('option');

      await user.keyboard('{ArrowUp}');

      expect(selected()).toBe(options.at(-1));
    });
  });

  describe('Enter', () => {
    it('searches for what was typed and remembers it as a recent search', async () => {
      const user = userEvent.setup();
      const { field } = renderOverlay();

      await user.type(field, 'lentil{Enter}');

      expect(await screen.findByRole('option', { name: /Lentil salad/ })).toBeInTheDocument();

      await user.keyboard('{Escape}');

      expect(screen.getByRole('heading', { name: 'Recent searches' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '↩ lentil' })).toBeInTheDocument();
    });

    it('opens the highlighted recipe and closes the search', async () => {
      const { user, onclose } = await search('lentil');

      // Down to the first option, then round to the last: the second result.
      await user.keyboard('{ArrowDown}{ArrowUp}{Enter}');

      await waitFor(() => expect(goto).toHaveBeenCalledWith('/recipes/r2'));
      expect(onclose).toHaveBeenCalledOnce();
    });

    it('opens the highlighted recipe in a new tab with Ctrl held, leaving the search open', async () => {
      const opened = vi.spyOn(window, 'open').mockReturnValue(null);
      const { user, onclose } = await search('lentil');

      await user.keyboard('{ArrowDown}{ArrowUp}{Control>}{Enter}{/Control}');

      expect(opened).toHaveBeenCalledWith('/recipes/r2', '_blank', 'noopener');
      expect(goto).not.toHaveBeenCalled();
      expect(onclose).not.toHaveBeenCalled();
    });

    it('goes to a highlighted page of the app', async () => {
      const user = userEvent.setup();
      const { field, onclose } = renderOverlay();

      await user.type(field, 'shopping');
      await screen.findByRole('option', { name: /Shopping/ });
      await user.keyboard('{ArrowDown}{Enter}');

      expect(onclose).toHaveBeenCalledOnce();
      expect(goto).toHaveBeenCalledWith('/shopping');
    });
  });

  describe('the offer after finding nothing', () => {
    it.each(['New recipe', 'Import'])('closes the search when "%s" is followed', async (name) => {
      const user = userEvent.setup();
      const { field, onclose } = renderOverlay();

      await user.type(field, 'zzzxxyyqq');
      const link = await screen.findByRole('link', { name });
      // jsdom cannot navigate; the page's own router would, and this sheet has to be gone by then.
      document.addEventListener('click', (event) => event.preventDefault(), { once: true });
      await user.click(link);

      expect(onclose).toHaveBeenCalledOnce();
    });
  });

  describe('Tab', () => {
    it('types out the first completion when none is highlighted, without opening the recipe', async () => {
      const { user, field } = await search('lent');

      await user.keyboard('{Tab}');

      expect(field).toHaveValue('Lentil dal');
      expect(field).toHaveFocus();
      expect(goto).not.toHaveBeenCalled();
    });

    it('takes the highlighted completion', async () => {
      const { user, field } = await search('lent');

      await user.keyboard('{ArrowDown}{ArrowDown}{Tab}');

      expect(field).toHaveValue('Lentils ');
    });

    it('is left alone when there is nothing to complete', async () => {
      const { field } = renderOverlay();

      expect(await fireEvent.keyDown(field, { key: 'Tab' })).toBe(true);
    });
  });

  describe('Backspace', () => {
    it('edits the text first, then takes off the last tag', async () => {
      const { user, field } = await search('lentil');

      // The tag completion, taken: the word becomes a tag.
      await user.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}{Enter}');

      const tag = await screen.findByRole('button', { name: 'Remove “Soup”' });

      expect(field).toHaveValue('');

      await user.type(field, 'x{Backspace}');

      expect(field).toHaveValue('');
      expect(tag).toBeInTheDocument();

      await user.keyboard('{Backspace}');

      expect(screen.queryByRole('button', { name: 'Remove “Soup”' })).not.toBeInTheDocument();
    });
  });

  describe('Escape', () => {
    it('clears the field first rather than closing the search', async () => {
      const { user, field, onclose } = await search('lentil');

      await user.keyboard('{Escape}');

      expect(field).toHaveValue('');
      expect(screen.queryByRole('option')).not.toBeInTheDocument();
      expect(screen.getByText('Quick access')).toBeInTheDocument();
      expect(onclose).not.toHaveBeenCalled();
    });

    it('is left to the sheet to close the search once the field is empty', async () => {
      const { field } = renderOverlay();

      expect(await fireEvent.keyDown(field, { key: 'Escape' })).toBe(true);
    });
  });

  describe('keeping a search as a cookbook', () => {
    it('is offered when a cookbook could ask for some of it, naming what it could not', async () => {
      const user = userEvent.setup();
      const { field } = renderOverlay();

      await user.type(field, 'pasta under 20 minutes');

      await user.click(await screen.findByRole('button', { name: 'Save as a cookbook' }));

      expect(
        screen.getByText('A cookbook cannot ask for “pasta”; the rest comes along.')
      ).toBeInTheDocument();

      const sheet = await screen.findByRole('dialog', { name: 'New cookbook' });

      expect(within(sheet).getByRole('textbox', { name: /Name/ })).toHaveValue(
        'pasta under 20 minutes'
      );
    });

    it('is not offered when a cookbook could ask for none of it', async () => {
      const user = userEvent.setup();
      const { field } = renderOverlay();

      await user.type(field, 'vegetarian');

      expect(await screen.findByText('1 recipe')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Save as a cookbook' })).not.toBeInTheDocument();
    });
  });
});

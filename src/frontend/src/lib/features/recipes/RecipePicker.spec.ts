import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import RecipePicker from './RecipePicker.svelte';
import { recipes } from './stores/recipes.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * Two pages now ask the same question through this, so what has to be right is
 * the asking: it searches only while it is up, it does not search per
 * keystroke, and it hands back the recipe rather than an id — the caller needs
 * the yield to know how much to buy.
 */
const summary = (id: string, title: string) => ({
  recipeId: id,
  title,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  updatedAt: '2026-09-12T00:00:00Z',
  ingredientMatch: null
});

/** Every list request the picker made, by the URL it asked for. */
let asked: string[] = [];

function serverHas(...items: ReturnType<typeof summary>[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      asked.push(input.url);

      return Promise.resolve(
        new Response(JSON.stringify({ items, nextCursor: null, total: items.length }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      );
    })
  );
}

/* jsdom has <dialog> but not the top layer, so showModal is the open state. */
beforeEach(() => {
  asked = [];
  recipes.reset();

  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };

  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };

  serverHas(summary('r1', 'Orzo'), summary('r2', 'Lentil soup'));
});

afterEach(() => vi.unstubAllGlobals());

const props = (over: Record<string, unknown> = {}) => ({
  open: true,
  householdId: 'h1',
  title: 'Which recipe?',
  onpick: vi.fn(),
  onclose: vi.fn(),
  ...over
});

describe('the recipe picker', () => {
  it('lists the household recipes once it is open', async () => {
    renderWithProviders(RecipePicker, { props: props() });

    expect(await screen.findByRole('button', { name: /Orzo/ })).toBeInTheDocument();
  });

  it('asks for nothing while it is closed', async () => {
    renderWithProviders(RecipePicker, { props: props({ open: false }) });

    // Long enough for a request to have been made had one been going to be.
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(asked).toEqual([]);
  });

  it('hands back the whole recipe, because the caller needs its yield', async () => {
    const onpick = vi.fn();

    renderWithProviders(RecipePicker, { props: props({ onpick }) });

    await userEvent.click(await screen.findByRole('button', { name: /Orzo/ }));

    expect(onpick).toHaveBeenCalledOnce();
    expect(onpick.mock.calls[0]![0]).toMatchObject({ id: 'r1', yieldAmount: 4 });
  });

  it('waits for a pause in the typing rather than searching per keystroke', async () => {
    vi.useFakeTimers();

    try {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      renderWithProviders(RecipePicker, { props: props() });

      await vi.waitFor(() => expect(asked).toHaveLength(1));

      await user.type(screen.getByRole('searchbox'), 'soup');

      // Still the one request the opening made: four keystrokes are not four
      // searches.
      expect(asked).toHaveLength(1);

      await vi.advanceTimersByTimeAsync(250);

      await vi.waitFor(() => expect(asked).toHaveLength(2));
      expect(asked[1]).toContain('soup');
    } finally {
      vi.useRealTimers();
    }
  });

  it('says so when a search matches nothing', async () => {
    serverHas();

    renderWithProviders(RecipePicker, { props: props() });

    expect(await screen.findByText('No recipe matches that search.')).toBeInTheDocument();
  });

  /*
   * A caller that keeps the sheet open across several picks — the shopping
   * list does — has no other way of saying which recipes already went on, and
   * after four searches the rows are the only place that can be read.
   */
  it('marks the recipes the caller says it has already taken', async () => {
    renderWithProviders(RecipePicker, { props: props({ taken: ['r1'] }) });

    expect(await screen.findByRole('button', { name: /Orzo.*Added/ })).toBeInTheDocument();

    // And only those: the other row still says what it is.
    expect(screen.getByRole('button', { name: /Lentil soup/ })).not.toHaveTextContent('Added');
  });

  /*
   * A cookbook is the other kind of caller: a recipe is on the shelf or it is
   * not, and a row that looked like an ordinary add for one that is already on
   * made curating a hundred recipes a matter of tapping and reading toasts.
   */
  describe('for a caller that can take a recipe back', () => {
    it('says which rows are already on before any of them is touched', async () => {
      renderWithProviders(RecipePicker, { props: props({ taken: ['r1'], onremove: vi.fn() }) });

      expect(
        await screen.findByRole('button', { name: /Orzo/, pressed: true })
      ).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: /Lentil soup/, pressed: false })
      ).toBeInTheDocument();
    });

    it('takes a row that is on back off, rather than adding it again', async () => {
      const onpick = vi.fn();
      const onremove = vi.fn();

      renderWithProviders(RecipePicker, { props: props({ taken: ['r1'], onpick, onremove }) });

      await userEvent.click(await screen.findByRole('button', { name: /Orzo/ }));

      expect(onremove).toHaveBeenCalledOnce();
      expect(onremove.mock.calls[0]![0]).toMatchObject({ id: 'r1' });
      expect(onpick).not.toHaveBeenCalled();
    });

    it('still adds a row that is off', async () => {
      const onpick = vi.fn();

      renderWithProviders(RecipePicker, {
        props: props({ taken: ['r1'], onpick, onremove: vi.fn() })
      });

      await userEvent.click(await screen.findByRole('button', { name: /Lentil soup/ }));

      expect(onpick).toHaveBeenCalledOnce();
    });
  });

  it('leaves every row an ordinary pick for a caller that cannot take one back', async () => {
    // The week plan: cooking the same thing twice is planning it twice.
    renderWithProviders(RecipePicker, { props: props({ taken: ['r1'] }) });

    const orzo = await screen.findByRole('button', { name: /Orzo/ });

    expect(orzo).not.toHaveAttribute('aria-pressed');
  });

  /*
   * The plan passes `open` as an expression rather than a binding, so the only
   * way it hears about a dismissal is this callback. Without it the page goes
   * on believing the sheet is up and will not open it again.
   */
  it('tells the caller when it is closed from the visible button', async () => {
    const onclose = vi.fn();

    renderWithProviders(RecipePicker, { props: props({ onclose }) });

    await userEvent.click(await screen.findByRole('button', { name: 'Close' }));

    expect(onclose).toHaveBeenCalledOnce();
  });

  /*
   * The sheet opens over a page that is itself showing a list — a cookbook, or
   * the collection — and that page reads the shared store. A picker searching
   * into it would empty the page behind the open sheet and leave the collection
   * filtered after it closed.
   */
  it('searches into its own list rather than the one the page behind it is reading', async () => {
    renderWithProviders(RecipePicker, { props: props() });

    await screen.findByRole('button', { name: /Orzo/ });

    expect(recipes.items).toHaveLength(0);
    expect(recipes.status).toBe('idle');
  });

  /* A caller looking inside one cookbook searches only inside it. */
  it('narrows to a cookbook when the caller is in one', async () => {
    renderWithProviders(RecipePicker, { props: props({ cookbookId: 'c1' }) });

    await screen.findByRole('button', { name: /Orzo/ });

    expect(asked.some((url) => url.includes('cookbookId=c1'))).toBe(true);
  });
});

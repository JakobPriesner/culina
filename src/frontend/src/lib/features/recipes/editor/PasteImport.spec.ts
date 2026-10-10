import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import PasteImport from './PasteImport.svelte';
import type { ParsedRecipe } from './parseRecipeText';
import { renderWithProviders } from '$lib/test/render';

/* An external link (share sheet, /recipes/new?url=…) is filled in and left alone: reading makes the server fetch it, so only a button tap may start that. */
const imports = '/api/v1/recipe-imports';

const urlOf = (input: unknown) =>
  input instanceof Request
    ? new URL(input.url).pathname
    : new URL(String(input), document.baseURI).pathname;

function serverAnswers(page: Record<string, unknown> = {}) {
  const fetched = vi.fn((input: unknown) =>
    Promise.resolve(
      new Response(
        JSON.stringify(
          urlOf(input) === imports
            ? {
                sourceUrl: 'https://example.com/beans',
                title: 'Beans',
                ingredientLines: ['120 g beans'],
                steps: ['Fry the beans.'],
                servings: null,
                totalMinutes: null,
                text: null,
                ...page
              }
            : { own: [] }
        ),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    )
  );

  vi.stubGlobal('fetch', fetched);

  return () => fetched.mock.calls.filter(([input]) => urlOf(input) === imports).length;
}

const settle = async () => {
  for (let turn = 0; turn < 6; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

const render = (onimport: (recipe: ParsedRecipe) => void = () => {}) =>
  renderWithProviders(PasteImport, {
    props: {
      householdId: 'h1',
      onimport,
      open: true,
      initialUrl: 'https://attacker.example/x'
    }
  });

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('a link that arrived from outside', () => {
  it('is filled in without being read', async () => {
    const reads = serverAnswers();

    render();
    await settle();

    expect(screen.getByRole('textbox', { name: 'A link to a recipe' })).toHaveValue(
      'https://attacker.example/x'
    );
    expect(screen.getByRole('status')).toHaveTextContent('Tap “Import recipe” to read it.');
    expect(reads()).toBe(0);
  });

  it('is read once somebody taps the button beside it', async () => {
    const reads = serverAnswers();

    render();
    await settle();
    await userEvent.click(screen.getByRole('button', { name: 'Import recipe' }));
    await settle();

    expect(reads()).toBe(1);
    expect(screen.queryByText('Tap “Import recipe” to read it.', { exact: false })).toBeNull();
  });
});

describe('a page that says what it makes', () => {
  it('hands over "12 Muffins" as pieces with their word, not as 12 servings', async () => {
    serverAnswers({ servings: 12, yieldKind: 'pieces', yieldLabel: 'Muffins' });
    // jsdom has no modal dialogs.
    HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };

    const imported: ParsedRecipe[] = [];

    render((recipe) => void imported.push(recipe));
    await settle();
    await userEvent.click(screen.getByRole('button', { name: 'Import recipe' }));
    await settle();
    await userEvent.click(screen.getByRole('button', { name: /^Review/ }));
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Save and edit recipe' }));
    await settle();

    expect(imported[0]).toMatchObject({ servings: 12, yieldKind: 'pieces', yieldLabel: 'Muffins' });
  });
});

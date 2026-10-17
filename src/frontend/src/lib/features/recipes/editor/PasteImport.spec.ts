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

    const imported: ParsedRecipe[] = [];

    render((recipe) => void imported.push(recipe));
    await settle();
    await userEvent.click(screen.getByRole('button', { name: 'Import recipe' }));
    await settle();
    // The preview on the page is the review: saving is one tap, with no second read-only copy.
    await userEvent.click(screen.getByRole('button', { name: 'Save and edit recipe' }));
    await settle();

    expect(imported[0]).toMatchObject({ servings: 12, yieldKind: 'pieces', yieldLabel: 'Muffins' });
  });
});

describe('a recipe pasted as text with its yield and time on lines of their own', () => {
  it('previews them as what the recipe makes and takes, and only the method as steps', async () => {
    serverAnswers();
    renderWithProviders(PasteImport, {
      props: {
        householdId: 'h1',
        onimport: () => {},
        open: true,
        initialText: [
          'Tomatenpasta',
          'Für 2 Portionen',
          'Zubereitung: 20 Minuten',
          'Zutaten',
          '200 g Spaghetti',
          '400 g Tomaten',
          '1 EL Olivenöl',
          'Zubereitung',
          '1. Spaghetti kochen.',
          '2. Tomaten in Olivenöl köcheln.',
          '3. Alles mischen.'
        ].join('\n')
      }
    });
    await settle();

    expect(screen.getByText('2 servings · 20 min')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem').filter((item) => item.closest('ol'))).toHaveLength(3);
  });
});

describe('the door it was opened by', () => {
  it('puts the photos first and focused when opened to photograph a recipe', async () => {
    serverAnswers();
    // jsdom has no layout to scroll.
    Element.prototype.scrollIntoView = () => {};
    renderWithProviders(PasteImport, {
      props: {
        householdId: 'h1',
        onimport: () => {},
        ondraft: () => {},
        intent: 'photo'
      }
    });
    await settle();

    const picker = screen.getByLabelText('Screenshots or recipe photos');
    const link = screen.getByRole('textbox', { name: 'A link to a recipe' });

    expect(picker).toHaveFocus();
    expect(picker.compareDocumentPosition(link) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
});

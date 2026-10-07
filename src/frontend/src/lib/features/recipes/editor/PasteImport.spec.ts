import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import PasteImport from './PasteImport.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * A link that arrives from outside — the share sheet, or any page linking to
 * /recipes/new?url=… — is filled in and left alone. Reading it makes the
 * server fetch the address, so only a person tapping the button may start
 * that, never the arrival itself.
 */
const imports = '/api/v1/recipe-imports';

const urlOf = (input: unknown) =>
  input instanceof Request
    ? new URL(input.url).pathname
    : new URL(String(input), document.baseURI).pathname;

function serverAnswers() {
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
                text: null
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

const render = () =>
  renderWithProviders(PasteImport, {
    props: {
      householdId: 'h1',
      onimport: () => {},
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

import { beforeEach, describe, expect, it, vi } from 'vitest';

import { session } from '$features/auth/session.svelte';
import { resetAllStores } from '$shell/stores';
import { renderWithProviders } from '$lib/test/render';

import OtherWaysIn from './OtherWaysIn.svelte';

/* On an iPhone a recipe cannot be shared into the app, so a copied link or a photo of the page is how most arrive: those doors come first. */
async function signedInWithAssistant() {
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            userId: 'u1',
            email: 'ada@example.com',
            displayName: 'Ada',
            isAdmin: false,
            createdAt: '2026-01-01T00:00:00Z',
            version: 1,
            assistance: { improve: false, draft: true, read: true, draw: false },
            households: [{ householdId: 'h1', name: 'Home', role: 'owner', inheritsFrom: [] }]
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        )
      )
    )
  );

  return session.refresh();
}

beforeEach(() => {
  resetAllStores();
  vi.unstubAllGlobals();
});

describe('the other ways into a new recipe', () => {
  it('offers a link or text first, a photo second, then the app import and an idea', async () => {
    await signedInWithAssistant();

    const { container } = renderWithProviders(OtherWaysIn, {
      props: { onpaste: () => {}, ondescribe: () => {} }
    });

    expect([...container.querySelectorAll('.way-title')].map((title) => title.textContent)).toEqual(
      ['Paste a link or text', 'Photograph a page', 'Import from another app', 'Start with an idea']
    );
  });
});

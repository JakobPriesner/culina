import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import NewRecipePage from './+page.svelte';
import { session } from '$features/auth/session.svelte';
import { renderWithProviders } from '$lib/test/render';
import { resetAllStores } from '$shell/stores';

const navigation = vi.hoisted(() => ({ goto: vi.fn() }));

vi.mock('$app/navigation', () => navigation);
vi.mock('$app/state', () => ({
  page: { url: new URL('http://culina.test/recipes/new') }
}));

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

const me = {
  userId: 'u1',
  email: 'ada@example.com',
  displayName: 'Ada',
  isAdmin: false,
  createdAt: '2026-01-01T00:00:00Z',
  version: 1,
  assistance: { improve: false, draft: false, read: false, draw: false },
  households: [{ householdId: 'h1', name: 'Home', role: 'owner', inheritsFrom: [] }]
};

const created = {
  recipeId: 'r-new',
  householdId: 'h1',
  title: 'Soup',
  language: 'en',
  yieldAmount: 4,
  yieldKind: 'servings',
  groups: [],
  steps: [],
  tags: [],
  createdBy: 'u1',
  createdAt: '2026-10-28T00:00:00Z',
  updatedAt: '2026-10-28T00:00:00Z',
  version: 1
};

let posts: number;
let releaseCreate: () => void;

beforeEach(async () => {
  resetAllStores();
  // A create in an earlier test remembers its recipe as the last draft; the page would then look it up.
  localStorage.clear();
  navigation.goto.mockReset();
  posts = 0;
  releaseCreate = () => {};

  vi.stubGlobal(
    'fetch',
    vi.fn((request: Request) => {
      if (request.method === 'POST' && request.url.endsWith('/api/v1/recipes')) {
        posts++;

        return new Promise<Response>((resolve) => {
          releaseCreate = () => resolve(json(created));
        });
      }

      return Promise.resolve(
        json(request.url.endsWith('/api/v1/users/me') ? me : { own: [], items: [] })
      );
    })
  );

  await session.refresh();
});

describe('starting a recipe from its title', () => {
  it('asks for a title instead of creating a recipe without one', async () => {
    renderWithProviders(NewRecipePage);

    await userEvent.click(screen.getByRole('button', { name: 'Create recipe' }));

    const field = screen.getByRole('textbox', { name: 'Title' });

    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveFocus();
    expect(field).toHaveAccessibleDescription('Give it a title. That is all a recipe needs.');

    await userEvent.type(field, '   ');
    await userEvent.click(screen.getByRole('button', { name: 'Create recipe' }));

    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(posts).toBe(0);
  });

  it('makes one recipe however often the button is pressed, then opens its editor', async () => {
    renderWithProviders(NewRecipePage);

    await userEvent.type(screen.getByRole('textbox', { name: 'Title' }), 'Soup');
    const button = screen.getByRole('button', { name: 'Create recipe' });

    await userEvent.dblClick(button);
    await userEvent.click(button);
    expect(posts).toBe(1);

    releaseCreate();

    await waitFor(() => expect(navigation.goto).toHaveBeenCalledTimes(1));
    expect(navigation.goto.mock.calls[0]![0]).toContain('r-new');
  });

  it('leaves the user in the flow they chose while a slow create finishes', async () => {
    renderWithProviders(NewRecipePage);

    await userEvent.type(screen.getByRole('textbox', { name: 'Title' }), 'Soup');
    await userEvent.click(screen.getByRole('button', { name: 'Create recipe' }));
    expect(posts).toBe(1);

    await userEvent.click(screen.getByRole('button', { name: /Paste a link or text/ }));
    releaseCreate();
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(navigation.goto).not.toHaveBeenCalled();
  });
});

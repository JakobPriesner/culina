import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';
import { resetAllStores } from '$shell/stores';

import ConnectAgentPanel from './ConnectAgentPanel.svelte';
import { session } from './session.svelte';

/* An agent's token is a key to the whole library: asked for with the password, shown once, and kept nowhere. */
const token = 'agent-token-0123456789';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const me = {
  userId: 'u1',
  email: 'ada@example.com',
  displayName: 'Ada',
  isAdmin: false,
  createdAt: '2026-01-01T00:00:00Z',
  version: 1,
  assistance: { improve: false, draft: false, read: false, draw: false },
  households: []
};

let fetched: ReturnType<typeof vi.fn>;

function serve(signIn: () => Response) {
  fetched = vi.fn((input: Request) => {
    const { pathname } = new URL(input.url);

    if (input.method === 'POST' && pathname === '/api/v1/sessions') {
      return Promise.resolve(signIn());
    }

    if (pathname === '/api/v1/users/me') {
      return Promise.resolve(json(me));
    }

    return Promise.resolve(json({ items: [] }));
  });
  vi.stubGlobal('fetch', fetched);
}

const signedIn = () =>
  json(
    {
      userId: 'u1',
      displayName: 'Ada',
      email: 'ada@example.com',
      isAdmin: false,
      csrfToken: 'agent-csrf',
      sessionToken: token
    },
    201
  );

const signIns = () =>
  fetched.mock.calls
    .map(([request]) => request as Request)
    .filter((r) => r.method === 'POST' && r.url.endsWith('/api/v1/sessions'));

async function connect(password: string) {
  await userEvent.click(screen.getByRole('button', { name: 'Connect an AI agent' }));
  await userEvent.type(screen.getByLabelText('Current password'), password);
  await userEvent.click(screen.getByRole('button', { name: 'Make a token' }));
}

beforeEach(async () => {
  localStorage.clear();
  sessionStorage.clear();
  resetAllStores();
  serve(signedIn);
  await session.refresh();
});

describe('connecting an AI agent', () => {
  it('signs in as an agent with the signed-in address and the password given', async () => {
    renderWithProviders(ConnectAgentPanel);

    await connect('correct horse');

    await waitFor(() => expect(signIns()).toHaveLength(1));
    expect(await signIns()[0]!.clone().json()).toEqual({
      email: 'ada@example.com',
      password: 'correct horse',
      client: 'agent'
    });
  });

  it('shows the token once, with the MCP address and the line for Claude Code', async () => {
    renderWithProviders(ConnectAgentPanel);

    await connect('correct horse');

    const url = `${location.origin}/api/mcp`;

    expect(await screen.findByText(token)).toBeInTheDocument();
    expect(screen.getByText(/shown only now/)).toBeInTheDocument();
    expect(screen.getByText(url)).toBeInTheDocument();
    expect(
      screen.getByText(
        `claude mcp add --transport http culina ${url} --header "Authorization: Bearer ${token}"`
      )
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copy the token' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copy the command' })).toBeInTheDocument();
  });

  it('copies the token', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } });

    renderWithProviders(ConnectAgentPanel);
    await connect('correct horse');

    await userEvent.click(await screen.findByRole('button', { name: 'Copy the token' }));

    expect(writeText).toHaveBeenCalledWith(token);
  });

  it('keeps the token nowhere, and forgets it when done', async () => {
    renderWithProviders(ConnectAgentPanel);
    await connect('correct horse');
    await screen.findByText(token);

    const stored = [
      ...Object.values({ ...localStorage }),
      ...Object.values({ ...sessionStorage }),
      document.cookie
    ];
    expect(stored.some((value) => String(value).includes(token))).toBe(false);

    await userEvent.click(screen.getByRole('button', { name: 'Done' }));

    expect(screen.queryByText(token)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Connect an AI agent' })).toBeInTheDocument();
  });

  it('says so when the password is wrong, and shows no token', async () => {
    serve(() =>
      json({ code: 'auth.invalid_credentials', title: 'Invalid credentials', status: 401 }, 401)
    );

    renderWithProviders(ConnectAgentPanel);
    await connect('wrong');

    expect(await screen.findByRole('alert')).toHaveTextContent("That isn't your current password.");
    expect(screen.queryByText(/shown only now/)).not.toBeInTheDocument();
    // A typo is not a lost session: this browser stays signed in.
    expect(session.user?.email).toBe('ada@example.com');
  });
});

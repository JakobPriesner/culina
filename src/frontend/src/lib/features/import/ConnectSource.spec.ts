import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ConnectSource from './ConnectSource.svelte';
import { sources } from './stores/sources.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The form that decides whether somebody gets as far as their recipes at all.
 * Everything here is about the step that used to stop them: being asked for an
 * API token before anything had offered to help them get one.
 */
let sent: Record<string, unknown>[] = [];

function serverAccepts() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      if (input.method === 'POST') {
        sent.push(await input.clone().json());
      }

      return new Response(
        JSON.stringify({
          sourceId: 's1',
          kind: 'tandoor',
          label: 'recipes.example.com',
          address: 'https://recipes.example.com',
          createdAt: '2026-09-17T00:00:00Z',
          lastUsedAt: null
        }),
        { status: 201, headers: { 'Content-Type': 'application/json' } }
      );
    })
  );
}

const render = () =>
  renderWithProviders(ConnectSource, {
    props: { householdId: 'h1', onconnected: () => {} }
  });

const address = () => screen.getByLabelText('App address');

beforeEach(() => {
  sources.reset();
  sent = [];
  serverAccepts();
});

describe('connecting another app', () => {
  it('asks only where it is, until there is somewhere to send anything', () => {
    render();

    // A form that asked for all four at once would be asking for a token
    // before it could offer any help getting one.
    expect(address()).toBeInTheDocument();
    expect(screen.queryByLabelText('Username in the app')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Connect' })).not.toBeInTheDocument();
  });

  it('waits for something that looks like a server, not for the first keystroke', async () => {
    render();

    await userEvent.type(address(), 'reci');

    expect(screen.queryByLabelText('Username in the app')).not.toBeInTheDocument();

    await userEvent.type(address(), 'pes.example.com');

    expect(screen.getByLabelText('Username in the app')).toBeInTheDocument();
  });

  it('offers signing in first, because that is what people already have', async () => {
    render();

    await userEvent.type(address(), 'recipes.example.com');

    expect(screen.getByRole('radio', { name: /Sign in/ })).toBeChecked();
    expect(screen.getByLabelText('Password in the app')).toBeInTheDocument();
  });

  it('sends the sign-in and no token', async () => {
    render();

    await userEvent.type(address(), 'recipes.example.com');
    await userEvent.type(screen.getByLabelText('Username in the app'), 'ada');
    await userEvent.type(screen.getByLabelText('Password in the app'), 'hunter2');
    await userEvent.click(screen.getByRole('button', { name: 'Connect' }));

    // The server refuses a request carrying both, so an empty token must not
    // be padded in alongside the sign-in.
    expect(sent[0]).toMatchObject({ username: 'ada', password: 'hunter2' });
    expect(sent[0]).not.toHaveProperty('token');
  });

  it('sends the token and no sign-in', async () => {
    render();

    await userEvent.type(address(), 'recipes.example.com');
    await userEvent.click(screen.getByRole('radio', { name: /Use an API token/ }));
    await userEvent.type(screen.getByLabelText('API token'), 'tda_secret');
    await userEvent.click(screen.getByRole('button', { name: 'Connect' }));

    expect(sent[0]).toMatchObject({ token: 'tda_secret' });
    expect(sent[0]).not.toHaveProperty('username');
    expect(sent[0]).not.toHaveProperty('password');
  });

  it('points at that server’s own token page, not at ours', async () => {
    render();

    await userEvent.type(address(), 'recipes.example.com');
    await userEvent.click(screen.getByRole('radio', { name: /Use an API token/ }));

    // Most of the work of "paste a token here" is knowing where to get one.
    expect(
      screen.getByRole('link', { name: /Open https:\/\/recipes\.example\.com/ })
    ).toHaveAttribute('href', 'https://recipes.example.com/settings');
  });

  it('keeps a typed port and scheme when pointing there', async () => {
    render();

    await userEvent.type(address(), 'http://tandoor.lan:8080');
    await userEvent.click(screen.getByRole('radio', { name: /Use an API token/ }));

    expect(screen.getByRole('link', { name: /Open http:\/\/tandoor\.lan:8080/ })).toHaveAttribute(
      'href',
      'http://tandoor.lan:8080/settings'
    );
  });

  it('does not leave the password sitting in the form afterwards', async () => {
    render();

    await userEvent.type(address(), 'recipes.example.com');
    await userEvent.type(screen.getByLabelText('Username in the app'), 'ada');
    await userEvent.type(screen.getByLabelText('Password in the app'), 'hunter2');
    await userEvent.click(screen.getByRole('button', { name: 'Connect' }));

    // "Used once and not stored" has to be true of this screen too, which on a
    // kitchen tablet other people walk past.
    expect(address()).toHaveValue('');
  });
});

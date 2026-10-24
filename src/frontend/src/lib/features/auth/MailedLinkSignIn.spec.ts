import { screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import MailedLinkSignIn from './MailedLinkSignIn.svelte';
import { renderWithProviders } from '$lib/test/render';

const navigation = vi.hoisted(() => ({ goto: vi.fn() }));

vi.mock('$app/navigation', () => navigation);

const json = (body: object, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const settle = async () => {
  for (let turn = 0; turn < 5; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

afterEach(() => {
  history.replaceState(null, '', '/');
  navigation.goto.mockReset();
});

describe('opening a mailed link', () => {
  it('sends the token from after the # and takes it out of the address bar', async () => {
    history.replaceState(null, '', '/login/link#token=abc123');
    const bodies: unknown[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: unknown) => {
        const request = input as Request;

        if (request.method === 'POST') {
          bodies.push(JSON.parse(await request.text()));

          return json(
            {
              userId: 'u1',
              displayName: 'Ada',
              email: 'ada@example.com',
              isAdmin: false,
              csrfToken: 'c'
            },
            201
          );
        }

        return json({
          userId: 'u1',
          email: 'ada@example.com',
          displayName: 'Ada',
          isAdmin: false,
          households: []
        });
      })
    );

    renderWithProviders(MailedLinkSignIn, { props: { kind: 'sign-in', title: 'Signing you in' } });
    await settle();

    expect(bodies).toEqual([{ token: 'abc123' }]);
    // A secret left in the address bar ends up in history and in copied links.
    expect(location.hash).toBe('');
    expect(navigation.goto).toHaveBeenCalled();
  });

  it('says the link no longer works, and offers the way back', async () => {
    history.replaceState(null, '', '/confirm-email#token=used');

    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        json(
          { code: 'auth.invalid_link', detail: 'That link does not work any more.', status: 400 },
          400
        )
      )
    );

    renderWithProviders(MailedLinkSignIn, {
      props: { kind: 'confirmation', title: 'Confirming your address' }
    });
    await settle();

    expect(
      screen.getByRole('heading', { level: 1, name: "That link didn't work" })
    ).toBeInTheDocument();
    expect(screen.getByText(/Links work once/)).toBeInTheDocument();
    expect(navigation.goto).not.toHaveBeenCalled();
  });
});

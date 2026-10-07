import { screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import JoinPage from './+page.svelte';
import { session, type SessionStatus } from '$features/auth/session.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * One link, four people opening it: somebody signed out, somebody signed in
 * who chooses to join, the owner checking the link they are about to send, and
 * somebody holding a code that no longer works. Only the first is ever offered
 * a sign-in, nobody signed in joins without asking to, and nobody signed in is
 * left on a page with no way back.
 */
vi.mock('$app/state', () => ({ page: { params: { code: 'abc123' } } }));

const goto = vi.hoisted(() => vi.fn());

vi.mock('$app/navigation', () => ({ goto }));

const json = (body: unknown, status: number) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const named = () => json({ householdName: 'Graces Küche' }, 200);

/**
 * Answers the redemption and the read of the invitation; every other request
 * is the session re-reading itself.
 */
function redemptionAnswers(
  reply: () => Response,
  read: () => Promise<Response> | Response = named
) {
  const fetched = vi.fn((input: Request) => {
    if (input.url.includes('/redemptions')) {
      return Promise.resolve(reply());
    }

    return Promise.resolve(input.url.endsWith('/invitations/abc123') ? read() : json({}, 200));
  });

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

const redemptions = (fetched: ReturnType<typeof redemptionAnswers>) =>
  fetched.mock.calls.filter(([input]) => input.url.includes('/redemptions'));

function signedIn(status: SessionStatus) {
  vi.spyOn(session, 'status', 'get').mockReturnValue(status);
  vi.spyOn(session, 'resolve').mockResolvedValue();
  vi.spyOn(session, 'reset').mockImplementation(() => {});
}

const signInOffered = () => screen.queryByRole('link', { name: /sign in/i });

/** Says yes to the household the link is for. */
async function joinIt() {
  (await screen.findByRole('button', { name: 'Join household' })).click();
}

beforeEach(() => goto.mockReset());

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('opening an invitation', () => {
  it('offers an account or a sign-in to somebody signed out, and comes back here after', async () => {
    signedIn('anonymous');
    const fetched = redemptionAnswers(() => json({}, 500));

    renderWithProviders(JoinPage);

    const signIn = await screen.findByRole('link', { name: /sign in/i });

    expect(signIn).toHaveAttribute(
      'href',
      expect.stringContaining(encodeURIComponent('/join/abc123'))
    );
    expect(fetched).not.toHaveBeenCalled();
  });

  it('asks somebody signed in before joining, naming the household, and does not join on opening', async () => {
    signedIn('authenticated');
    const fetched = redemptionAnswers(() =>
      json({ householdId: 'h2', name: 'Graces Küche', alreadyMember: false }, 201)
    );

    renderWithProviders(JoinPage);

    expect(await screen.findByText("You're invited to join Graces Küche.")).toBeVisible();
    expect(screen.getByRole('button', { name: 'Join household' })).toBeVisible();
    expect(screen.getByRole('link', { name: 'Back to your kitchen' })).toHaveAttribute('href', '/');
    await new Promise((settled) => setTimeout(settled, 50));

    expect(redemptions(fetched)).toHaveLength(0);
    expect(goto).not.toHaveBeenCalled();
  });

  it('shows a placeholder where the name will be while the invitation is read', async () => {
    signedIn('authenticated');
    redemptionAnswers(
      () => json({}, 500),
      () => new Promise<Response>(() => {})
    );

    const { container } = renderWithProviders(JoinPage);

    await vi.waitFor(() => expect(container.querySelector('.skeleton')).not.toBeNull());
    expect(container.querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(screen.getByRole('button', { name: 'Join household' })).toBeVisible();
  });

  it('says a code that no longer works is invalid before anybody presses Join', async () => {
    signedIn('authenticated');
    const fetched = redemptionAnswers(
      () => json({}, 500),
      () =>
        json(
          { code: 'households.invitation_invalid', detail: 'That invitation is not valid.' },
          404
        )
    );

    renderWithProviders(JoinPage);

    expect(
      await screen.findByRole('heading', { name: 'That invitation is not valid' })
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Join household' })).not.toBeInTheDocument();
    expect(redemptions(fetched)).toHaveLength(0);
  });

  it('still asks, without a name, when the invitation cannot be read', async () => {
    signedIn('authenticated');
    const select = vi.spyOn(session, 'selectHousehold').mockImplementation(() => {});
    const fetched = redemptionAnswers(
      () => json({ householdId: 'h2', name: 'Graces Küche', alreadyMember: false }, 201),
      () => json({ code: 'server.unexpected', detail: 'Oops.' }, 500)
    );

    renderWithProviders(JoinPage);
    await vi.waitFor(() => expect(fetched).toHaveBeenCalled());
    await new Promise((settled) => setTimeout(settled, 50));

    expect(screen.queryByText(/invited to join/)).not.toBeInTheDocument();
    await joinIt();
    await vi.waitFor(() => expect(select).toHaveBeenCalledWith('h2'));
  });

  it('takes somebody who joins into the household it is for', async () => {
    signedIn('authenticated');
    const select = vi.spyOn(session, 'selectHousehold').mockImplementation(() => {});

    redemptionAnswers(() =>
      json({ householdId: 'h2', name: 'Graces Küche', alreadyMember: false }, 201)
    );

    renderWithProviders(JoinPage);
    await joinIt();

    await vi.waitFor(() => expect(goto).toHaveBeenCalled());

    // The one the link was for, not whichever kitchen they had open last.
    expect(select).toHaveBeenCalledWith('h2');
  });

  it('redeems the code once, though joining re-reads the session', async () => {
    // The real store this time: it is its status changing under the page,
    // as joining re-reads the session, that must not redeem the code again.
    session.reset();
    let redeemed = 0;

    vi.stubGlobal('fetch', (input: Request) => {
      if (input.url.includes('/redemptions')) {
        redeemed += 1;

        return Promise.resolve(
          json({ householdId: 'h2', name: 'Graces Küche', alreadyMember: redeemed > 1 }, 201)
        );
      }

      return Promise.resolve(
        input.url.endsWith('/users/me')
          ? json({ userId: 'u1', households: [] }, 200)
          : json({}, 404)
      );
    });

    renderWithProviders(JoinPage);
    await joinIt();

    await vi.waitFor(() => expect(goto).toHaveBeenCalled());
    await new Promise((settled) => setTimeout(settled, 50));

    expect(redeemed).toBe(1);
    session.reset();
  });

  it('tells the owner checking their own link that they are already in', async () => {
    signedIn('authenticated');
    redemptionAnswers(() => json({ householdId: 'h1', name: 'Home', alreadyMember: true }, 200));

    renderWithProviders(JoinPage);
    await joinIt();

    expect(
      await screen.findByRole('heading', { name: "You're already in Home" })
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Open Home' })).toBeVisible();
    expect(signInOffered()).not.toBeInTheDocument();
  });

  it('says a used, expired or revoked code is invalid, and offers the way back', async () => {
    signedIn('authenticated');
    redemptionAnswers(() =>
      json({ code: 'households.invitation_invalid', detail: 'That invitation is not valid.' }, 404)
    );

    renderWithProviders(JoinPage);
    await joinIt();

    expect(
      await screen.findByRole('heading', { name: 'That invitation is not valid' })
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to your kitchen' })).toHaveAttribute('href', '/');
    expect(signInOffered()).not.toBeInTheDocument();
  });

  it('does not call a failure to reach the server an invalid invitation', async () => {
    signedIn('authenticated');
    redemptionAnswers(() => json({ code: 'server.unexpected', detail: 'Oops.' }, 500));

    renderWithProviders(JoinPage);
    await joinIt();

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeVisible();
    expect(screen.queryByText('That invitation is not valid')).not.toBeInTheDocument();
  });
});

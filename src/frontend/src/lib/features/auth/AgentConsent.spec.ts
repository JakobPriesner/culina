import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';

import AgentConsent from './AgentConsent.svelte';
import type { AgentAuthorization } from './agentAuthorization';

/* An app asks to connect an AI agent: the person sees who asks and where they go back to, and answers once. */
const claude: AgentAuthorization = {
  id: 'request-1',
  clientName: 'Claude',
  vouchedForBy: 'claude.ai',
  redirectHost: 'claude.ai',
  redirectsToThisComputer: false
};

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status < 300 ? 'application/json' : 'application/problem+json' }
  });

let fetched: ReturnType<typeof vi.fn>;

function serve(answer: (approved: boolean) => Response) {
  fetched = vi.fn(async (input: Request) =>
    answer(((await input.json()) as { approved: boolean }).approved)
  );
  vi.stubGlobal('fetch', fetched);
}

const back =
  'https://claude.ai/api/mcp/auth_callback?code=abc&state=xyz&iss=https%3A%2F%2Fculina.example';

beforeEach(() => {
  serve((approved) => json({ approved, redirectUri: back }));
});

describe('the consent screen', () => {
  it('names the app, who vouches for it and where it sends the browser', () => {
    renderWithProviders(AgentConsent, { props: { authorization: claude, onanswered: vi.fn() } });

    expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Claude');
    expect(screen.getByText('Sends you back to').nextElementSibling?.textContent).toBe('claude.ai');
    expect(screen.getByText('Vouched for by').nextElementSibling?.textContent).toBe('claude.ai');
    expect(screen.queryByText(/program on this computer/)).toBeNull();
  });

  it('shows an app that named itself as text, and warns about one that returns to this computer', () => {
    const named = '<img src=x onerror=alert(1)>';

    renderWithProviders(AgentConsent, {
      props: {
        authorization: {
          ...claude,
          clientName: named,
          vouchedForBy: null,
          redirectHost: 'localhost',
          redirectsToThisComputer: true
        },
        onanswered: vi.fn()
      }
    });

    expect(screen.getByText('App').nextElementSibling?.textContent).toBe(named);
    expect(document.querySelector('img')).toBeNull();
    expect(screen.getByText(/the app named itself/)).toBeTruthy();
    expect(screen.getByText(/program on this computer/)).toBeTruthy();
  });

  it('sends the approval and hands over the return address', async () => {
    const onanswered = vi.fn();
    renderWithProviders(AgentConsent, { props: { authorization: claude, onanswered } });

    await userEvent.click(screen.getByRole('button', { name: 'Allow' }));

    await waitFor(() => expect(onanswered).toHaveBeenCalledWith(back));
    const sent = fetched.mock.calls[0]![0] as Request;
    expect(sent.method).toBe('PUT');
    expect(new URL(sent.url).pathname).toBe('/api/v1/agent-authorizations/request-1/decision');
  });

  it('sends a refusal when the person denies', async () => {
    const onanswered = vi.fn();
    serve((approved) =>
      json({ approved, redirectUri: 'https://claude.ai/api/mcp/auth_callback?error=access_denied' })
    );
    renderWithProviders(AgentConsent, { props: { authorization: claude, onanswered } });

    await userEvent.click(screen.getByRole('button', { name: 'Deny' }));

    await waitFor(() =>
      expect(onanswered).toHaveBeenCalledWith(
        'https://claude.ai/api/mcp/auth_callback?error=access_denied'
      )
    );
  });

  it('says so when the request expired meanwhile, and goes nowhere', async () => {
    const onanswered = vi.fn();
    serve(() =>
      json(
        {
          status: 404,
          code: 'agent_authorizations.not_found',
          detail:
            'This connection request has expired or was already answered. Start connecting from the app again.'
        },
        404
      )
    );
    renderWithProviders(AgentConsent, { props: { authorization: claude, onanswered } });

    await userEvent.click(screen.getByRole('button', { name: 'Allow' }));

    expect((await screen.findByRole('alert')).textContent).toContain(
      'expired or was already answered'
    );
    expect(onanswered).not.toHaveBeenCalled();
  });
});

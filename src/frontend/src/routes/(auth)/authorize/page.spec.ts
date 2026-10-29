import { screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { clientError } from '$api';
import { renderWithProviders } from '#lib/test/render.js';
import { resetAllStores } from '$shell/stores';

import AuthorizePage from './+page.svelte';
import { load } from './+page';

/* Where Culina's OAuth endpoint sends the browser: a request to answer, a refusal to explain, or sign-in first. */
const session = vi.hoisted(() => ({ status: 'anonymous', resolve: vi.fn(async () => {}) }));

vi.mock('$features/auth/session.svelte', () => ({ session }));

const loadAt = (search: string) =>
  load({ url: new URL(`http://culina.test/authorize${search}`) } as Parameters<typeof load>[0]);

beforeEach(() => {
  resetAllStores();
  session.status = 'anonymous';
});

describe('the authorize route', () => {
  it('sends a stranger to sign in, and back here afterwards', async () => {
    await expect(loadAt('?request=request-1')).rejects.toMatchObject({
      status: 307,
      location: `/login?next=${encodeURIComponent('/authorize?request=request-1')}`
    });
  });

  it('explains a request the app could not be trusted with, without signing in', async () => {
    const data = await loadAt('?problem=oauth.invalid_redirect_uri');

    renderWithProviders(AuthorizePage, { props: { data } as never });

    expect(
      screen.getByRole('heading', { level: 1, name: "This connection can't go ahead" })
    ).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toContain('never registered');
    expect(screen.queryByRole('button', { name: 'Allow' })).toBeNull();
  });

  it('offers nothing to answer once the request is gone', () => {
    const problem = clientError('agent_authorizations.not_found', 'Gone.');

    renderWithProviders(AuthorizePage, {
      props: { data: { authorization: null, problem } } as never
    });

    expect(screen.getByRole('alert').textContent).toContain('expired or was already answered');
  });
});

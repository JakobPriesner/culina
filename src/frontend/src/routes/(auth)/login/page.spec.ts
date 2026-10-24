import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import LoginPage from './+page.svelte';
import { resetAllStores } from '$shell/stores';
import { renderWithProviders } from '$lib/test/render';

const location = vi.hoisted(() => ({ search: '' }));

vi.mock('$app/state', () => ({
  page: {
    get url() {
      return new URL(`http://culina.test/login${location.search}`);
    }
  }
}));

vi.mock('$app/navigation', () => ({ goto: vi.fn() }));

const flames = (container: HTMLElement) => container.querySelectorAll('.flame.on').length;

beforeEach(() => {
  resetAllStores();
  location.search = '';
});

describe('the sign-in page', () => {
  it('explains nothing to somebody who simply opened the app', () => {
    renderWithProviders(LoginPage);

    expect(screen.getByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('says the session went cold, and lights a flame for every character typed', async () => {
    location.search = '?next=%2Fshopping&reason=expired';
    const { container } = renderWithProviders(LoginPage);

    expect(
      screen.getByRole('heading', { level: 1, name: 'Your session went cold' })
    ).toBeInTheDocument();
    expect(flames(container)).toBe(0);

    // jsdom gives a password field no role, unlike a browser.
    await userEvent.type(container.querySelector('input[type="password"]')!, 'sauce');

    expect(flames(container)).toBe(5);
    expect(screen.getByRole('button', { name: 'Turn the heat back on' })).toBeInTheDocument();
  });

  it('calls a followed link a family secret, without showing what is behind it', () => {
    location.search = '?next=%2Frecipes%2Fabc';
    renderWithProviders(LoginPage);

    expect(
      screen.getByRole('heading', { level: 1, name: "This one's a family secret" })
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "I'm family, sign me in" })).toBeInTheDocument();
  });

  it('offers a sign-in link where the instance sends mail, and says where to look once it is sent', async () => {
    const posted: string[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: unknown) => {
        const request = input as Request;

        if (request.url.includes('/registration/policy')) {
          return new Response(
            JSON.stringify({
              openRegistration: false,
              requireInvitation: false,
              hasAccounts: true,
              sendsMail: true
            }),
            { headers: { 'Content-Type': 'application/json' } }
          );
        }

        posted.push(request.url);

        return new Response(null, { status: 202 });
      })
    );

    renderWithProviders(LoginPage);

    await userEvent.click(
      await screen.findByRole('button', { name: 'Email me a sign-in link instead' })
    );
    await userEvent.type(screen.getByRole('textbox', { name: /email/i }), 'ada@example.com');
    await userEvent.click(screen.getByRole('button', { name: 'Email me a link' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Check your inbox' })
    ).toBeInTheDocument();
    // Said as "if it has an account": the answer is the same either way.
    expect(screen.getByText(/If ada@example.com has a Culina account/)).toBeInTheDocument();
    expect(posted.some((url) => url.endsWith('/api/v1/sign-in-links'))).toBe(true);
  });
});

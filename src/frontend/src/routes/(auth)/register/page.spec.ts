import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import RegisterPage from './+page.svelte';
import { resetAllStores } from '$shell/stores';
import { renderWithProviders } from '#lib/test/render.js';

const register = vi.hoisted(() => vi.fn());

vi.mock('$features/auth/registration.svelte', () => ({ register }));

vi.mock('$app/state', () => ({
  page: {
    get url() {
      return new URL('http://culina.test/register');
    }
  }
}));

vi.mock('$app/navigation', () => ({ goto: vi.fn() }));

const policy = { openRegistration: true, requireInvitation: false, hasAccounts: true };

/** The server names the wrong fields in its own order, not the page's. */
const rejected = (...fields: string[]) => ({
  code: 'users.invalid',
  detail: 'That did not work.',
  status: 400,
  requestId: 'req-1',
  retryAfterSeconds: null,
  fields: fields.map((field) => ({ field, code: 'invalid', detail: `Fix ${field}.` }))
});

const name = () => screen.getByLabelText(/^What should we call you/);
const email = () => screen.getByLabelText(/^Email address/);
const password = () => screen.getByLabelText(/^Password/);
const submit = () => userEvent.click(screen.getByRole('button', { name: 'Create account' }));

beforeEach(() => {
  resetAllStores();
  register.mockReset();
});

describe('the sign-up page', () => {
  it('focuses the name when everything is empty, though the server lists email first', async () => {
    register.mockResolvedValue(rejected('email', 'password', 'displayName'));
    renderWithProviders(RegisterPage, { props: { data: { policy } } });

    await submit();

    await waitFor(() => expect(name()).toHaveFocus());
    expect(name()).toHaveAttribute('aria-invalid', 'true');
    expect(name()).toHaveAccessibleDescription(/Fix displayName/);
  });

  it('focuses the email when only the email and password are wrong', async () => {
    register.mockResolvedValue(rejected('password', 'email'));
    renderWithProviders(RegisterPage, { props: { data: { policy } } });

    await userEvent.type(name(), 'Ada');
    await submit();

    await waitFor(() => expect(email()).toHaveFocus());
    expect(email()).toHaveAttribute('aria-invalid', 'true');
  });

  it('focuses the password when it is the only thing wrong', async () => {
    register.mockResolvedValue(rejected('password'));
    renderWithProviders(RegisterPage, { props: { data: { policy } } });

    await userEvent.type(name(), 'Ada');
    await userEvent.type(email(), 'ada@culina.test');
    await submit();

    await waitFor(() => expect(password()).toHaveFocus());
    expect(password()).toHaveAttribute('aria-invalid', 'true');
    expect(password()).toHaveAccessibleDescription(/Fix password/);
  });

  it('leaves focus alone when the answer arrives after the user moved on', async () => {
    let answer: (value: unknown) => void = () => {};
    register.mockReturnValue(new Promise((resolve) => (answer = resolve)));
    renderWithProviders(RegisterPage, { props: { data: { policy } } });

    await submit();
    await userEvent.click(password());
    answer(rejected('displayName'));

    await waitFor(() => expect(name()).toHaveAttribute('aria-invalid', 'true'));
    expect(password()).toHaveFocus();
  });
});

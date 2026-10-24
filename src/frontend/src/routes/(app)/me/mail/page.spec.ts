import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import MailPage from './+page.svelte';
import { renderWithProviders } from '$lib/test/render';
import { toaster } from '$shell/toaster.svelte';

/* The mail page: a write-only password, kept only for the server it was saved for, and a test through what was saved. */
const saved = {
  enabled: true,
  host: 'smtp.example.com',
  port: 587,
  security: 'starttls',
  username: 'culina@example.com',
  passwordConfigured: true,
  fromAddress: 'culina@example.com',
  fromName: 'Culina',
  siteUrl: 'https://culina.example.com/',
  sending: true
};

const json = (body: object, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

function answers(settings: object = saved) {
  const sent: { method: string; url: string; body: unknown }[] = [];

  const fetched = vi.fn(async (input: unknown) => {
    const request = input as Request;
    const body = request.method === 'GET' ? null : await request.text();

    sent.push({ method: request.method, url: request.url, body: body ? JSON.parse(body) : null });

    if (request.url.includes('/settings/mail/tests')) return new Response(null, { status: 204 });
    // A save answers with what is stored, which here is what was there.
    if (request.url.includes('/settings/mail')) return json(settings);

    return json({});
  });

  vi.stubGlobal('fetch', fetched);

  return sent;
}

const settle = async () => {
  for (let turn = 0; turn < 5; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

describe('the mail settings page', () => {
  it('keeps the saved password by sending none when the box is left empty', async () => {
    const sent = answers();

    renderWithProviders(MailPage);
    await settle();

    expect(screen.getByText(/A password is saved/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await settle();

    const put = sent.find((one) => one.method === 'PUT');
    expect(put?.body).toMatchObject({ host: 'smtp.example.com', port: 587, password: null });
  });

  it('asks for the password again once the server changes', async () => {
    answers();

    renderWithProviders(MailPage);
    await settle();

    const host = screen.getByRole('textbox', { name: 'SMTP server' });
    await userEvent.clear(host);
    await userEvent.type(host, 'smtp.elsewhere.example');

    expect(screen.getByText(/only sent to the server it was saved for/)).toBeInTheDocument();
  });

  it('says why mail cannot be switched on while Culina has no public address', async () => {
    answers({ ...saved, enabled: false, siteUrl: null, sending: false });

    renderWithProviders(MailPage);
    await settle();

    expect(screen.getByRole('note')).toHaveTextContent(/Site__Url/);
    // Nothing is sent, so a test would prove nothing.
    expect(screen.getByRole('button', { name: 'Send a test mail' })).toBeDisabled();
  });

  it('sends a test mail through the saved account', async () => {
    const sent = answers();

    renderWithProviders(MailPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Send a test mail' }));
    await settle();

    expect(
      sent.some((one) => one.method === 'POST' && one.url.endsWith('/settings/mail/tests'))
    ).toBe(true);
    expect(toaster.toasts.map((toast) => toast.message())).toContain(
      'Test mail sent to your address.'
    );
  });
});

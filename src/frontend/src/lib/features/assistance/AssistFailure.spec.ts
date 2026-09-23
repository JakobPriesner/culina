import { render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { clientError } from '$api';
import { applyLocale } from '$shell/i18n';

import AssistFailure from './AssistFailure.svelte';

const signedIn = vi.hoisted(() => ({ user: { isAdmin: true } as { isAdmin: boolean } | null }));

vi.mock('$features/auth/session.svelte', () => ({ session: signedIn }));

/** Every code the assistant can fail with in front of somebody cooking. */
const codes = [
  'assistance.not_configured',
  'assistance.disabled',
  'assistance.model_missing',
  'assistance.unknown_provider',
  'assistance.drawing_not_supported',
  'assistance.unavailable',
  'assistance.rejected',
  'assistance.throttled',
  'assistance.budget_exhausted',
  'assistance.personal_budget_exhausted',
  'assistance.refused',
  'assistance.unusable_answer',
  'assistance.nothing_to_work_from',
  'assistance.too_much_to_work_from'
];

/** What the server says, which is English and written for a log. */
const serverDetail = 'The assistant would not answer that.';

const said = (code: string): string => {
  const { unmount } = render(AssistFailure, { error: clientError(code, serverDetail) });
  const text = screen.getByRole('alert').textContent?.trim() ?? '';

  unmount();

  return text;
};

describe('an assistant failure', () => {
  afterEach(() => {
    applyLocale('en');
    signedIn.user = { isAdmin: true };
  });

  it.each(codes)('says %s in both languages rather than repeating the server', (code) => {
    applyLocale('en');
    const english = said(code);

    applyLocale('de');
    const german = said(code);

    expect(english).not.toContain(serverDetail);
    expect(german).not.toContain(serverDetail);
    expect(german).not.toBe(english);
  });

  it('names the missing model as the problem, not the request', () => {
    applyLocale('de');

    render(AssistFailure, { error: clientError('assistance.model_missing', serverDetail) });

    expect(screen.getByRole('alert')).toHaveTextContent(/KI-Modell ist nicht verfügbar/);
  });

  it('takes an administrator straight to the assistant settings when the fix is there', () => {
    render(AssistFailure, { error: clientError('assistance.model_missing', serverDetail) });

    expect(screen.getByRole('link', { name: 'Open assistant settings' })).toHaveAttribute(
      'href',
      expect.stringMatching(/\/me\/ai$/)
    );
  });

  it('tells everybody else who can fix it, without a link they could not use', () => {
    signedIn.user = { isAdmin: false };

    render(AssistFailure, { error: clientError('assistance.not_configured', serverDetail) });

    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByText(/Whoever looks after this Culina/)).toBeInTheDocument();
  });

  it('offers no settings where settings would not help', () => {
    render(AssistFailure, { error: clientError('assistance.unusable_answer', serverDetail) });

    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent(/Trying again usually works/);
  });

  it('falls back to the app-wide wording for a failure that is not the assistant’s', () => {
    applyLocale('de');

    render(AssistFailure, { error: clientError('client.offline', 'You appear to be offline.') });

    expect(screen.getByRole('alert')).not.toHaveTextContent('You appear to be offline.');
  });
});

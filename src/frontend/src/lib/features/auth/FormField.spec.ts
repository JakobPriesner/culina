import { screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import FormFieldHarness from './__fixtures__/FormFieldHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

const problem = (field: string, detail: string) => ({
  code: 'users.invalid',
  detail: 'That did not work.',
  status: 400,
  requestId: 'req-1',
  retryAfterSeconds: null,
  fields: [{ field, code: 'required', detail }]
});

describe('a form field', () => {
  it('is labelled, so it can be found and clicked into by its label', () => {
    renderWithProviders(FormFieldHarness, {});

    expect(screen.getByLabelText('Email address')).toBeInTheDocument();
  });

  it('shows the server error on the field, never somewhere else', async () => {
    renderWithProviders(FormFieldHarness, {
      props: { failure: problem('email', 'That address is already in use.') }
    });

    await waitFor(() =>
      expect(screen.getByLabelText('Email address')).toHaveAccessibleDescription(
        'That address is already in use.'
      )
    );

    expect(screen.getByLabelText('Email address')).toHaveAttribute('aria-invalid', 'true');
  });

  it('takes focus when it is the first thing the server complained about', async () => {
    renderWithProviders(FormFieldHarness, {
      props: { failure: problem('email', 'That address is already in use.') }
    });

    await waitFor(() => expect(screen.getByLabelText('Email address')).toHaveFocus());
  });

  it('is unbothered by a failure about some other field', async () => {
    renderWithProviders(FormFieldHarness, {
      props: { failure: problem('password', 'Too short.') }
    });

    await waitFor(() =>
      expect(screen.getByLabelText('Email address')).not.toHaveAttribute('aria-invalid')
    );
  });
});

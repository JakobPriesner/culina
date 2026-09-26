import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import TextInput from './TextInput.svelte';
import { renderWithProviders } from '$lib/test/render';

const field = () => screen.getByLabelText('Password');
const reveal = () => screen.queryByRole('button', { name: 'Show password' });

describe('TextInput', () => {
  it('hides a password until asked, and hides it again', async () => {
    renderWithProviders(TextInput, {
      props: {
        id: 'password',
        value: '',
        type: 'password',
        label: 'Password',
        revealLabel: 'Show password'
      }
    });

    await userEvent.type(field(), 'correct horse');

    expect(field()).toHaveAttribute('type', 'password');
    expect(reveal()).toHaveAttribute('aria-pressed', 'false');

    await userEvent.click(reveal()!);

    expect(field()).toHaveAttribute('type', 'text');
    expect(field()).toHaveValue('correct horse');
    expect(reveal()).toHaveAttribute('aria-pressed', 'true');

    await userEvent.click(reveal()!);

    expect(field()).toHaveAttribute('type', 'password');
    expect(field()).toHaveValue('correct horse');
  });

  it('offers nothing to reveal in a field that hides nothing', () => {
    renderWithProviders(TextInput, {
      props: {
        id: 'email',
        value: '',
        type: 'email',
        label: 'Password',
        revealLabel: 'Show password'
      }
    });

    expect(reveal()).not.toBeInTheDocument();
  });
});

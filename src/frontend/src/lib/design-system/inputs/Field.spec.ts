import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import FieldHarness from '../__fixtures__/FieldHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The point of Field is that nobody writes aria-describedby by hand. These
 * assertions are the contract that makes that safe to rely on.
 */
describe('Field', () => {
  it('labels its control, so clicking the label focuses the input', () => {
    renderWithProviders(FieldHarness, { props: { label: 'Recipe title' } });

    expect(screen.getByLabelText('Recipe title')).toBeInTheDocument();
  });

  it('describes the control with the hint', () => {
    renderWithProviders(FieldHarness, { props: { hint: 'Roughly how long it takes.' } });

    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription(
      'Roughly how long it takes.'
    );
  });

  it('describes the control with the error, and marks it invalid', () => {
    renderWithProviders(FieldHarness, { props: { error: 'Give it a name.' } });

    const input = screen.getByLabelText('Title');

    expect(input).toHaveAccessibleDescription('Give it a name.');
    expect(input).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows the error instead of the hint, because two messages compete', () => {
    renderWithProviders(FieldHarness, {
      props: { hint: 'At least three characters.', error: 'Give it a name.' }
    });

    expect(screen.queryByText('At least three characters.')).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Give it a name.');
  });

  it('claims nothing about validity when there is no error', () => {
    renderWithProviders(FieldHarness, {});

    expect(screen.getByLabelText('Title')).not.toHaveAttribute('aria-invalid');
  });
});

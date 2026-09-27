import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import DietQuestion from './DietQuestion.svelte';
import { renderWithProviders } from '$lib/test/render';

describe('asking whether a presumed recipe keeps its diet', () => {
  it('asks about the diet the search presumed', () => {
    renderWithProviders(DietQuestion, { props: { diet: 'vegan', onanswer: vi.fn() } });

    expect(screen.getByText('Is this vegan?')).toBeInTheDocument();
  });

  it('hands back either answer, since both are worth writing down', async () => {
    const user = userEvent.setup();
    const onanswer = vi.fn();
    renderWithProviders(DietQuestion, { props: { diet: 'vegetarian', onanswer } });

    await user.click(screen.getByRole('button', { name: 'Yes' }));
    await user.click(screen.getByRole('button', { name: 'No' }));

    expect(onanswer.mock.calls).toEqual([[true], [false]]);
  });
});

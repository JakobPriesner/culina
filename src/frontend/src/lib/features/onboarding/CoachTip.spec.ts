import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { createRawSnippet } from 'svelte';
import { afterEach, describe, expect, it } from 'vitest';

import { renderWithProviders } from '$lib/test/render';

import CoachTip from './CoachTip.svelte';
import { tips } from './tips.svelte';

/* A tip where it is needed, once. */
afterEach(() => {
  tips.reset();
  localStorage.clear();
});

const body = createRawSnippet(() => ({ render: () => '<p>Drag a meal to another day.</p>' }));

describe('a tip in place', () => {
  it('is shown the first time and gone for good after “Got it”', async () => {
    tips.own('u1');

    renderWithProviders(CoachTip, { props: { id: 'plan', title: 'Your week', children: body } });

    expect(screen.getByRole('complementary', { name: 'Your week' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Got it' }));

    expect(screen.queryByRole('complementary', { name: 'Your week' })).toBeNull();
    expect(tips.isNew('plan')).toBe(false);
  });
});

import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { createRawSnippet } from 'svelte';
import { afterEach, describe, expect, it } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';

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

    renderWithProviders(CoachTip, {
      props: { id: 'plan', pose: 'thinking', title: 'Move meals around', children: body }
    });

    expect(screen.getByRole('complementary', { name: 'Move meals around' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Got it' }));

    expect(screen.queryByRole('complementary', { name: 'Move meals around' })).toBeNull();
    expect(tips.isNew('plan')).toBe(false);
  });
});

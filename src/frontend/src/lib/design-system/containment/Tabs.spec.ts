import { screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { describe, expect, it } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';

import Tabs from './Tabs.svelte';

/* A tab only claims the panel that exists: the others would point at nothing. */
const children = createRawSnippet(() => ({ render: () => '<p>Content</p>' }));

describe('the tab strip', () => {
  it('points aria-controls only at the rendered panel', () => {
    renderWithProviders(Tabs, {
      props: {
        tabs: [
          { id: 'a', label: 'First' },
          { id: 'b', label: 'Second' }
        ],
        selected: 'a',
        label: 'Views',
        children
      }
    });

    const panel = screen.getByRole('tabpanel');

    expect(screen.getByRole('tab', { name: 'First' })).toHaveAttribute('aria-controls', panel.id);
    expect(screen.getByRole('tab', { name: 'Second' })).not.toHaveAttribute('aria-controls');
  });
});

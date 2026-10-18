import { render, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { describe, expect, it } from 'vitest';

import OlliSays from './OlliSays.svelte';

/* Olli's words are the content; the pot itself stays out of the accessibility tree. */
describe('Olli saying something', () => {
  it('shows the words in a bubble, announced when asked', () => {
    const { container } = render(OlliSays, {
      props: {
        pose: 'hello',
        live: true,
        children: createRawSnippet(() => ({ render: () => '<p>Hi, I’m Olli!</p>' }))
      }
    });

    expect(screen.getByText('Hi, I’m Olli!').parentElement).toHaveAttribute('aria-live', 'polite');
    expect(container.querySelector('svg.olli')).toHaveAttribute('aria-hidden', 'true');
  });
});

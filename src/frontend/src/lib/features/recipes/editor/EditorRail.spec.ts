import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import EditorRail from './EditorRail.svelte';
import type { SaveTone } from './SaveState.svelte';
import { renderWithProviders } from '#lib/test/render.js';

const show = (tone: SaveTone, status: string) =>
  renderWithProviders(EditorRail, {
    props: {
      backHref: '/recipes/1',
      backLabel: 'Done',
      title: 'A very long recipe title that has to stay recognisable',
      untitled: 'Untitled',
      tone,
      status,
      sectionsLabel: 'Sections',
      sections: []
    }
  });

describe('the editor rail status', () => {
  it('keeps the steady word for assistive tech and as a tooltip', () => {
    show('idle', 'Saves as you write');

    const status = screen.getByRole('status');

    expect(status).toHaveTextContent('Saves as you write');
    expect(status).toHaveAttribute('title', 'Saves as you write');
    expect(status).toHaveClass('idle');
  });

  it('says a save in flight', () => {
    show('saving', 'Saving');

    expect(screen.getByRole('status')).toHaveTextContent('Saving');
  });

  it.each([
    ['failed', 'Not saved yet'],
    ['conflict', 'Someone changed this'],
    ['local', 'Saved on this device']
  ] as const)('keeps the words of the %s state in the status', (tone, text) => {
    show(tone, text);

    const status = screen.getByRole('status');

    expect(status).toHaveTextContent(text);
    expect(status).toHaveClass(tone);
    expect(status.closest('.state')).toHaveClass(tone);
  });

  it('keeps the recipe name beside the status', () => {
    show('saved', 'Saved');

    expect(screen.getByText(/A very long recipe title/)).toBeInTheDocument();
  });
});

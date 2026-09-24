import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import TagChooser from './TagChooser.svelte';

/*
 * Fifty tags is an ordinary kitchen after an import. What has to hold is that
 * the other questions on the same sheet stay reachable — the chooser opens to
 * a handful — and that nothing chosen can scroll out of sight.
 */
const kitchen = Array.from({ length: 50 }, (_, index) => ({
  slug: `tag-${index}`,
  name: index === 42 ? 'Crème brûlée' : `Tag ${String(index).padStart(2, '0')}`,
  recipeCount: 100 - index
}));

const chooser = (selected: string[] = [], ontoggle = vi.fn()) =>
  render(TagChooser, { tags: kitchen, selected, ontoggle, empty: 'No tags yet.' });

const chips = () => screen.getAllByRole('button', { pressed: false });

describe('choosing tags', () => {
  it('opens on the few most recipes carry, not on all fifty', () => {
    chooser();

    expect(chips()).toHaveLength(8);
    expect(chips()[0]).toHaveAccessibleName(/^Tag 00 ?\(100 recipes\)$/);
  });

  it('shows the rest when asked, and says it can be folded away again', async () => {
    chooser();

    const more = screen.getByRole('button', { name: 'Show all 50 tags' });

    await userEvent.click(more);

    expect(chips()).toHaveLength(50);
    expect(screen.getByRole('button', { name: 'Show fewer' })).toHaveAttribute(
      'aria-expanded',
      'true'
    );
  });

  it('keeps what is chosen first and in view, however far down the list it was', () => {
    chooser(['tag-49']);

    const chosen = screen.getByRole('button', { pressed: true });

    expect(chosen).toHaveAccessibleName(/Tag 49/);
    expect(screen.getAllByRole('button')[0]).toBe(chosen);
  });

  it('finds a tag by a few letters, ignoring accents', async () => {
    chooser();

    await userEvent.type(screen.getByRole('searchbox'), 'creme');

    expect(chips().map((chip) => chip.textContent)).toEqual([expect.stringContaining('Crème')]);
  });

  it('says so when nothing matches, rather than showing an empty space', async () => {
    chooser();

    await userEvent.type(screen.getByRole('searchbox'), 'zzz');

    expect(screen.getByRole('status')).toHaveTextContent('No tag matches that.');
  });

  it('turns a tag on and off', async () => {
    const ontoggle = vi.fn();

    chooser(['tag-3'], ontoggle);

    await userEvent.click(screen.getByRole('button', { name: /Tag 03/ }));
    await userEvent.click(screen.getByRole('button', { name: /Tag 00/ }));

    expect(ontoggle.mock.calls).toEqual([
      ['tag-3', false],
      ['tag-0', true]
    ]);
  });

  it('asks nothing of a kitchen with only a few tags', () => {
    render(TagChooser, {
      tags: kitchen.slice(0, 3),
      selected: [],
      ontoggle: vi.fn(),
      empty: 'No tags yet.'
    });

    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Show all/ })).not.toBeInTheDocument();
  });
});

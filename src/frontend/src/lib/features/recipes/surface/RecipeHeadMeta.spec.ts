import { screen } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import RecipeHeadMeta from './RecipeHeadMeta.svelte';
import type { RecipeReading } from '../types';
import { renderWithProviders } from '$lib/test/render';

const recipe = {
  totalMinutes: 35,
  yieldAmount: 2,
  yieldKind: 'servings',
  yieldLabel: null,
  description: null,
  sourceUrl: null,
  cookCount: 0
} as unknown as RecipeReading;

const show = (props: object) =>
  renderWithProviders(RecipeHeadMeta, {
    props: { recipe, cooking: false, cookbooks: [], printedYield: '2 servings', ...props }
  });

describe('the nutrition figure in the meta line', () => {
  const nutrition = (onopen = vi.fn()) => ({
    label: 'at least 520 kcal',
    ariaLabel: 'at least 520 kcal, show nutrition',
    onopen
  });

  it('follows time and portions, and opens the panel', async () => {
    const onopen = vi.fn();

    show({ nutrition: nutrition(onopen) });

    expect(document.querySelector('.meta')).toHaveTextContent(
      /35 min.*2 servings.*at least 520 kcal/
    );

    await userEvent.click(
      screen.getByRole('button', { name: 'at least 520 kcal, show nutrition' })
    );

    expect(onopen).toHaveBeenCalledOnce();
  });

  it('is absent while unknown, nothing counted or failed (no figure given)', () => {
    show({ nutrition: null });

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(document.querySelector('.meta')).not.toHaveTextContent('kcal');
  });

  it('is absent while cooking', () => {
    show({ cooking: true, nutrition: nutrition() });

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});

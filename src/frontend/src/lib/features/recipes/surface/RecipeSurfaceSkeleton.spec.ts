import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import RecipeSurfaceSkeleton from './RecipeSurfaceSkeleton.svelte';
import { renderWithProviders } from '$lib/test/render';

describe('RecipeSurfaceSkeleton', () => {
  it('marks the region as busy with an accessible loading announcement', () => {
    const { container } = renderWithProviders(RecipeSurfaceSkeleton);

    const region = screen.getByRole('article');
    expect(region).toHaveAttribute('aria-busy', 'true');
    expect(container.querySelectorAll('.skeleton').length).toBeGreaterThan(5);
  });

  it('hides individual placeholders from assistive technology', () => {
    const { container } = renderWithProviders(RecipeSurfaceSkeleton);

    for (const block of container.querySelectorAll('.skeleton')) {
      expect(block).toHaveAttribute('aria-hidden', 'true');
    }
  });

  it('can omit the hero photo placeholder when requested', () => {
    const withPhoto = renderWithProviders(RecipeSurfaceSkeleton, { props: { hasPhoto: true } });
    expect(withPhoto.container.querySelector('.hero')).not.toBeNull();

    const withoutPhoto = renderWithProviders(RecipeSurfaceSkeleton, {
      props: { hasPhoto: false }
    });
    expect(withoutPhoto.container.querySelector('.hero')).toBeNull();
  });
});

import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';
import PhotoField from './PhotoField.svelte';

/* "This picture was drawn by the AI" is about the picture on the recipe, so it needs one that was. */
const note = /created by AI/i;

const render = (props: Record<string, unknown>) =>
  renderWithProviders(PhotoField, {
    props: { recipeId: 'r1', imageId: null, onchange: () => {}, ...props }
  });

describe('owning up to a drawn picture', () => {
  it('says nothing while the recipe has no picture', () => {
    render({ drawn: true });

    expect(screen.queryByText(note)).toBeNull();
  });

  it('says nothing beside a photograph', () => {
    render({ imageId: 'i1', drawn: false });

    expect(screen.queryByText(note)).toBeNull();
  });

  it('says so beside a picture the assistant drew', () => {
    render({ imageId: 'i1', drawn: true });

    expect(screen.getByText(note)).toBeInTheDocument();
  });
});

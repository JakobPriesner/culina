import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import ImageField from './ImageField.svelte';
import { renderWithProviders } from '$lib/test/render';

const labels = {
  label: 'Photo',
  hint: 'One picture of the finished dish.',
  chooseLabel: 'Choose a photo',
  replaceLabel: 'Replace the photo',
  removeLabel: 'Remove the photo'
};

const render = (props: Record<string, unknown> = {}) =>
  renderWithProviders(ImageField, {
    props: { ...labels, onpick: vi.fn(), onremove: vi.fn(), ...props }
  });

describe('with no picture yet', () => {
  it('shows the template rather than a gap, and says what goes in it', () => {
    render();

    expect(screen.getByText('One picture of the finished dish.')).toBeInTheDocument();
  });

  it('asks for one, and offers nothing to remove', () => {
    render();

    expect(screen.getByRole('button', { name: 'Choose a photo' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove the photo' })).not.toBeInTheDocument();
  });

  it('reports the file that was chosen', async () => {
    const onpick = vi.fn();

    render({ onpick });

    const file = new File(['bytes'], 'dinner.jpg', { type: 'image/jpeg' });

    await userEvent.upload(screen.getByLabelText('Choose a photo'), file);

    expect(onpick).toHaveBeenCalledWith(file);
  });
});

describe('with a picture', () => {
  const withPhoto = { src: 'https://example.test/photo.jpg' };

  it('shows it instead of the template', () => {
    render(withPhoto);

    expect(screen.queryByText('One picture of the finished dish.')).not.toBeInTheDocument();
    expect(screen.getByRole('presentation')).toHaveAttribute('src', withPhoto.src);
  });

  it('offers to replace it rather than to choose one', () => {
    render(withPhoto);

    expect(screen.getByRole('button', { name: 'Replace the photo' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Choose a photo' })).not.toBeInTheDocument();
  });

  it('offers to take it away', async () => {
    const onremove = vi.fn();

    render({ ...withPhoto, onremove });

    await userEvent.click(screen.getByRole('button', { name: 'Remove the photo' }));

    expect(onremove).toHaveBeenCalled();
  });

  it('will not remove a picture while one is being uploaded over it', () => {
    render({ ...withPhoto, busy: true });

    expect(screen.getByRole('button', { name: 'Remove the photo' })).toBeDisabled();
  });
});

describe('where the things you can do to a picture are', () => {
  const withPhoto = { src: 'https://example.test/photo.jpg' };

  /** What the browser answers when the component asks how wide the screen is. */
  const width = (narrow: boolean) =>
    vi.stubGlobal('matchMedia', (query: string) => ({
      matches: narrow,
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false
    }));

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('lays them on the picture where there is a pointer to reveal them with', () => {
    width(false);

    const { container } = render(withPhoto);

    // On the picture, inside the frame: the strip is revealed by hovering, and
    // a strip left there permanently covers the bottom of every photograph.
    expect(container.querySelector('.frame .overlay')).not.toBeNull();
    expect(container.querySelector('.filled-actions')).toBeNull();
  });

  it('puts them under it on a phone or a tablet, where nothing can hover', () => {
    width(true);

    const { container } = render(withPhoto);

    expect(container.querySelector('.frame .overlay')).toBeNull();
    expect(container.querySelector('.filled-actions')).not.toBeNull();
  });

  it('offers the same three either way', () => {
    width(true);

    render(withPhoto);

    expect(screen.getByRole('button', { name: 'Replace the photo' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove the photo' })).toBeInTheDocument();
  });
});

describe('when something goes wrong', () => {
  it('says so where it will be read out', () => {
    render({ failure: 'That photo could not be saved.' });

    expect(screen.getByRole('alert')).toHaveTextContent('That photo could not be saved.');
  });
});

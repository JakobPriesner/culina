import { fireEvent, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '#lib/test/render.js';
import Image from './Image.svelte';
import { useImageSource } from './imageSources';

describe('Image', () => {
  it('retains the description when a photo fails and can display a replacement', async () => {
    const view = renderWithProviders(Image, { props: { src: '/missing.webp', alt: 'Lemon orzo' } });
    await fireEvent.error(screen.getByRole('img', { name: 'Lemon orzo' }));
    expect(screen.getByRole('img', { name: 'Lemon orzo' }).tagName).toBe('DIV');
    expect(view.container.querySelector('img')).toBeNull();

    await view.rerender({ src: '/replacement.webp', alt: 'Tomato toast' });
    const replacement = screen.getByRole('img', { name: 'Tomato toast' });
    expect(replacement).toHaveAttribute('src', '/replacement.webp');
    await fireEvent.load(replacement);
    expect(replacement).toHaveClass('loaded');
  });

  it('draws the placeholder when there is no photo at all', () => {
    const view = renderWithProviders(Image, { props: { alt: '' } });

    expect(view.container.querySelector('img')).toBeNull();
    expect(view.container.querySelector('.fallback')).toBeInTheDocument();
  });

  it('keeps a decorative fallback out of the accessibility tree', async () => {
    const view = renderWithProviders(Image, { props: { src: '/missing.webp', alt: '' } });
    await fireEvent.error(view.container.querySelector('img')!);
    expect(screen.queryByRole('img')).toBeNull();
    expect(view.container.querySelector('.fallback')).toHaveAttribute('aria-hidden', 'true');
  });

  it('fetches lazily and without a hint unless told the photo is on screen at first paint', () => {
    const view = renderWithProviders(Image, { props: { src: '/a.webp', alt: '' } });
    const img = view.container.querySelector('img')!;

    expect(img).toHaveAttribute('loading', 'lazy');
    expect(img).not.toHaveAttribute('fetchpriority');
  });

  it('passes on a fetch priority', () => {
    const view = renderWithProviders(Image, {
      props: { src: '/a.webp', alt: '', loading: 'eager', fetchpriority: 'high' }
    });
    const img = view.container.querySelector('img')!;

    expect(img).toHaveAttribute('loading', 'eager');
    expect(img).toHaveAttribute('fetchpriority', 'high');
  });
});

describe('Image in the app, where photos are fetched first', () => {
  afterEach(() => useImageSource(null));

  it('asks again when the device comes back online after the first answer was a failure', async () => {
    let calls = 0;
    const released: string[] = [];

    useImageSource(
      () => Promise.resolve(++calls === 1 ? null : 'blob:photo'),
      (url) => released.push(url)
    );

    const view = renderWithProviders(Image, { props: { src: '/api/v1/photo', alt: 'Orzo' } });

    await vi.waitFor(() => expect(view.container.querySelector('.fallback')).toBeInTheDocument());

    window.dispatchEvent(new Event('online'));

    await vi.waitFor(() =>
      expect(view.container.querySelector('img')).toHaveAttribute('src', 'blob:photo')
    );
    expect(calls).toBe(2);
    // The failed ask was released at once; the shown one is held until the image goes.
    expect(released).toEqual(['/api/v1/photo']);

    view.unmount();
    expect(released).toEqual(['/api/v1/photo', '/api/v1/photo']);
  });
});

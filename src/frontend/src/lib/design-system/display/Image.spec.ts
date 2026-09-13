import { fireEvent, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '$lib/test/render';
import Image from './Image.svelte';

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

  it('keeps a decorative fallback out of the accessibility tree', async () => {
    const view = renderWithProviders(Image, { props: { src: '/missing.webp', alt: '' } });
    await fireEvent.error(view.container.querySelector('img')!);
    expect(screen.queryByRole('img')).toBeNull();
    expect(view.container.querySelector('.fallback')).toHaveAttribute('aria-hidden', 'true');
  });
});

<script lang="ts">
  import { imageSource, pickCandidate } from './imageSources';

  /**
   * A photo that does not move the page: the box is reserved from the aspect ratio, on a warm tint (grey reads as a broken image mid-fade).
   * A missing photo draws the same box with a mark, so grids keep their shape.
   */
  interface Props {
    /** Absent when there is no photo, which draws the placeholder. */
    src?: string;
    /** What the photo shows. Empty string when it is purely decorative. */
    alt: string;
    ratio?: number;
    /** `eager` only for the one image already on screen at first paint. */
    loading?: 'lazy' | 'eager';
    /** `high` for the photo that is the first paint's largest element, so it is not queued behind its neighbours. */
    fetchpriority?: 'high' | 'low';
    /** Candidate widths, so a phone does not download a desktop photo. */
    srcset?: string;
    sizes?: string;
    rounded?: boolean;
    fill?: boolean;
  }

  let {
    src,
    alt,
    ratio = 4 / 3,
    loading = 'lazy',
    fetchpriority,
    srcset,
    sizes,
    rounded = true,
    fill = false
  }: Props = $props();

  let loadedSource = $state<string>();
  let failedSource = $state<string>();

  /** Set only where photos must be fetched before they can be shown (the app); otherwise the browser loads `src` itself. */
  const resolve = imageSource();
  let width = $state(0);
  let resolved = $state<{ for: string; url: string }>();

  // Measured first, so the fetched candidate fits the box as `srcset` would have chosen; asked again only when the photo changes.
  const wanted = $derived(resolve && src && width > 0 ? pickCandidate(src, srcset, width) : null);

  $effect(() => {
    const url = wanted;
    const owner = src;

    if (!resolve || !url || !owner || resolved?.for === owner) {
      return;
    }

    let current = true;

    void resolve(url).then((answer) => {
      if (!current) {
        return;
      }

      if (answer) {
        resolved = { for: owner, url: answer };
      } else {
        failedSource = owner;
      }
    });

    return () => {
      current = false;
    };
  });

  /** Read once: the box is reserved from its ratio, so its width is known before any photo arrives. The website never measures. */
  function measure(frame: HTMLDivElement) {
    width = frame.clientWidth || 400;
  }

  const shown = $derived(
    resolve ? (resolved && resolved.for === src ? resolved.url : undefined) : src
  );
</script>

<div
  class="frame"
  class:rounded
  class:fill
  style:aspect-ratio={fill ? undefined : ratio}
  {@attach resolve && measure}
>
  {#if !src || failedSource === src}
    <div
      class="fallback"
      role={alt ? 'img' : undefined}
      aria-label={alt || undefined}
      aria-hidden={alt ? undefined : true}
    >
      <svg
        aria-hidden="true"
        viewBox="0 0 48 48"
        fill="none"
        stroke="currentColor"
        stroke-width="1.5"
        ><path
          d="M8 25h32c-1 10-7 15-16 15S9 35 8 25ZM5 25h38M18 17c-4-5 4-5 0-10m12 10c-4-5 4-5 0-10"
          stroke-linecap="round"
        /></svg
      >
    </div>
  {:else if shown}
    {#key src}<img
        class="image"
        class:loaded={loadedSource === src}
        src={shown}
        {alt}
        {loading}
        {fetchpriority}
        srcset={resolve ? undefined : srcset}
        {sizes}
        decoding="async"
        onload={() => (loadedSource = src)}
        onerror={() => (failedSource = src)}
      />{/key}
  {/if}
</div>

<style>
  .frame {
    position: relative;
    width: 100%;
    overflow: hidden;
    background: var(--surface-accent-subtle);
  }

  .rounded {
    border-radius: var(--radius-lg);
  }

  .fill {
    height: 100%;
    min-height: 0;
  }

  .fill .image,
  .fallback {
    position: absolute;
    inset: 0;
  }

  .fallback {
    display: grid;
    place-items: center;
    color: var(--text-muted);
  }

  .fallback svg {
    width: var(--space-12);
    height: var(--space-12);
  }

  .image {
    width: 100%;
    height: 100%;
    object-fit: cover;
    opacity: 0;
    transition: opacity var(--duration-slow) var(--ease-out);
  }

  .loaded {
    opacity: 1;
  }

  /* Without the fade the image just appears; what must not happen is staying invisible. */
  @media (prefers-reduced-motion: reduce) {
    .image {
      opacity: 1;
      transition: none;
    }
  }
</style>

<script lang="ts">
  /**
   * A photo that does not move the page when it arrives.
   *
   * The box is reserved from the aspect ratio before anything loads, so text
   * below never jumps down mid-read — which is both the most irritating thing a
   * page can do and the thing that gets noticed last, because it only happens
   * on a slow connection.
   *
   * The backdrop is a warm tint rather than grey: a food photo fading in over
   * grey looks like a broken image for the moment before it lands.
   */
  interface Props {
    src: string;
    /** What the photo shows. Empty string when it is purely decorative. */
    alt: string;
    /** Width over height, e.g. 4 / 3. */
    ratio?: number;
    /** `eager` only for the one image already on screen at first paint. */
    loading?: 'lazy' | 'eager';
    /** Candidate widths, so a phone does not download a desktop photo. */
    srcset?: string;
    sizes?: string;
    rounded?: boolean;
    /** Fill a parent that already reserves its own height. */
    fill?: boolean;
  }

  let {
    src,
    alt,
    ratio = 4 / 3,
    loading = 'lazy',
    srcset,
    sizes,
    rounded = true,
    fill = false
  }: Props = $props();

  let loadedSource = $state<string>();
  let failedSource = $state<string>();
</script>

<div class="frame" class:rounded class:fill style:aspect-ratio={fill ? undefined : ratio}>
  {#if failedSource === src}
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
  {:else}
    {#key src}<img
        class="image"
        class:loaded={loadedSource === src}
        {src}
        {alt}
        {loading}
        {srcset}
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

  /* Without the fade the image simply appears, which is fine — what must not
     happen is that it stays invisible. */
  @media (prefers-reduced-motion: reduce) {
    .image {
      opacity: 1;
      transition: none;
    }
  }
</style>

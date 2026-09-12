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
  }

  let {
    src,
    alt,
    ratio = 4 / 3,
    loading = 'lazy',
    srcset,
    sizes,
    rounded = true
  }: Props = $props();

  let loaded = $state(false);
</script>

<div class="frame" class:rounded style:aspect-ratio={ratio}>
  <img
    class="image"
    class:loaded
    {src}
    {alt}
    {loading}
    {srcset}
    {sizes}
    decoding="async"
    onload={() => (loaded = true)}
  />
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

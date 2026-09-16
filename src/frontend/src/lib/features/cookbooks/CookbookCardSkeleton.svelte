<script lang="ts">
  import { Skeleton } from '$ds';

  /**
   * The shape of a cookbook before it arrives.
   *
   * A square where the cover will be, then the two lines under it, at the same
   * heights — so nothing moves when the real thing lands. A skeleton that does
   * not match what replaces it is a layout shift with extra steps.
   */
</script>

<div class="cookbook" aria-hidden="true">
  <Skeleton shape="block" height="0" />
  <Skeleton width="70%" height="1.4em" />
  <Skeleton width="40%" />
</div>

<style>
  .cookbook {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  /* The cover is square, and its placeholder has to be too or the row below it
     lands somewhere else once the pictures arrive. */
  .cookbook > :global(:first-child) {
    aspect-ratio: 1;
    height: auto;
    border-radius: var(--radius-lg);
    margin-bottom: var(--space-2);
  }
</style>

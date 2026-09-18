<script lang="ts">
  import { Skeleton } from '$ds';

  /**
   * The shape of a recipe row before it arrives.
   *
   * Deliberately the same rhythm and roughly the same heights as the real
   * thing — a 4:3 photo box first, then the eyebrow, title and meta line — so
   * nothing moves when the content lands. A skeleton that does not match what
   * replaces it is a layout shift with extra steps.
   */
</script>

<div class="recipe" aria-hidden="true">
  <div class="photo"><Skeleton shape="block" /></div>
  <Skeleton width="5rem" />
  <Skeleton width="80%" height="1.6em" />
  <Skeleton width="60%" />
</div>

<style>
  .recipe {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-bottom: var(--space-4);
  }
  .photo {
    aspect-ratio: 4 / 3;
    margin-bottom: var(--space-2);
  }
</style>

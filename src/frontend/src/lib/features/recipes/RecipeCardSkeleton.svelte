<script lang="ts">
  import { Skeleton } from '$ds';

  /**
   * The shape of a recipe row before it arrives.
   *
   * Deliberately the same hairline, the same rhythm and roughly the same
   * heights as the real thing, so nothing moves when the content lands. A
   * skeleton that does not match what replaces it is a layout shift with extra
   * steps.
   */
</script>

<div class="recipe" aria-hidden="true">
  <Skeleton width="5rem" />
  <Skeleton width="80%" height="1.6em" />
  <Skeleton width="60%" />
</div>

<style>
  .recipe {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding-block: var(--space-6);
    border-top: 1px solid var(--border);
  }
</style>

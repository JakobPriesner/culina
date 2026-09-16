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
  <div class="mark"><Skeleton width="2rem" height="2rem" /></div>
  <Skeleton width="5rem" />
  <Skeleton width="80%" height="1.6em" />
  <Skeleton width="60%" />
</div>

<style>
  .recipe {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-height: 17rem;
    border: 1px solid var(--border);
    padding: var(--space-6);
    border-radius: var(--radius-lg);
    background: var(--surface-raised);
    justify-content: flex-end;
  }
  .mark {
    margin-bottom: auto;
    padding-bottom: var(--space-6);
  }
</style>

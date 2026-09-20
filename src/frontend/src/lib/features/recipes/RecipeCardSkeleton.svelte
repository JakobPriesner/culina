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
  <div class="eyebrow"><Skeleton width="4.5rem" height="0.75rem" /></div>
  <div class="title"><Skeleton width="80%" height="1.6rem" /></div>
  <div class="details">
    <Skeleton width="55%" height="1rem" />
    <Skeleton shape="circle" width="var(--space-8)" height="var(--space-8)" />
  </div>
</div>

<style>
  .recipe {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
    height: 100%;
    padding-bottom: var(--space-4);
  }
  .photo {
    aspect-ratio: 4 / 3;
    margin-bottom: var(--space-2);
  }
  .eyebrow {
    display: flex;
    align-items: center;
    height: 1rem;
  }
  .title {
    margin-block: 0.125rem;
  }
  .details {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
  }
</style>

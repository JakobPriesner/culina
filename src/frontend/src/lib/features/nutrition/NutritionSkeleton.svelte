<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  /** Matches the summary card's title and figure so loading has the same footprint. */
</script>

<div class="row" aria-busy="true" aria-label={m['nutrition.loading']()}>
  <Skeleton width="8rem" height="1.5rem" />
  <div class="overview">
    <div class="words">
      <Skeleton width="min(12rem, 80%)" height="3.5rem" />
      <Skeleton width="min(10rem, 70%)" height="1rem" />
    </div>
    <div class="macros">
      {#each [0, 1, 2] as column (column)}
        <div class="words">
          <Skeleton width="80%" height="1rem" />
          <Skeleton width="70%" height="1.5rem" />
        </div>
      {/each}
    </div>
  </div>
  <div class="action"><Skeleton width="7rem" height="1rem" /></div>
</div>

<style>
  .row {
    display: grid;
    gap: var(--space-6);
    padding: var(--space-8);
  }

  .overview {
    display: grid;
    gap: var(--space-6);
  }

  .macros {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: var(--space-4);
  }

  .action {
    padding-top: var(--space-4);
    border-top: 1px solid var(--border);
  }

  .words {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    flex: 1;
    min-width: 0;
  }

  @container (width >= 52rem) {
    .overview {
      grid-template-columns: minmax(0, 2fr) minmax(0, 3fr);
      gap: var(--space-12);
      align-items: center;
    }
  }

  @container (width < 32rem) {
    .row {
      padding: var(--space-6);
    }
  }

  @media print {
    .row {
      display: none;
    }
  }
</style>

<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  /** The closed disclosure's row: a chevron, a label and the headline; the same height, so nothing moves when it arrives. */
</script>

<div class="row" aria-busy="true" aria-label={m['nutrition.loading']()}>
  <Skeleton shape="block" width="var(--space-4)" height="var(--space-4)" />
  <Skeleton width="5rem" height="1rem" />
  <Skeleton width="min(14rem, 45%)" height="1rem" />
</div>

<style>
  .row {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
  }

  @media print {
    .row {
      display: none;
    }
  }
</style>

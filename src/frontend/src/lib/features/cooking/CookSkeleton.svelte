<!-- The cooking screen's shape, while the recipe it will show is on its way. -->
<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';
  import CookingControlsBar from './CookingControlsBar.svelte';
</script>

<div class="cook-skeleton" aria-busy="true" aria-label={m['recipes.list.loading']()}>
  <div class="back">
    <Skeleton width="6rem" height="1.5rem" />
  </div>

  <div class="cook-head">
    <Skeleton width="50%" height="2rem" />
    <Skeleton width="25%" height="1rem" />
  </div>

  <div class="cook-servings">
    <Skeleton shape="block" width="8.5rem" height="var(--control-sm)" />
  </div>

  <div class="cook-step-card">
    <div class="cook-step-header">
      <Skeleton shape="circle" width="2.25rem" height="2.25rem" />
      <Skeleton width="5rem" height="1.25rem" />
    </div>
    <div class="cook-step-body">
      <Skeleton width="95%" height="1.25rem" />
      <Skeleton width="85%" height="1.25rem" />
      <Skeleton width="70%" height="1.25rem" />
    </div>
  </div>

  <CookingControlsBar decorative>
    <Skeleton width="6rem" height="1rem" />
    <div class="moves">
      <Skeleton shape="circle" width="var(--control-lg)" height="var(--control-lg)" />
      <div class="advance">
        <Skeleton shape="block" width="9rem" height="var(--control-lg)" />
      </div>
    </div>
  </CookingControlsBar>
</div>

<style>
  .cook-skeleton {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .cook-head {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .cook-servings {
    padding-block: var(--space-4);
    border-block: 1px solid var(--border);
  }

  .cook-step-card {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-6);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
    min-height: 14rem;
  }

  .cook-step-header {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .cook-step-body {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }
</style>

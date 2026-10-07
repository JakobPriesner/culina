<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  import EditorLayout from './EditorLayout.svelte';
</script>

<EditorLayout aria-busy="true" aria-label={m['editor.loading']()}>
  {#snippet rail()}
    <div class="ghost-rail">
      <Skeleton width="5rem" height="1.25rem" />
      <Skeleton width="10rem" height="1.5rem" />
      <Skeleton width="6rem" height="1rem" />

      <div class="ghost-sections">
        {#each [0, 1, 2, 3, 4] as i (i)}
          <div class="ghost-section-row">
            <Skeleton width="6rem" height="1rem" />
          </div>
        {/each}
      </div>
    </div>
  {/snippet}

  <div class="ghost">
    <Skeleton width="8rem" height="1.75rem" />
    <div class="ghost-field">
      <Skeleton width="4rem" height="0.875rem" />
      <Skeleton width="100%" height="3rem" shape="block" />
    </div>
    <div class="ghost-field">
      <Skeleton width="6rem" height="0.875rem" />
      <Skeleton width="100%" height="5rem" shape="block" />
    </div>
    <div class="ghost-meta">
      <div class="ghost-pair">
        <Skeleton width="100%" height="var(--control-sm)" shape="block" />
        <Skeleton width="100%" height="var(--control-sm)" shape="block" />
      </div>
      <div class="ghost-pair">
        <Skeleton width="100%" height="var(--control-sm)" shape="block" />
        <Skeleton width="100%" height="var(--control-sm)" shape="block" />
      </div>
    </div>
  </div>

  <div class="ghost">
    <Skeleton width="5rem" height="1.75rem" />
    <Skeleton width="100%" height="10rem" shape="block" />
  </div>

  <div class="ghost">
    <Skeleton width="7rem" height="1.75rem" />
    <div class="ghost-list">
      {#each [0, 1, 2, 3] as row (row)}
        <div class="ghost-row">
          <Skeleton width="4rem" height="var(--control-sm)" shape="block" />
          <Skeleton width="100%" height="var(--control-sm)" shape="block" />
        </div>
      {/each}
    </div>
  </div>
</EditorLayout>

<style>
  .ghost,
  .ghost-rail {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .ghost-sections {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-top: var(--space-4);
  }

  .ghost-section-row {
    display: flex;
    align-items: center;
    min-height: var(--control-sm);
  }

  .ghost-field {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .ghost-meta {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-4);
  }

  .ghost-pair {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--space-3);
  }

  .ghost-list {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .ghost-row {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .ghost + .ghost {
    margin-top: var(--layout-section-gap);
  }
</style>

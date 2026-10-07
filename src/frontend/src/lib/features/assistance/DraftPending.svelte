<script lang="ts">
  import { GenerationAura, Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  import DraftProgress from './DraftProgress.svelte';

  /**
   * What the review shows while the assistant is still writing: the progress
   * line, and before anything has arrived the shape of what is coming.
   */
  interface Props {
    /** Whether any of the draft has arrived yet. */
    arriving: boolean;
  }

  let { arriving }: Props = $props();
</script>

<div class="progress">
  <GenerationAura />
  <DraftProgress
    label={arriving ? m['assist.improve.writing']() : m['assist.improve.asking']()}
    {arriving}
  />
</div>

{#if !arriving}
  <div class="forming" aria-hidden="true">
    <Skeleton width="9rem" height="1rem" />
    <Skeleton width="100%" height="3.5rem" shape="block" />
    <Skeleton width="7rem" height="1rem" />
    <Skeleton width="82%" height="1rem" />
  </div>
{/if}

<style>
  /* Its own ground rather than a tint: the assistant's glow round the edge is
     what marks it out, and an accent wash underneath would muddy the colours. */
  .progress {
    position: relative;
    isolation: isolate;
    margin: var(--space-4) var(--space-1) 0;
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
  }

  .forming {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    margin-top: var(--space-4);
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }
</style>

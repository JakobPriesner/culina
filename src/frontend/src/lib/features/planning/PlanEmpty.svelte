<script lang="ts">
  import { Button } from '$ds';
  import OlliSays from '$features/onboarding/OlliSays.svelte';
  import { m } from '$shell/i18n';

  /**
   * Nothing planned on the days shown. One state for the place: Olli only while the list is empty too (the pane then
   * shows just its add field); a list in use gets a quiet line, as nothing about it is missing.
   */
  interface Props {
    listInUse: boolean;
    /** Days are rows here, and Olli the larger pot. */
    wide: boolean;
    onplan: () => void;
  }

  let { listInUse, wide, onplan }: Props = $props();
</script>

{#if listInUse}
  <p class="nothing-planned">
    {m['plan.empty.title']()}
    <button type="button" class="text-action hit-target reach" onclick={onplan}
      >{m['plan.empty.action']()}</button
    >
  </p>
{:else}
  <!-- Olli under the days and talking about them, in line with them: centred in the column, a page-sized empty state
       floated below the rows it was about, often below the fold. -->
  <div class="empty-week" class:wide>
    <OlliSays pose="peeking" size={wide ? 'md' : 'sm'}>
      <h2 class="empty-title">{m['plan.empty.title']()}</h2>
      <p>{m['plan.empty.description']()}</p>
      <div class="empty-action">
        <Button variant="primary" onclick={onplan}>{m['plan.empty.action']()}</Button>
      </div>
    </OlliSays>
  </div>
{/if}

<style>
  .empty-week {
    margin-top: var(--space-6);
  }

  /* Olli at the bubble's top, as in a coach tip: the button makes the bubble taller than the pot. */
  .empty-week :global(.says) {
    align-items: flex-start;
  }

  .empty-week :global(.bubble) {
    max-width: 34rem;
  }

  /* The tail at the pot's middle: the small one on a phone, so the bubble keeps the width to read in. */
  .empty-week :global(.bubble::before) {
    top: 1.4rem;
  }

  .empty-week.wide :global(.bubble::before) {
    top: 2.4rem;
  }

  .empty-title {
    margin-bottom: var(--space-1);
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.02em;
  }

  .empty-week p {
    color: var(--text-muted);
    text-wrap: pretty;
  }

  .empty-action {
    margin-top: var(--space-3);
  }

  .nothing-planned {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-top: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>

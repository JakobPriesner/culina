<script lang="ts">
  import type { Snippet } from 'svelte';

  import { Button } from '$ds';
  import { m } from '$shell/i18n';
  import type { Pose } from '$shell/olli/poses';

  import OlliSays from './OlliSays.svelte';
  import { tips, type TipId } from './tips.svelte';

  /**
   * Olli beside the thing it explains, the first time it is used, and never again after "Got it". In the
   * flow and never over a control; the pose says what kind of moment it is (writing, cooking, planning).
   */
  interface Props {
    id: TipId;
    pose: Pose;
    title: string;
    children: Snippet;
    /** Something to do about it, besides putting it away. */
    action?: Snippet;
  }

  let { id, pose, title, children, action }: Props = $props();
</script>

{#if tips.isNew(id)}
  <aside class="tip" aria-label={title}>
    <OlliSays {pose} size="sm">
      <p class="title">{title}</p>
      <div class="body">{@render children()}</div>
      <div class="actions">
        {@render action?.()}
        <Button size="sm" variant="ghost" onclick={() => tips.dismiss(id)}
          >{m['tips.gotIt']()}</Button
        >
      </div>
    </OlliSays>
  </aside>
{/if}

<style>
  .tip {
    margin-bottom: var(--space-4);
  }

  /* Olli at the bubble's top, not its middle: a tall bubble would leave Olli floating beside nothing. */
  .tip :global(.says) {
    align-items: flex-start;
  }

  .tip :global(.bubble::before) {
    top: 1.4rem;
  }

  .title {
    font-weight: var(--weight-semibold);
  }

  .body {
    margin-top: var(--space-1);
    line-height: var(--leading-normal);
  }

  .body :global(ul) {
    margin: 0;
    padding-inline-start: var(--space-4);
  }

  .body :global(li + li) {
    margin-top: var(--space-1);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: flex-end;
    gap: var(--space-2);
    margin-top: var(--space-2);
  }

  @media print {
    .tip {
      display: none;
    }
  }
</style>

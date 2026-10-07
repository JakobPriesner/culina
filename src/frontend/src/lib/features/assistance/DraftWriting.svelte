<script lang="ts">
  import { GenerationAura, Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  import DraftProgress from './DraftProgress.svelte';
  import type { Draft } from './draftToRecipe';

  /** The recipe arriving: streaming turns tens of seconds of spinner into something read. Nothing is interactive (controls belong to review), and only completed fields appear, so lines never stutter. */
  interface Props {
    /** What has arrived so far, or null before anything has. */
    draft: Draft | null;
    /** Whether more is still coming. */
    writing: boolean;
    showProgress?: boolean;
  }

  let { draft, writing, showProgress = true }: Props = $props();

  const lines = $derived(draft?.groups.flatMap((group) => group.ingredients) ?? []);
  const steps = $derived(draft?.steps ?? []);
</script>

<!-- One polite live region for the lot: it announces the draft, not each of twenty lines. -->
<section class="writing" aria-busy={writing} aria-live="polite">
  <GenerationAura active={writing} />

  {#if writing && showProgress}
    <DraftProgress label={m['assist.writing']()} arriving={draft !== null} />
  {/if}

  {#if draft?.title}
    <h3 class="title arrival">{draft.title}</h3>
  {:else if writing}
    <Skeleton width="60%" height="1.6em" />
  {/if}

  {#if draft?.description}
    <p class="description arrival">{draft.description}</p>
  {/if}

  {#if lines.length > 0}
    <div class="part arrival">
      <h4 class="part-title">{m['assist.part.ingredients']()}</h4>
      <ul class="lines">
        {#each lines as line, index (index)}
          <li class="arrival">
            {[line.quantity, line.unit, line.name].filter(Boolean).join(' ')}
            {#if line.note}<span class="note">, {line.note}</span>{/if}
          </li>
        {/each}
      </ul>
    </div>
  {/if}

  {#if steps.length > 0}
    <div class="part arrival">
      <h4 class="part-title">{m['assist.part.steps']()}</h4>
      <ol class="steps">
        {#each steps as step, index (index)}
          <li class="arrival">
            {#if step.title}<span class="step-title">{step.title}</span>{/if}
            {step.text}
          </li>
        {/each}
      </ol>
    </div>
  {/if}
</section>

<style>
  .writing {
    position: relative;
    isolation: isolate;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
    line-height: var(--leading-tight);
  }

  .description {
    max-width: var(--measure);
    color: var(--text-muted);
    line-height: var(--leading-normal);
  }

  .part {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  .part-title {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .lines,
  .steps {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .lines {
    list-style: none;
  }

  .steps {
    gap: var(--space-2);
    padding-left: var(--space-6);
  }

  .note {
    color: var(--text-muted);
  }

  .step-title {
    display: block;
    font-weight: var(--weight-semibold);
  }

  /* Each completed field or line enters once, making the stream visible instead of swapping a loader for a finished block. */
  .arrival {
    animation: arrive var(--duration-base) var(--ease-out) both;
  }

  @keyframes arrive {
    from {
      opacity: 0;
      transform: translateY(var(--space-1));
    }

    to {
      opacity: 1;
      transform: translateY(0);
    }
  }

  /* The pulse is also in the words beside it, so reduced motion loses nothing. */
  @media (prefers-reduced-motion: reduce) {
    .arrival {
      animation: none;
    }
  }
</style>

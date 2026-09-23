<script lang="ts">
  import { GenerationStatus, Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  import type { Draft } from './draftToRecipe';

  /**
   * The recipe, arriving.
   *
   * The whole reason the assistant streams. A model takes tens of seconds over
   * a recipe, and forty seconds of a spinner is forty seconds of wondering
   * whether it has broken — where a title at three seconds, ingredients at
   * eight and steps filling in after that is something somebody reads. By the
   * time it is finished they have already decided whether they want it.
   *
   * Nothing here is interactive, and that is deliberate: this is the recipe
   * being written, not the recipe being reviewed. Every control belongs to
   * whatever comes after.
   *
   * It shows only what has actually been written. The server sends a field once
   * the model has finished saying it and not before, so a half-typed
   * ingredient is absent rather than flickering through its own letters —
   * lines appear, they do not stutter.
   */
  interface Props {
    /** What has arrived so far, or null before anything has. */
    draft: Draft | null;
    /** Whether more is still coming. */
    writing: boolean;
  }

  let { draft, writing }: Props = $props();

  const lines = $derived(draft?.groups.flatMap((group) => group.ingredients) ?? []);
  const steps = $derived(draft?.steps ?? []);
</script>

<!--
  One live region for the lot, and polite. A reader being interrupted twenty
  times while a recipe is typed out is worse than being told once, at the end,
  what it says — so this announces the draft rather than each line of it.
-->
<section class="writing" aria-busy={writing} aria-live="polite">
  {#if writing}
    <GenerationStatus label={m['assist.writing']()} />
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

  /* Every completed field or line enters once. That makes the transport
     visible: the recipe grows as stream events land instead of replacing a
     frozen loader with a finished block. */
  .arrival {
    animation: arrive 320ms var(--ease-out) both;
  }

  @keyframes arrive {
    from {
      opacity: 0;
      transform: translateY(0.45rem);
    }

    to {
      opacity: 1;
      transform: translateY(0);
    }
  }

  /* A pulse is information here, not decoration — but the information is also
     in the words beside it, so somebody who asked for less motion loses
     nothing. */
  @media (prefers-reduced-motion: reduce) {
    .arrival {
      animation: none;
    }
  }
</style>

<script lang="ts">
  import { GenerationAura, SectionLabel, Skeleton } from '$ds';
  import { formatQuantity } from '$features/recipes/formatQuantity';
  import { quantityLabels } from '$features/recipes/quantityLabels';
  import { scaleQuantity } from '$features/recipes/scaling';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

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

  /** The unit in the reader's language ("EL", not the stored `tbsp`), the same as the recipe will show it. */
  const amountOf = (line: (typeof lines)[number]): string =>
    formatQuantity(
      scaleQuantity({ value: line.quantity ?? null, unit: line.unit ?? null }, 1),
      preferences.locale,
      quantityLabels
    ).text;

  /** What the draft says about the yield and the time, so a figure read off the source is seen rather than lost. */
  const facts = $derived.by(() => {
    if (!draft) {
      return [];
    }

    const yielded =
      draft.yieldAmount == null
        ? null
        : draft.yieldKind === 'pieces'
          ? `${draft.yieldAmount} ${draft.yieldLabel ?? ''}`.trim()
          : m['recipes.meta.servings']({ count: draft.yieldAmount });

    return [
      yielded,
      ...[draft.prepMinutes, draft.cookMinutes].map((minutes) =>
        minutes == null ? null : m['assist.minutes']({ count: minutes })
      )
    ].filter((fact): fact is string => fact !== null);
  });
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

  {#if facts.length > 0}
    <p class="facts arrival">{facts.join(m['recipes.meta.separator']())}</p>
  {/if}

  {#if lines.length > 0}
    <div class="part arrival">
      <SectionLabel as="h4">{m['assist.part.ingredients']()}</SectionLabel>
      <ul class="lines">
        {#each lines as line, index (index)}
          <li class="arrival">
            {[amountOf(line), line.name].filter(Boolean).join(' ')}
            {#if line.note}<span class="note">, {line.note}</span>{/if}
          </li>
        {/each}
      </ul>
    </div>
  {/if}

  {#if steps.length > 0}
    <div class="part arrival">
      <SectionLabel as="h4">{m['assist.part.steps']()}</SectionLabel>
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

  .facts {
    color: var(--text-muted);
    font-size: var(--text-sm);
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

<script lang="ts">
  import { resolve } from '$app/paths';

  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { metaLineFor } from '../recipeMeta';
  import type { NutritionLink } from './nutritionLink';
  import { createScaling } from './scaled.svelte';
  import StepText from './StepText.svelte';
  import { sourceLink } from '../sourceLink';
  import type { RecipeReading } from '../types';

  interface Props {
    recipe: RecipeReading;
    cooking: boolean;
    cookbooks: readonly { readonly id: string; readonly name: string }[];
    printedYield: string;
    nutrition?: NutritionLink | null;
    /** How often this person made it, said like on a recipe card ("2× cooked"). */
    cooked?: { readonly count: number; readonly lastMadeAt: string | null } | null;
  }

  let {
    recipe,
    cooking,
    cookbooks,
    printedYield,
    nutrition = null,
    cooked = null
  }: Props = $props();

  const lastMade = $derived(
    cooked?.lastMadeAt
      ? new Intl.DateTimeFormat(preferences.locale, { day: 'numeric', month: 'short' }).format(
          new Date(cooked.lastMadeAt)
        )
      : null
  );

  /** "2× cooked, last 23 Sept": the count as a recipe card says it, with when. */
  const cookedLine = $derived(
    cooked?.count
      ? [
          m['recipes.meta.cooked']({ count: cooked.count }),
          lastMade ? m['recipe.lastCooked']({ when: lastMade }) : ''
        ]
          .filter(Boolean)
          .join(', ')
      : ''
  );

  const metaLine = $derived(
    [metaLineFor(recipe, { yields: false }), printedYield, cookedLine]
      .filter(Boolean)
      .join(m['recipes.meta.separator']())
  );

  // A description holds no ingredient references; StepText only needs a scaling to type-check.
  const scaling = createScaling(
    () => recipe,
    () => recipe.yieldAmount
  );

  // Host only, not the full URL; no line at all when the address isn't an ordinary web address.
  const original = $derived(sourceLink(recipe.sourceUrl));
</script>

<p class="meta">
  <!-- The yield on screen, not the written one: "2 servings" above a stepper at 4 contradicts it. -->
  {metaLine}{#if nutrition && !cooking}{m['recipes.meta.separator']()}<button
      type="button"
      class="nutrition"
      aria-label={nutrition.ariaLabel}
      onclick={nutrition.onopen}>{nutrition.label}</button
    >{/if}
</p>

{#if recipe.description}
  <!-- Markdown like a step: the author's line breaks, lists, emphasis and links survive. -->
  <div class="description">
    <StepText step={{ segments: [{ kind: 'text', text: recipe.description }] }} {scaling} />
  </div>
{/if}

{#if cookbooks.length > 0 && !cooking}
  <p class="shelves">
    <span class="shelves-label">{m['cookbooks.recipe.inLabel']()}</span>
    {#each cookbooks as shelf, index (shelf.id)}
      <a
        class="hit-target"
        href={resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: shelf.id })}>{shelf.name}</a
      >{#if index < cookbooks.length - 1}<span aria-hidden="true">, </span>{/if}
    {/each}
  </p>
{/if}

<!-- Deliberately the quietest line, so "imported" doesn't read as a second kind of recipe. -->
{#if original && !cooking}
  <p class="origin">
    <!-- External link: resolve() is only for the app's own routes. -->
    <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
    <a href={original.href} rel="noreferrer nofollow" target="_blank">
      {m['import.origin.from']({ where: original.host })}
    </a>
  </p>
{/if}

<!-- Paper only: on screen the servings control says this. -->
<p class="printed-yield">{printedYield}</p>

<style>
  .meta,
  .description {
    color: var(--text-muted);
    margin-top: var(--space-3);
    max-width: var(--measure);
  }

  /* Paragraphs and lists carry no margins of their own (see StepText). */
  .description {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  /* A link in the line, not a control beside it: it wraps with the words and keeps their colour. */
  .nutrition {
    padding: 0;
    border: 0;
    background: none;
    color: inherit;
    font: inherit;
    text-decoration: underline;
    text-decoration-color: var(--border-strong, currentColor);
    text-underline-offset: 0.2em;
    cursor: pointer;
  }

  @media (hover: hover) {
    .nutrition:hover {
      color: var(--text);
    }
  }

  .shelves {
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .shelves-label {
    margin-right: var(--space-1);
  }

  .shelves a {
    color: inherit;
  }

  @media (hover: hover) {
    .shelves a:hover {
      color: var(--text);
    }
  }

  .origin {
    margin-top: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
  }

  .printed-yield {
    display: none;
  }

  @media (width < 52rem) {
    .meta,
    .description,
    .shelves {
      margin-top: var(--space-2);
    }

    /* Keep a long shelf name to one line on phones. */
    .shelves {
      display: flex;
      align-items: baseline;
      min-width: 0;
      overflow: hidden;
    }

    .shelves a {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .shelves-label {
      flex: none;
    }

    .origin {
      margin-top: var(--space-1);
    }
  }

  @media print {
    /* The meta line's yield is the written one, wrong beside a scaled ingredient list. */
    .meta {
      display: none;
    }

    .shelves {
      margin-top: var(--space-3);
    }

    .printed-yield {
      display: block;
      margin-top: 2mm;
      font-weight: var(--weight-medium);
    }
  }
</style>

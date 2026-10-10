<script lang="ts">
  import { resolve } from '$app/paths';

  import { m } from '$shell/i18n';
  import { metaLineFor } from '../recipeMeta';
  import type { NutritionLink } from './nutritionLink';
  import { sourceLink } from '../sourceLink';
  import type { RecipeReading } from '../types';

  interface Props {
    recipe: RecipeReading;
    cooking: boolean;
    cookbooks: readonly { readonly id: string; readonly name: string }[];
    printedYield: string;
    nutrition?: NutritionLink | null;
  }

  let { recipe, cooking, cookbooks, printedYield, nutrition = null }: Props = $props();

  // Host only, not the full URL; no line at all when the address isn't an ordinary web address.
  const original = $derived(sourceLink(recipe.sourceUrl));
</script>

<p class="meta">
  {metaLineFor(recipe)}{#if nutrition && !cooking}{m['recipes.meta.separator']()}<button
      type="button"
      class="nutrition"
      aria-label={nutrition.ariaLabel}
      onclick={nutrition.onopen}>{nutrition.label}</button
    >{/if}
</p>

{#if recipe.description}
  <p class="description">{recipe.description}</p>
{/if}

{#if cookbooks.length > 0 && !cooking}
  <p class="shelves">
    <span class="shelves-label">{m['cookbooks.recipe.inLabel']()}</span>
    {#each cookbooks as shelf, index (shelf.id)}
      <a href={resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: shelf.id })}>{shelf.name}</a
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

  .nutrition:hover {
    color: var(--text);
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

  .shelves a:hover {
    color: var(--text);
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

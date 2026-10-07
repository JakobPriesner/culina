<script lang="ts">
  import { resolve } from '$app/paths';

  import { m } from '$shell/i18n';
  import { metaLineFor } from '../recipeMeta';
  import { sourceLink } from '../sourceLink';
  import type { RecipeReading } from '../types';

  /**
   * What is said about a recipe under its title: the facts, the description,
   * which shelves it is on, where it came from, and — on paper only — what it
   * makes at the servings on screen.
   *
   * Lines in the header's own flow, not a box of their own: it renders them
   * and nothing around them.
   */
  interface Props {
    recipe: RecipeReading;
    /** The chrome recedes while cooking, and the lines that are not needed go. */
    cooking: boolean;
    /** The cookbooks it is already on. */
    cookbooks: readonly { readonly id: string; readonly name: string }[];
    /** What it makes at the servings on screen, for the printed page. */
    printedYield: string;
  }

  let { recipe, cooking, cookbooks, printedYield }: Props = $props();

  /**
   * The original, for the "from …" line, or no line at all.
   *
   * The host and not the whole address: "chefkoch.de" is the fact worth showing
   * and a 140-character URL with tracking parameters on the end is the same
   * fact, unreadable. An address that is not an ordinary web address — it came
   * from somewhere this app does not control — simply has no line.
   */
  const original = $derived(sourceLink(recipe.sourceUrl));
</script>

<p class="meta">{metaLineFor(recipe)}</p>

{#if recipe.description}
  <p class="description">{recipe.description}</p>
{/if}

<!-- Where this recipe lives, and only while reading: a cook standing at the
     hob does not need to be told which shelf it came off. -->
{#if cookbooks.length > 0 && !cooking}
  <p class="shelves">
    <span class="shelves-label">{m['cookbooks.recipe.inLabel']()}</span>
    {#each cookbooks as shelf, index (shelf.id)}
      <a href={resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: shelf.id })}>{shelf.name}</a
      >{#if index < cookbooks.length - 1}<span aria-hidden="true">, </span>{/if}
    {/each}
  </p>
{/if}

<!-- Where it started. Deliberately the quietest line on the page: this is
     an ordinary recipe now, and anything louder would make "imported" into
     a second kind of recipe. Reading only — at the hob, where it came from
     is the least useful fact on the screen. -->
{#if original && !cooking}
  <p class="origin">
    <!-- Off site, and the one link on this page that is: resolve() is for
         this app's own routes, and there is nothing here to resolve. -->
    <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
    <a href={original.href} rel="noreferrer nofollow" target="_blank">
      {m['import.origin.from']({ where: original.host })}
    </a>
  </p>
{/if}

<!-- Paper only. On screen the servings control says this, and says it
     better because it can be changed; on paper there is nothing to say it
     at all, and the amounts below have to be accounted for. -->
<p class="printed-yield">{printedYield}</p>

<style>
  .meta,
  .description {
    color: var(--text-muted);
    margin-top: var(--space-3);
    max-width: var(--measure);
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

  /* Screen has the servings control for this; paper has nothing. */
  .printed-yield {
    display: none;
  }

  @media (width < 52rem) {
    .meta,
    .description,
    .shelves {
      margin-top: var(--space-2);
    }

    /* A long shelf name is context, not the recipe itself. Keep it available
       in the link while preventing it from taking four lines before the first
       ingredient on a phone. */
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
    /* The meta line carries the yield the recipe was *written* for, which
       beside a scaled ingredient list is the one number on the page that is
       not true. Its tags and its timings are no loss either: what a paper
       recipe needs is what it makes and how to make it. */
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

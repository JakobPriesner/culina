<script lang="ts">
  import { resolve } from '$app/paths';

  import { Image } from '$ds';
  import RecipeCalories from '$features/nutrition/RecipeCalories.svelte';

  import { m } from '$shell/i18n';
  import { imageSrcset, imageUrl } from './recipeImage';
  import { matchLineFor, metaLineFor } from './recipeMeta';
  import Highlighted from './search/Highlighted.svelte';
  import { reasonLine } from './search/wording';
  import type { RecipeSummary } from './types';

  /**
   * One recipe: an editorial title and the few facts that decide tonight, not a bordered box. The whole card is the link.
   * A phone shows it as a row, thumbnail beside the words, so a screen holds several recipes rather than one photo,
   * unless the reader chose to browse by photo.
   */
  interface Props {
    recipe: RecipeSummary;
    pending?: boolean;
    /** What was searched for, so the words that found it can be seen. */
    query?: string;
    /** The household it comes from when inherited, said on the card so nobody opens it expecting to change it. */
    from?: string | null;
    /** The photo is on screen at first paint: fetched at once and ahead of the rest. */
    priority?: boolean;
    /** The full-width photo on a phone too, rather than a row. */
    photo?: boolean;
  }

  let {
    recipe,
    pending = false,
    query = '',
    from = null,
    priority = false,
    photo = false
  }: Props = $props();

  const meta = $derived(metaLineFor(recipe, { yields: false }));
  const match = $derived(matchLineFor(recipe));
  const eyebrow = $derived(recipe.tags[0] ?? null);
  /** Why a search found it, when its title does not say. */
  const reason = $derived(recipe.matchReason ? reasonLine(recipe.matchReason) : null);
</script>

<article class="recipe" class:pending class:row={!photo} aria-busy={pending || undefined}>
  <!-- Same box photographed or not (placeholder), so a mixed grid reads as one. -->
  <div class="photo">
    <Image
      src={recipe.imageId ? imageUrl(recipe.id, 400, recipe.imageId) : undefined}
      srcset={recipe.imageId ? imageSrcset(recipe.id, recipe.imageId) : undefined}
      sizes="(min-width: 64rem) 20rem, (min-width: 40rem) 45vw, {photo ? '90vw' : '6.5rem'}"
      alt=""
      ratio={4 / 3}
      loading={priority ? 'eager' : 'lazy'}
      fetchpriority={priority ? 'high' : undefined}
    />
  </div>

  <div class="text">
    {#if eyebrow}
      <p class="eyebrow">{eyebrow}</p>
    {/if}

    <h3 class="title">
      <a
        class="link"
        href={resolve('/(app)/recipes/[recipeId]', { recipeId: recipe.id })}
        aria-label={m['recipes.card.open']({ title: recipe.title })}
      >
        <Highlighted text={recipe.title} {query} />
      </a>
    </h3>

    <div class="details">
      <div class="facts">
        {#if meta}<p class="meta">{meta}</p>{/if}
        <RecipeCalories {recipe} explained={false} />
      </div>
      <span class="open" aria-hidden="true">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
          <path d="M5 12h14m-5-5 5 5-5 5" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </span>
    </div>

    {#if match}
      <p class="match" class:complete={recipe.match?.missing === 0}>{match}</p>
    {/if}

    {#if reason}
      <p class="reason"><Highlighted text={reason} {query} /></p>
    {/if}

    {#if from}
      <p class="from">{m['recipes.card.from']({ household: from })}</p>
    {/if}
  </div>
</article>

<style>
  .facts {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0 var(--space-3);
    color: var(--text-muted);
  }
  .recipe {
    position: relative;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
    height: 100%;
    padding-bottom: var(--space-4);
  }

  .pending {
    border-bottom: 2px dashed var(--border-strong);
  }
  .details {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
  }
  .open {
    display: grid;
    place-items: center;
    flex-shrink: 0;
    width: var(--space-8);
    height: var(--space-8);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    color: var(--text-muted);
  }
  .open svg {
    width: var(--space-4);
    height: var(--space-4);
  }
  .recipe:focus-within .open {
    background: var(--surface-accent-subtle);
    border-color: var(--accent);
    color: var(--accent);
  }

  @media (hover: hover) {
    .recipe:hover .open {
      background: var(--surface-accent-subtle);
      border-color: var(--accent);
      color: var(--accent);
    }
  }

  .photo {
    margin-bottom: var(--space-2);
  }

  .text {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  .from {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .eyebrow {
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
    line-height: 1.35;
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
  }

  .link {
    color: inherit;
    text-decoration: none;
  }

  .link::after {
    content: '';
    position: absolute;
    inset: 0;
  }

  .link:focus-visible::after {
    outline: 2px solid var(--border-focus);
    outline-offset: 4px;
    border-radius: var(--radius-lg);
  }

  @media (hover: hover) {
    .link:hover {
      text-decoration: underline;
      text-underline-offset: 0.2em;
    }
  }

  .meta {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .reason {
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-style: italic;
  }

  .match {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }

  .complete {
    color: var(--text-success);
  }

  /* Below the grid's two columns: a row, so the title stays whole beside a thumbnail. */
  @media (width < 40rem) {
    .row {
      flex-direction: row;
      align-items: flex-start;
      gap: var(--space-4);
      padding-bottom: 0;
    }

    .row .photo {
      flex: 0 0 6.5rem;
      margin-bottom: 0;
    }

    .row .text {
      flex: 1;
      gap: var(--space-1);
    }

    /* Beside a thumbnail a long compound has half the width: it hyphenates by the page's language, as on a plan card. */
    .row .title {
      font-size: var(--text-lg);
      hyphens: auto;
      overflow-wrap: break-word;
    }

    /* The whole row is the link; the arrow only takes the width the title needs. */
    .row .open {
      display: none;
    }
  }
</style>

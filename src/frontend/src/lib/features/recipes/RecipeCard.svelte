<script lang="ts">
  import { resolve } from '$app/paths';

  import { Image } from '$ds';

  import { m } from '$shell/i18n';
  import { imageSrcset, imageUrl } from './recipeImage';
  import { matchLineFor, metaLineFor } from './recipeMeta';
  import { reasonLine } from './search/wording';
  import type { RecipeSummary } from './types';

  /**
   * One recipe in a list.
   *
   * Not a box. A hairline, a title set in the editorial face, and the two or
   * three facts that decide whether you cook it tonight — because a page of
   * bordered rectangles is slower to read than the same content separated by
   * space and type, and when everything is a card nothing stands out.
   *
   * The whole row is the link, so the target is the recipe rather than the four
   * words of its title.
   */
  interface Props {
    recipe: RecipeSummary;
    /** Marked while a change to it is in flight. */
    pending?: boolean;
  }

  let { recipe, pending = false }: Props = $props();

  const meta = $derived(metaLineFor(recipe));
  const match = $derived(matchLineFor(recipe));
  const eyebrow = $derived(recipe.tags[0] ?? null);
  /**
   * Why a search found it, when its title does not say — the question somebody
   * asks silently about every result they did not expect.
   */
  const reason = $derived(recipe.matchReason ? reasonLine(recipe.matchReason) : null);
</script>

<article class="recipe" class:pending aria-busy={pending || undefined}>
  <!-- Every recipe leads with the same box, photographed or not: an unphotographed
       one gets the placeholder rather than a card of its own shape, so a grid of
       both reads as one grid. -->
  <div class="photo">
    <Image
      src={recipe.imageId ? imageUrl(recipe.id, 400, recipe.imageId) : undefined}
      srcset={recipe.imageId ? imageSrcset(recipe.id, recipe.imageId) : undefined}
      sizes="(min-width: 64rem) 20rem, (min-width: 40rem) 45vw, 90vw"
      alt=""
      ratio={4 / 3}
    />
  </div>

  {#if eyebrow}
    <p class="eyebrow">{eyebrow}</p>
  {/if}

  <h3 class="title">
    <a
      class="link"
      href={resolve('/(app)/recipes/[recipeId]', { recipeId: recipe.id })}
      aria-label={m['recipes.card.open']({ title: recipe.title })}
    >
      {recipe.title}
    </a>
  </h3>

  <div class="details">
    <p class="meta">{meta}</p>
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
    <p class="reason">{reason}</p>
  {/if}
</article>

<style>
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
  .recipe:hover .open,
  .recipe:focus-within .open {
    background: var(--surface-accent-subtle);
    border-color: var(--accent);
    color: var(--accent);
  }

  .photo {
    margin-bottom: var(--space-2);
  }

  .eyebrow {
    color: var(--text-muted);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
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

  /* The link covers the whole row, so the target is the recipe and not the
     four words of its title. */
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

  .link:hover {
    text-decoration: underline;
    text-underline-offset: 0.2em;
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
</style>

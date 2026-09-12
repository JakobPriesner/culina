<script lang="ts">
  import { resolve } from '$app/paths';

  import { Image } from '$ds';

  import { m } from '$shell/i18n';
  import { imageSrcset, imageUrl } from './recipeImage';
  import { matchLineFor, metaLineFor } from './recipeMeta';
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
    /** Dimmed while a change to it is in flight. */
    pending?: boolean;
  }

  let { recipe, pending = false }: Props = $props();

  const meta = $derived(metaLineFor(recipe));
  const match = $derived(matchLineFor(recipe));
  const eyebrow = $derived(recipe.tags[0] ?? null);
</script>

<article class="recipe" class:pending aria-busy={pending || undefined}>
  <!--
    A photo when there is one, and nothing at all when there is not — a grey
    placeholder box on every recipe somebody has not photographed is worse than
    the honest absence of one. The box reserves its space from the ratio, so
    nothing shifts as the picture arrives.
  -->
  {#if recipe.imageId}
    <div class="photo">
      <Image
        src={imageUrl(recipe.id, 400)}
        srcset={imageSrcset(recipe.id)}
        sizes="(min-width: 64rem) 20rem, (min-width: 40rem) 45vw, 90vw"
        alt=""
        ratio={4 / 3}
      />
    </div>
  {/if}

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

  <p class="meta">{meta}</p>

  {#if match}
    <p class="match" class:complete={recipe.match?.missing === 0}>{match}</p>
  {/if}
</article>

<style>
  .recipe {
    position: relative;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-block: var(--space-6);
    border-top: 1px solid var(--border);
  }

  /* Applied while a write is in flight. Readable, visibly not settled. */
  .pending {
    opacity: 0.6;
    transition: opacity var(--duration-base) var(--ease-out);
  }

  .photo {
    margin-bottom: var(--space-2);
  }

  .eyebrow {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
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

  .link:hover {
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }

  .meta {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .match {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }

  .complete {
    color: var(--text-success);
  }
</style>

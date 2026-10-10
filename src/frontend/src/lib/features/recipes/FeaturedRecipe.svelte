<script lang="ts">
  import { resolve } from '$app/paths';

  import { Image } from '$ds';

  import { m } from '$shell/i18n';
  import { imageSrcset, imageUrl } from './recipeImage';
  import { metaLineFor } from './recipeMeta';
  import type { RecipeSummary } from './types';

  interface Props {
    recipe: RecipeSummary;
    /** Why this one is here, when something can honestly say; falls back to the fixed eyebrow line. */
    reason?: string | null;
    /**
     * Stops this being suggested. The only negative signal ranking can't derive: a small household
     * has no meaningful non-click.
     */
    ondismiss?: () => void;
    /** True for the leader at first paint: its photograph is the largest image the app fetches, so the rest of the deck loads lazily. */
    priority?: boolean;
    /** The household it comes from, when it is inherited rather than this one's own. */
    from?: string | null;
  }

  let { recipe, reason = null, ondismiss, priority = false, from = null }: Props = $props();

  const headingId = $props.id();
</script>

<section class="feature" aria-labelledby={headingId}>
  <div class="copy">
    <p class="eyebrow">{reason ?? m['recipes.featured.eyebrow']()}</p>
    <h2 id={headingId}>{recipe.title}</h2>
    <p class="meta">{metaLineFor(recipe)}</p>
    {#if from}
      <p class="meta">{m['recipes.card.from']({ household: from })}</p>
    {/if}
    <div class="actions">
      <a class="feature-link" href={resolve('/(app)/recipes/[recipeId]', { recipeId: recipe.id })}>
        {m['recipes.featured.open']()}
        <span aria-hidden="true">→</span>
      </a>

      {#if ondismiss}
        <!-- Named for a screen reader: five buttons all reading "Dismiss" are indistinguishable. -->
        <button type="button" class="dismiss" onclick={ondismiss}>
          {m['suggestions.dismiss']({ title: recipe.title })}
        </button>
      {/if}
    </div>
  </div>

  <!-- Second link to the recipe for the photograph, kept out of the tab order and accessibility
       tree so it is met only once. -->
  <a
    class="photo"
    href={resolve('/(app)/recipes/[recipeId]', { recipeId: recipe.id })}
    tabindex="-1"
    aria-hidden="true"
  >
    <Image
      src={imageUrl(recipe.id, 1600, recipe.imageId)}
      srcset={imageSrcset(recipe.id, recipe.imageId)}
      sizes="(min-width: 80rem) 44rem, (min-width: 64rem) 55vw, 100vw"
      alt=""
      loading={priority ? 'eager' : 'lazy'}
      fetchpriority={priority ? 'high' : undefined}
      fill
      rounded={false}
    />
  </a>
</section>

<style>
  .feature {
    --border-focus: var(--text-on-feature);
    display: grid;
    grid-template-columns: minmax(18rem, 0.8fr) minmax(0, 1.2fr);
    min-height: 18rem;
    overflow: hidden;
    border-radius: var(--radius-lg);
    background: var(--surface-feature);
    color: var(--text-on-feature);
  }

  .copy {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    justify-content: center;
    gap: var(--space-3);
    padding: var(--space-6) var(--space-8);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2) var(--space-6);
  }

  .dismiss {
    padding: 0;
    border: 0;
    background: none;
    color: inherit;
    opacity: 0.75;
    font: inherit;
    font-size: var(--text-xs);
    text-align: start;
    cursor: pointer;
  }

  .dismiss:hover {
    opacity: 1;
    text-decoration: underline;
  }

  .eyebrow {
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.14em;
    text-transform: uppercase;
  }

  h2 {
    max-width: 16ch;
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.035em;
  }

  .meta {
    margin-bottom: var(--space-2);
    font-size: var(--text-sm);
  }

  .feature-link {
    display: inline-flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-8);
    min-height: var(--control-md);
    padding-block: var(--space-2);
    border-bottom: 1px solid currentcolor;
    color: var(--text-on-feature);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-decoration: none;
  }
  .feature-link:hover {
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }
  .feature-link span {
    font-size: var(--text-xl);
  }
  .photo {
    min-height: 18rem;
  }

  @media (max-width: 63.999rem) {
    .copy {
      padding: var(--space-6);
    }
  }

  /* A phone leads with its recipes: a shorter photo, tighter copy and a title that may use the full width. */
  @media (width < 48rem) {
    .feature {
      grid-template-columns: 1fr;
      min-height: 0;
    }

    .photo {
      grid-row: 1;
      min-height: 0;
      aspect-ratio: 5 / 2;
    }

    .copy {
      grid-row: 2;
      gap: var(--space-2);
      padding: var(--space-4);
    }

    h2 {
      max-width: none;
      font-size: var(--text-xl);
    }

    .meta {
      margin-bottom: 0;
    }

    .actions {
      column-gap: var(--space-4);
    }

    /* The dismissal is a text button; give it a finger-sized row as the open link has. */
    .dismiss {
      min-height: var(--control-md);
    }
  }

  /* A landscape phone has width to spare and no height: photo beside the copy instead of above it. */
  @media (width < 48rem) and (orientation: landscape) and (height < 30rem) {
    .feature {
      grid-template-columns: minmax(0, 3fr) minmax(0, 2fr);
    }

    .photo,
    .copy {
      grid-row: auto;
    }

    .photo {
      aspect-ratio: auto;
    }
  }

  /* Short screens at any width: the photo no longer holds the deck at 18rem. */
  @media (height < 30rem) {
    .feature,
    .photo {
      min-height: 0;
    }
  }
</style>

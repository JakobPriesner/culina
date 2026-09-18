<script lang="ts">
  import { resolve } from '$app/paths';

  import { Image } from '$ds';

  import { m } from '$shell/i18n';
  import { imageSrcset, imageUrl } from './recipeImage';
  import { metaLineFor } from './recipeMeta';
  import type { RecipeSummary } from './types';

  interface Props {
    recipe: RecipeSummary;
    /**
     * Why this one is here, when something can honestly say.
     *
     * The panel already had an eyebrow, filled with a fixed line because the
     * recipe underneath it was picked by an accident of photography. Now the
     * recipe is an answer, and the eyebrow is what makes it read as one —
     * contextual rather than promotional, with no section header claiming to
     * recommend anything.
     *
     * Falls back to the fixed line, so a suggestion nothing can explain looks
     * exactly like the panel always did.
     */
    reason?: string | null;
    /**
     * Stops this one being suggested, when it is a suggestion.
     *
     * Absent on the panel's old behaviour, where the recipe was picked by an
     * accident of photography and there was nothing to disagree with. It is the
     * only negative signal the ranking cannot derive from something another
     * feature already records — with a household this size there is no such
     * thing as a meaningful non-click, so "not this" has to be sayable.
     */
    ondismiss?: () => void;
    /**
     * Whether this is the panel already on screen at first paint.
     *
     * True for the leader and false for everything behind it in the deck. The
     * photograph here is the largest image the app ever asks for, and five of
     * them fetched eagerly to look at one is four downloads spent on a swipe
     * that most evenings never happens.
     */
    priority?: boolean;
  }

  let { recipe, reason = null, ondismiss, priority = false }: Props = $props();

  const headingId = $props.id();
</script>

<section class="feature" aria-labelledby={headingId}>
  <div class="copy">
    <p class="eyebrow">{reason ?? m['recipes.featured.eyebrow']()}</p>
    <h2 id={headingId}>{recipe.title}</h2>
    <p class="meta">{metaLineFor(recipe)}</p>
    <div class="actions">
      <a class="feature-link" href={resolve('/(app)/recipes/[recipeId]', { recipeId: recipe.id })}>
        {m['recipes.featured.open']()}
        <span aria-hidden="true">→</span>
      </a>

      {#if ondismiss}
        <!-- Quiet, and named for a screen reader: five buttons all reading
             "Dismiss" is five buttons nobody can tell apart. -->
        <button type="button" class="dismiss" onclick={ondismiss}>
          {m['suggestions.dismiss']({ title: recipe.title })}
        </button>
      {/if}
    </div>
  </div>

  <div class="photo">
    <Image
      src={imageUrl(recipe.id, 1600)}
      srcset={imageSrcset(recipe.id)}
      sizes="(min-width: 80rem) 44rem, (min-width: 64rem) 55vw, 100vw"
      alt=""
      loading={priority ? 'eager' : 'lazy'}
      fill
      rounded={false}
    />
  </div>
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

  @media (width < 48rem) {
    .feature {
      grid-template-columns: 1fr;
    }

    .photo {
      grid-row: 1;
      min-height: 0;
      aspect-ratio: 16 / 9;
    }

    .copy {
      grid-row: 2;
    }
  }
</style>

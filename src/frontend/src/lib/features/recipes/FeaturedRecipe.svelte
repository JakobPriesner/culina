<script lang="ts">
  import { resolve } from '$app/paths';

  import { Image } from '$ds';

  import { m } from '$shell/i18n';
  import { imageSrcset, imageUrl } from './recipeImage';
  import { metaLineFor } from './recipeMeta';
  import type { RecipeSummary } from './types';

  interface Props {
    recipe: RecipeSummary;
  }

  let { recipe }: Props = $props();

  const headingId = $props.id();
</script>

<section class="feature" aria-labelledby={headingId}>
  <div class="copy">
    <p class="eyebrow">{m['recipes.featured.eyebrow']()}</p>
    <h2 id={headingId}>{recipe.title}</h2>
    <p class="meta">{metaLineFor(recipe)}</p>
    <a class="feature-link" href={resolve('/(app)/recipes/[recipeId]', { recipeId: recipe.id })}>
      {m['recipes.featured.open']()}
      <span aria-hidden="true">→</span>
    </a>
  </div>

  <div class="photo">
    <Image
      src={imageUrl(recipe.id, 1600)}
      srcset={imageSrcset(recipe.id)}
      sizes="(min-width: 80rem) 44rem, (min-width: 64rem) 55vw, 100vw"
      alt=""
      loading="eager"
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
    margin-bottom: var(--space-8);
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

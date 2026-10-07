<script lang="ts">
  import { Image } from '$ds';
  import { m } from '$shell/i18n';

  import type { PreviewRecipe } from './recipes';

  /** The previewed recipe's title and photograph, which recede while cooking. */
  interface Props {
    recipe: PreviewRecipe;
    cooking: boolean;
  }

  let { recipe, cooking }: Props = $props();
</script>

<header class="recipe-header" class:cooking>
  <div class="intro">
    <p class="eyebrow">{recipe.tag} · {m['preview.minutes']({ count: recipe.minutes })}</p>
    <h1 tabindex="-1">{recipe.title}</h1>
    {#if !cooking}<p class="description">{recipe.description}</p>{/if}
  </div>
  {#if !cooking && recipe.image}
    <div class="recipe-photo"><Image src={recipe.image} alt="" fill loading="eager" /></div>
  {/if}
</header>

<style>
  .recipe-header {
    display: grid;
    grid-template-columns: minmax(0, 1.5fr) minmax(0, 1fr);
    align-items: center;
    gap: var(--layout-section-gap);
    margin-bottom: var(--space-12);
  }

  .recipe-photo {
    height: clamp(12rem, 24vw, 20rem);
  }

  .eyebrow {
    font-size: var(--text-sm);
    color: var(--text-muted);
    margin-bottom: var(--space-4);
  }

  h1 {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
    line-height: var(--leading-tight);
  }

  .description {
    margin-top: var(--space-4);
    color: var(--text-muted);
    max-width: var(--measure);
  }

  .cooking {
    display: block;
    margin-bottom: var(--space-8);
    padding-bottom: var(--space-6);
    border-bottom: 1px solid var(--border);
  }

  .cooking .eyebrow {
    margin-bottom: var(--space-2);
  }

  .cooking h1 {
    font-size: var(--text-3xl);
  }

  @media (width < 64rem) {
    .recipe-header {
      grid-template-columns: 1fr;
      gap: var(--space-6);
      margin-bottom: var(--space-8);
    }

    .recipe-photo {
      height: auto;
      aspect-ratio: 16 / 9;
    }

    .cooking {
      margin-bottom: var(--space-6);
    }

    .cooking h1 {
      font-size: var(--text-2xl);
    }
  }
</style>

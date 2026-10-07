<script lang="ts">
  import { Button, Image } from '$ds';
  import { m } from '$shell/i18n';

  import type { PreviewRecipe } from './recipes';

  /** The recipe the preview's library leads with, while nothing narrows it. */
  interface Props {
    recipe: PreviewRecipe;
    onopen: () => void;
  }

  let { recipe, onopen }: Props = $props();
</script>

<section class="feature" aria-labelledby="featured-title">
  <div class="feature-copy">
    <p class="eyebrow">{m['preview.featured']()}</p>
    <h2 id="featured-title">{recipe.title}</h2>
    <p>{recipe.description}</p>
    <span class="meta"
      >{m['preview.minutes']({ count: recipe.minutes })} · {m['preview.twoServings']()}</span
    >
    <div>
      <Button variant="secondary" onclick={onopen}
        >{m['preview.open']()} <span aria-hidden="true">→</span></Button
      >
    </div>
  </div>
  <div class="photo">
    <Image src={recipe.image!} alt={recipe.title} loading="eager" fill rounded={false} />
  </div>
</section>

<style>
  .feature {
    --border-focus: var(--text-on-feature);
    display: grid;
    grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr);
    background: var(--surface-feature);
    color: var(--text-on-feature);
    border-radius: var(--radius-lg);
    overflow: hidden;
  }

  .photo {
    min-height: 22rem;
  }

  .feature-copy {
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: var(--space-4);
    padding: var(--space-8) var(--space-12);
    align-items: flex-start;
  }

  .feature-copy h2 {
    font-family: var(--font-editorial);
    font-weight: var(--weight-regular);
    font-size: clamp(1.75rem, 2.8vw, 2.5rem);
    line-height: 1.18;
    letter-spacing: -0.035em;
  }

  .feature-copy > p:not(.eyebrow) {
    color: var(--text-on-feature);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
    max-width: 38ch;
  }

  .eyebrow {
    font-size: var(--text-xs);
    letter-spacing: 0.12em;
    text-transform: uppercase;
    color: var(--text-on-feature);
  }

  .meta {
    font-size: var(--text-sm);
    color: var(--text-on-feature);
    padding-block: var(--space-1);
  }

  @media (max-width: 63.999rem) {
    .feature-copy {
      padding: var(--space-6);
    }
  }

  @media (width < 64rem) {
    .feature {
      grid-template-columns: 1fr;
    }

    .photo {
      grid-row: 1;
      min-height: 0;
      aspect-ratio: 16 / 10;
    }
  }
</style>

<script lang="ts">
  import { IconButton, Image } from '$ds';
  import { m } from '$shell/i18n';
  import type { PreviewRecipe } from './recipes';
  interface Props {
    recipe: PreviewRecipe;
    favourite: boolean;
    onopen: () => void;
    onfavourite: () => void;
  }
  let { recipe, favourite, onopen, onfavourite }: Props = $props();
</script>

<article class="recipe">
  <!-- Always the same box: a recipe without a photo gets the placeholder rather
       than a card of its own shape. -->
  <Image src={recipe.image} alt="" ratio={4 / 3} />
  <div class="copy">
    <span class="tag">{recipe.tag}</span>
    <h3><button class="open" onclick={onopen}>{recipe.title}</button></h3>
    <p class="description">{recipe.description}</p>
    <p class="meta">
      {m['preview.minutes']({ count: recipe.minutes })}<span aria-hidden="true"> ↗</span>
    </p>
  </div>
  <div class="favourite">
    <IconButton
      label={m['preview.favourite']({ title: recipe.title })}
      pressed={favourite}
      onclick={onfavourite}
    >
      <svg
        viewBox="0 0 24 24"
        fill={favourite ? 'currentColor' : 'none'}
        stroke="currentColor"
        stroke-width="1.5"
        ><path
          d="M12 20S3 15 3 8.8C3 3.7 9.3 2 12 6.7 14.7 2 21 3.7 21 8.8 21 15 12 20 12 20Z"
        /></svg
      >
    </IconButton>
  </div>
</article>

<style>
  .recipe {
    position: relative;
    display: flex;
    flex-direction: column;
    min-width: 0;
  }
  .copy {
    padding-block: var(--space-4);
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }
  .open {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    width: 100%;
    gap: var(--space-2);
    padding: 0;
    min-height: var(--control-sm);
    text-align: start;
    border: 0;
    background: transparent;
    color: var(--text);
    font: inherit;
    cursor: pointer;
  }
  .open:hover {
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }
  .open::after {
    content: '';
    position: absolute;
    inset: 0;
    border-radius: var(--radius-lg);
  }
  .open:focus-visible {
    outline: none;
  }
  .open:focus-visible::after {
    outline: 2px solid var(--border-focus);
    outline-offset: 4px;
  }
  .tag {
    color: var(--text-muted);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }
  h3 {
    font-family: var(--font-editorial);
    font-size: var(--text-xl);
    line-height: 1.35;
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
  }
  .description {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }
  .meta {
    font-size: var(--text-sm);
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-top: var(--space-3);
    border-top: 1px solid var(--border);
    margin-top: auto;
    color: var(--text-muted);
  }
  .favourite {
    z-index: 1;
    position: absolute;
    top: var(--space-3);
    right: var(--space-3);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    box-shadow: var(--shadow-card);
  }
</style>

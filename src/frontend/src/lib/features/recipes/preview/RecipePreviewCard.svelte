<script lang="ts">
  import { IconButton } from '$ds';
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
  <span class="tag">{recipe.tag}</span>
  <h3><button class="open" onclick={onopen}>{recipe.title}</button></h3>
  <p class="description">{recipe.description}</p>
  <p class="meta">
    {m['preview.minutes']({ count: recipe.minutes })}<span aria-hidden="true"> ↗</span>
  </p>
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
    border-top: 1px solid var(--border);
    padding: var(--space-6) var(--space-12) var(--space-6) 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }
  .open {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    width: 100%;
    gap: var(--space-3);
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
  }
  .tag {
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
  h3 {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
  }
  .description {
    font-size: var(--text-sm);
    color: var(--text-muted);
  }
  .meta {
    font-size: var(--text-sm);
    margin-top: var(--space-2);
  }
  .favourite {
    z-index: 1;
    position: absolute;
    top: var(--space-3);
    right: 0;
  }
</style>

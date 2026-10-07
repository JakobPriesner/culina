<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  import type { PreviewRecipe } from './recipes';

  /** The line that offers to carry on with a recipe that is part-way cooked. */
  interface Props {
    recipe: PreviewRecipe;
    onresume: () => void;
  }

  let { recipe, onresume }: Props = $props();
</script>

<div class="resume">
  <div>
    <span>{m['preview.inProgress']()}</span>
    <p>{recipe.title}</p>
  </div>
  <Button size="sm" onclick={onresume}
    >{m['preview.resume']()} <span aria-hidden="true">→</span></Button
  >
</div>

<style>
  .resume {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    margin-top: var(--space-6);
    padding-block: var(--space-4);
    border-block: 1px solid var(--border);
  }

  .resume span {
    font-size: var(--text-xs);
    color: var(--text-muted);
  }

  .resume p {
    margin-top: var(--space-1);
    font-weight: var(--weight-medium);
  }

  @media (width < 64rem) {
    .resume {
      align-items: flex-start;
      flex-direction: column;
    }
  }
</style>

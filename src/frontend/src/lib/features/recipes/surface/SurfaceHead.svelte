<script lang="ts">
  import RecipeActions from './RecipeActions.svelte';
  import RecipeHeadMeta from './RecipeHeadMeta.svelte';
  import type { RecipeReading } from '../types';

  interface Props {
    recipe: RecipeReading;
    cooking: boolean;
    editable: boolean;
    cookbooks: readonly { readonly id: string; readonly name: string }[];
    /** Yield at the servings on screen, for print. */
    printedYield: string;
    onaddtolist?: () => void;
    onaddtocookbook?: () => void;
    onaddtoplan?: () => void;
    onshare?: () => void;
    oncopy?: () => void;
    ondelete?: () => void;
  }

  let {
    recipe,
    cooking,
    editable,
    cookbooks,
    printedYield,
    onaddtolist,
    onaddtocookbook,
    onaddtoplan,
    onshare,
    oncopy,
    ondelete
  }: Props = $props();
</script>

<header class="head" class:cooking>
  <div class="titleRow">
    <h1 class="title">{recipe.title}</h1>

    <RecipeActions
      recipeId={recipe.id}
      {cooking}
      {editable}
      {onaddtolist}
      {onaddtocookbook}
      {onaddtoplan}
      {onshare}
      {oncopy}
      {ondelete}
    />
  </div>

  <RecipeHeadMeta {recipe} {cooking} {cookbooks} {printedYield} />
</header>

<style>
  /* A grid, not a wrapping row, so a long title wraps in its own column instead of pushing the controls down. */
  .titleRow {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-4);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  /* Recede while cooking via size and `--text-muted`, not opacity, which escapes the contrast tests. */
  .cooking {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .cooking .title {
    font-size: var(--text-xl);
  }

  @media (width < 52rem) {
    .titleRow {
      align-items: start;
      gap: var(--space-2);
    }

    .title {
      font-size: var(--text-3xl);
      line-height: var(--leading-tight);
    }
  }

  @media print {
    .head {
      margin-bottom: 6mm;
    }

    .title {
      font-size: 20pt;
    }
  }
</style>

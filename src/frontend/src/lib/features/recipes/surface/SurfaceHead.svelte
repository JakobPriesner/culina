<script lang="ts">
  import { visibleBelow } from '$shell/whenVisible';
  import RecipeActions from './RecipeActions.svelte';
  import RecipeHeadMeta from './RecipeHeadMeta.svelte';
  import type { NutritionLink } from './nutritionLink';
  import type { RecipeReading } from '../types';

  interface Props {
    recipe: RecipeReading;
    cooking: boolean;
    editable: boolean;
    cookbooks: readonly { readonly id: string; readonly name: string }[];
    /** Yield at the servings on screen, for print. */
    printedYield: string;
    onaddtolist?: () => void;
    listed?: boolean;
    cooked?: { readonly count: number; readonly lastMadeAt: string | null } | null;
    onaddtocookbook?: () => void;
    onaddtoplan?: () => void;
    onshare?: () => void;
    oncopy?: () => void;
    ondelete?: () => void;
    /** Whether the title is still on screen, for a page that repeats it in a sticky bar. */
    ontitleview?: (visible: boolean) => void;
    nutrition?: NutritionLink | null;
  }

  let {
    recipe,
    cooking,
    editable,
    cookbooks,
    printedYield,
    onaddtolist,
    listed = false,
    cooked = null,
    onaddtocookbook,
    onaddtoplan,
    onshare,
    oncopy,
    ondelete,
    ontitleview,
    nutrition = null
  }: Props = $props();

  /** The page's sticky bar is about this tall (px); the title counts as gone once it slides under it. */
  const stickyBarHeight = 56;
</script>

<header class="head" class:cooking>
  <div class="titleRow">
    <h1 class="title" {@attach ontitleview && visibleBelow(stickyBarHeight, ontitleview)}>
      {recipe.title}
    </h1>

    <RecipeActions
      recipeId={recipe.id}
      {cooking}
      {editable}
      {onaddtolist}
      {listed}
      {onaddtocookbook}
      {onaddtoplan}
      {onshare}
      {oncopy}
      {ondelete}
    />
  </div>

  <RecipeHeadMeta {recipe} {cooking} {cookbooks} {printedYield} {nutrition} {cooked} />
</header>

<style>
  /* The title takes the width its text needs: a title that does not fit beside the actions takes the whole line and they wrap under it, instead of leaving it a skinny column that splits words. */
  .titleRow {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3) var(--space-4);
  }

  .title {
    flex: 1 1 auto;
    min-width: 0;
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

  /* On a phone the title takes the whole line and the actions sit under it as a toolbar, never over the photograph. */
  @media (width < 52rem) {
    .titleRow {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: var(--space-3);
    }

    .title {
      font-size: var(--text-3xl);
      line-height: var(--leading-tight);
    }
  }

  /* The photograph stands beside the title on a short screen, so the title has a column, not the width. */
  @media screen and (max-height: 32rem) {
    .title {
      font-size: var(--text-2xl);
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

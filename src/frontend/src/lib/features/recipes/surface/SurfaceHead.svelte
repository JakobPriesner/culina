<script lang="ts">
  import RecipeActions from './RecipeActions.svelte';
  import RecipeHeadMeta from './RecipeHeadMeta.svelte';
  import type { RecipeReading } from '../types';

  /** The title, the controls beside it, and what is said about the recipe underneath. */
  interface Props {
    recipe: RecipeReading;
    cooking: boolean;
    editable: boolean;
    cookbooks: readonly { readonly id: string; readonly name: string }[];
    /** What it makes at the servings on screen, for the printed page. */
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
  /*
   * Two columns, not a wrapping row.
   *
   * A row let a long title push the controls onto a line of their own at the
   * *start* of it, where they sat directly on top of the meta line — so the
   * page's furniture changed places depending on how long somebody's recipe
   * was called. Here the title takes the room it needs and wraps inside its own
   * column, and the group stays at the end of the row it belongs to.
   */
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

  /* The chrome recedes when cooking; it does not disappear, because knowing
     which recipe you are in is not optional.

     Smaller and quieter, not faded. Opacity on text is how contrast breaks
     without anybody noticing: it blends toward the background by an amount no
     palette review can see, and the theme's own contrast test cannot reach it.
     `--text-muted` is a colour the contract already proves readable in both
     modes. */
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

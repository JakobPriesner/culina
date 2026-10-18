<script lang="ts">
  import { SectionLabel, SegmentedControl } from '$ds';

  import { m } from '$shell/i18n';
  import type { createIngredientPanel } from './ingredientPanel.svelte';
  import IngredientList from './IngredientList.svelte';
  import type { Scaling } from './scaled.svelte';
  import SectionHead from './SectionHead.svelte';

  interface Props {
    panel: ReturnType<typeof createIngredientPanel>;
    cooking: boolean;
    scaling: Scaling;
    highlighted: string | null;
    onhighlight: (ingredientId: string | null) => void;
    /** Whether a name is still to buy on the shopping list; see `IngredientRow`. */
    onTheList?: (name: string) => boolean;
  }

  let { panel, cooking, scaling, highlighted, onhighlight, onTheList }: Props = $props();

  const perStep = $derived(panel.perStep);
</script>

<section class="ingredients" class:perStep aria-label={m['recipe.ingredients']()}>
  <SectionHead title={m['recipe.ingredients']()}>
    <!-- Nothing to choose in a recipe with one arrangement. Cooking chooses between this step and all, to prep ahead. -->
    {#if cooking && panel.divisible && panel.written.length > 0}
      <SegmentedControl
        label={m['recipe.ingredientsView.label']()}
        selected={panel.cookingView}
        segments={[
          { id: 'step', label: m['recipe.ingredientsView.thisStep']() },
          { id: 'all', label: m['recipe.ingredientsView.all']() }
        ]}
        onselect={panel.chooseWhileCooking}
      />
    {:else if !cooking && panel.divisible && panel.written.length > 0}
      <SegmentedControl
        label={m['recipe.ingredientsView.label']()}
        selected={panel.view}
        segments={[
          { id: 'combined', label: m['recipe.ingredientsView.combined']() },
          { id: 'perStep', label: m['recipe.ingredientsView.perStep']() }
        ]}
        onselect={panel.choose}
      />
    {/if}
  </SectionHead>

  {#if panel.panel.length > 0}
    <!-- Beside the steps this is only the ingredients no step named, so say so. -->
    {#if perStep}
      <SectionLabel spacing="tight">{m['recipe.notInAnyStep']()}</SectionLabel>
    {/if}

    <IngredientList
      ingredients={panel.panel}
      {scaling}
      {highlighted}
      onhover={onhighlight}
      {onTheList}
    />
  {:else if !perStep}
    <!-- While cooking the list is filtered to this step, so "none written" would be wrong. Beside the steps, all ingredients accounted for says nothing. -->
    <p class="empty">
      {cooking && panel.written.length > 0
        ? m['recipe.noneThisStep']()
        : m['recipe.noIngredients']()}
    </p>
  {:else if panel.written.length === 0}
    <p class="empty">{m['recipe.noIngredients']()}</p>
  {/if}
</section>

<style>
  /*
   * Only the list gets a surface, and it stays pinned while the steps scroll.
   * Needs `align-items: start` on the surface's body grid, or it has no room to travel.
   * The offset clears the floating header. No max-height: an inner overlay scrollbar
   * hides, so a clipped card reads as broken.
   */
  .ingredients {
    position: sticky;
    top: var(--space-24);
    padding: var(--card-padding);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
  }

  /* Beside the steps there is nothing to follow, so it stays put; see `StepList`. */
  .perStep {
    grid-column: 1;
    grid-row: 1;
    position: static;
  }

  .empty {
    color: var(--text-muted);
  }

  /* One column: a pinned list above the steps would cover them. */
  @media screen and (width < 64rem) {
    .ingredients {
      position: static;
    }
  }

  /* Ink, not grey blocks. */
  @media print {
    .ingredients {
      padding: 0;
      background: none;
      position: static;
    }
  }
</style>

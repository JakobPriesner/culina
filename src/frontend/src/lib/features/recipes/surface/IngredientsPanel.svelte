<script lang="ts">
  import { SegmentedControl } from '$ds';

  import { m } from '$shell/i18n';
  import type { createIngredientPanel } from './ingredientPanel.svelte';
  import IngredientList from './IngredientList.svelte';
  import type { Scaling } from './scaled.svelte';
  import SectionHead from './SectionHead.svelte';

  /**
   * The ingredient region: its heading, the switch between its arrangements,
   * and the list.
   *
   * Same position in both weightings; contracts to the current step's
   * ingredients when cooking.
   */
  interface Props {
    panel: ReturnType<typeof createIngredientPanel>;
    cooking: boolean;
    scaling: Scaling;
    /** Which ingredient the reader is pointing at, from a step's text. */
    highlighted: string | null;
    onhighlight: (ingredientId: string | null) => void;
  }

  let { panel, cooking, scaling, highlighted, onhighlight }: Props = $props();

  const perStep = $derived(panel.perStep);
</script>

<section class="ingredients" class:perStep aria-label={m['recipe.ingredients']()}>
  <!-- The switch sits on the heading's own line, and stays put when the
       arrangement changes: the head keeps the width of the ingredient
       column even where the section has grown past it. -->
  <SectionHead title={m['recipe.ingredients']()}>
    <!-- Nothing to choose while cooking, where one step is the whole
         arrangement, nor in a recipe that only has the one. -->
    {#if !cooking && panel.divisible && panel.written.length > 0}
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
    <!-- Said only where it needs saying. In the combined list this is the
         list; beside the steps it is the handful the steps never named, and
         a reader who is not told that will wonder what happened to the
         rest. -->
    {#if perStep}
      <p class="leftovers">{m['recipe.notInAnyStep']()}</p>
    {/if}

    <IngredientList ingredients={panel.panel} {scaling} {highlighted} onhover={onhighlight} />
  {:else if !perStep}
    <!-- Two different emptinesses. While cooking the list is filtered to
         what this step needs, so "none written down" would be a lie about a
         recipe that has seven. Beside the steps there is a third: every
         ingredient is accounted for, and the right thing to say is
         nothing. -->
    <p class="empty">
      {cooking ? m['recipe.noneThisStep']() : m['recipe.noIngredients']()}
    </p>
  {:else if panel.written.length === 0}
    <p class="empty">{m['recipe.noIngredients']()}</p>
  {/if}
</section>

<style>
  /*
   * Only the list gets a surface.
   *
   * It is the thing you keep glancing back at and have to find again
   * mid-sentence, and the panel is what makes it findable — a shape the eye
   * returns to rather than a column it has to re-locate. The method is prose:
   * read straight down, once, and the longest thing on the page. Drawing a
   * panel around that boxes in something nobody was going to lose, and doubles
   * the page's furniture to say it.
   *
   * The rule holds in both arrangements, which is what makes the surface mean
   * something: wherever it is, it is what you need out. Beside the steps it
   * travels with them — see `.perStep .step-needs` in `StepList`.
   *
   * The list stays on screen while the method scrolls past it.
   *
   * A recipe's steps are long and its ingredients are the thing you keep
   * glancing back at — "how much of the wine goes in here" is asked at step
   * four, where the list left the screen at step one. Sticking it is what
   * makes the two columns behave like a spread in a cookbook rather than two
   * documents that happen to be side by side.
   *
   * `align-items: start` on the surface's body grid is what makes this work at
   * all: a stretched grid item fills its row and has nowhere to travel. If that
   * ever comes off, this silently stops sticking.
   *
   * The offset is the one the steps already scroll to — far enough down that
   * the app's floating header is not sitting on top of it.
   *
   * No height limit, deliberately. Capping the panel to the viewport and
   * letting it scroll inside itself reads as a bug long before it helps: an
   * overlay scrollbar is invisible until it is touched, so a fifteen-line list
   * on an ordinary laptop is just a card with its end cut off — and that is
   * the common recipe, not the rare one. A list taller than the screen keeps
   * its top pinned instead, which is the half you glance back at, and shows
   * its end once the steps beside it run out. Whole and occasionally clipped
   * beats tidy and apparently broken.
   */
  .ingredients {
    position: sticky;
    top: var(--space-24);
    padding: var(--card-padding);
    background: var(--surface-sunken);
    border-radius: var(--radius-lg);
  }

  /* Beside the steps the panel keeps its column and its card, and has nothing
     to follow: what is left in it is the heading and whatever no step asked
     for, and the rest is already level with the step that needs it. The steps
     arrange themselves around it; see `StepList`. */
  .perStep {
    grid-column: 1;
    grid-row: 1;
    position: static;
  }

  .leftovers {
    margin-bottom: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .empty {
    color: var(--text-muted);
  }

  /* One column: the list is above the steps rather than beside them, and
     something stuck to the top of the screen there is not a companion, it is
     a lid. */
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

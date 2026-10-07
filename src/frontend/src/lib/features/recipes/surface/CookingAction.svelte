<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import { dockCookingAction } from './dockCookingAction';

  /** The one action parked at the bottom of the recipe: start cooking, or stop. */
  interface Props {
    cooking: boolean;
    /** False for a recipe with no steps, which would open cook mode on "Step 1 of 0". */
    canCook: boolean;
    onstartcooking?: () => void;
    onstopcooking?: () => void;
  }

  let { cooking, canCook, onstartcooking, onstopcooking }: Props = $props();
</script>

{#snippet action()}
  <footer class="foot">
    {#if cooking}
      <Button size="lg" onclick={onstopcooking}>{m['recipe.stopCooking']()}</Button>
    {:else if canCook}
      <!-- At the end of the reading, where the decision is made; floats within reach until its own
           place enters the viewport. -->
      <Button variant="primary" size="lg" onclick={onstartcooking}>
        {m['recipe.startCooking']()}
      </Button>
    {/if}
  </footer>
{/snippet}

{#if cooking || canCook}
  {#if cooking}
    {@render action()}
  {:else}
    <div class="cook-action" use:dockCookingAction>
      {@render action()}
    </div>
  {/if}
{/if}

<style>
  /*
   * In the document at the end of the recipe; cooking keeps it here because step navigation owns
   * the bottom edge.
   */
  .foot {
    max-width: 100%;
    margin-block-end: var(--space-8);
    display: flex;
    justify-content: center;
    align-self: center;
  }

  /*
   * A strip of its own rather than a bare floating button, which cut a hole in a line of the method.
   * Not at the inline end: that column is the ingredient list. Sticks above the shell's bottom inset.
   */
  @media screen {
    .cook-action {
      position: sticky;
      bottom: max(var(--bottom-inset), env(safe-area-inset-bottom, 0px));
      z-index: var(--z-sticky);
      align-self: stretch;
      margin-block-end: var(--space-8);
    }

    .cook-action .foot {
      position: relative;
      margin-block-end: 0;
      padding-block: var(--space-8) var(--space-6);
    }

    /*
     * Gradient, not blur: what passes under is a flat column. Solid behind the button, faded above.
     */
    .cook-action .foot::before {
      content: '';
      position: absolute;
      inset: 0;
      z-index: -1;
      background: linear-gradient(to top, var(--surface) 60%, transparent);
      pointer-events: none;
    }
  }

  /* `screen and`: A4 paper is narrower than this but wants the two columns. */
  @media screen and (width < 64rem) {
    .cook-action {
      --action-height: 0px;
      --action-top: auto;
      --action-left: auto;
      --action-width: auto;
      position: static;
      min-height: var(--action-height, 0px);
    }

    .cook-action .foot {
      position: relative;
      margin-block-end: 0;
    }

    .cook-action .foot:global(.docked) {
      position: fixed;
      top: var(--action-top);
      bottom: auto;
      left: var(--action-left);
      width: var(--action-width);
      z-index: var(--z-sticky);
    }
  }

  /* On a phone the lone control gets the whole line, since a thumb at the bottom does not aim. */
  @media (width < 52rem) {
    .foot {
      align-self: stretch;
      width: 100%;
    }

    :global(.surface:not(.cooking)) .foot {
      padding-block: var(--space-4) var(--space-3);
    }

    .foot :global(.button) {
      flex: 1;
    }
  }

  @media print {
    .foot,
    .cook-action {
      display: none !important;
    }
  }
</style>

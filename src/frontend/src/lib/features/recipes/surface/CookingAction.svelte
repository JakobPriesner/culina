<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import { dockCookingAction } from './dockCookingAction';

  /**
   * The one thing parked at the bottom of the recipe: start cooking, or stop.
   *
   * One action, not four: the bar carries the thing the page exists to offer
   * and nothing else. A visitor following a share link cannot cook here —
   * cooking keeps a session, and they have none — and a bar floating over the
   * last step with nothing on it is worse than no bar.
   */
  interface Props {
    cooking: boolean;
    /**
     * Whether there is anything to cook.
     *
     * A recipe with no steps offered the button like any other, and cook mode
     * then opened on "Step 1 of 0" with nothing under it. So the page says so
     * where the steps would be, rather than letting somebody walk into a screen
     * that looks broken.
     */
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
      <!--
        One button, alone, after the recipe. It stays within reach while the
        recipe is read, then settles here when its place enters the viewport.

        It is here rather than beside the title because the decision is made
        at the end of the reading, not at the start of it: you look at the
        photograph, you read down the ingredients, you work out whether you
        have the cream — and by then the title is three screens up. Everything
        that is not that decision went to the title row, so what floats over
        the recipe is one accented control instead of a strip of four.
      -->
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
   * At the end of the recipe, in the document. Cooking keeps it here because
   * its step navigation already owns the page's bottom edge.
   */
  .foot {
    max-width: 100%;
    /* Below its resting place, once the page ends. */
    margin-block-end: var(--space-8);
    display: flex;
    justify-content: center;
    align-self: center;
  }

  /*
   * Within reach while the recipe is read — and standing on
   * a strip of its own rather than on the recipe.
   *
   * A button floating over the method sat in the middle of a line of it:
   * "Eigelb zugeben" to its left, "bis der Zucker" to its right, and the words
   * between them gone. That is a hole in the reading. A strip turns it into an
   * edge, the same one the app's header draws at the top — the page fading out
   * under the control instead of being cut through by it — so what passes
   * behind reads as below the fold, not as covered. The steps already scroll
   * themselves clear of it (`--controls-inset`).
   *
   * Not at the inline end, where the reading column is not: in the combined
   * view that is the pinned ingredient list, and in the per-step view each
   * step's own ingredients, so nothing in that column is free to stand on.
   *
   * The shell's bottom inset keeps it above compact-screen navigation. Bottom
   * stickiness brings it into view before its normal position, then lets it
   * scroll with the document once the reader reaches the end of the recipe.
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

    /* A gradient and not a blur, for the header's reason: what passes under it
       is a centred column on a flat background. Solid behind the button, so
       no half-line shows around it; faded above, so the edge is soft. */
    .cook-action .foot::before {
      content: '';
      position: absolute;
      inset: 0;
      z-index: -1;
      background: linear-gradient(to top, var(--surface) 60%, transparent);
      pointer-events: none;
    }
  }

  /*
   * `screen and`, because a sheet of A4 is narrower than this and is not a
   * phone: paper wants the two columns, and had to spend the whole print block
   * undoing these rules to get them back.
   */
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

  /*
   * On a phone it is the width of the page.
   *
   * A thumb reaching the bottom of a propped-up phone does not aim, so the one
   * control down there is given the whole line to land on.
   */
  @media (width < 52rem) {
    .foot {
      align-self: stretch;
      width: 100%;
    }

    /* The surface it is in says whether the recipe is being read. */
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

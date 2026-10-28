<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import { dockCookingAction } from './dockCookingAction';

  /** The one action parked at the bottom of the recipe: start cooking or go back to it. Stopping lives in the cook screen's kitchen sheet. */
  interface Props {
    cooking: boolean;
    /** False for a recipe with no steps, which would open cook mode on "Step 1 of 0". */
    canCook: boolean;
    /** A session for this recipe is already open: the way in is back to it, not a new start. */
    resuming?: boolean;
    onstartcooking?: () => void;
    /** Cooked without cook mode (from the page, from memory): one tap records it. */
    onmadeit?: () => void;
  }

  let { cooking, canCook, resuming = false, onstartcooking, onmadeit }: Props = $props();
</script>

{#snippet action()}
  <footer class="foot">
    {#if canCook}
      <!-- At the end of the reading, where the decision is made; floats until its own place is in view. -->
      <Button variant="primary" size="lg" onclick={onstartcooking}>
        {resuming ? m['cooking.resume']() : m['recipe.startCooking']()}
      </Button>
      {#if onmadeit}
        <!-- Quieter than starting: on a phone just the check, named for a screen reader. -->
        <Button size="lg" label={m['cooking.madeIt']()} onclick={onmadeit}>
          {#snippet icon()}
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <circle cx="12" cy="12" r="9" />
              <path d="m8.5 12.5 2.5 2.5 4.5-5" />
            </svg>
          {/snippet}

          {m['cooking.madeIt']()}
        </Button>
      {/if}
    {/if}
  </footer>
{/snippet}

<!-- While cooking the step controls own the bottom edge, and stopping is in their kitchen sheet. -->
{#if !cooking && canCook}
  <div class="cook-action" use:dockCookingAction>
    {@render action()}
  </div>
{/if}

<style>
  /* In the document at the recipe's end; step navigation owns the bottom edge while cooking. */
  .foot {
    max-width: 100%;
    margin-block-end: var(--space-8);
    display: flex;
    justify-content: center;
    gap: var(--space-3);
    align-self: center;
  }

  /* A strip of its own (a bare floating button cut a hole in the method), not at the inline end where the ingredient column is; sticks above the shell's bottom inset. */
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
      padding-block: var(--space-4);
    }

    /* A short edge fade, like a toolbar's scroll edge: solid only below the button's middle, so the recipe
       stays readable up to the button instead of disappearing behind a band (it once covered ~110px). */
    .cook-action .foot::before {
      content: '';
      position: absolute;
      inset: 0;
      z-index: -1;
      background: linear-gradient(to top, var(--surface) 35%, transparent);
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
      /* Follows the bottom bar as it slides away and back. */
      transition: top var(--duration-base) var(--ease-out);
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
      white-space: nowrap;
    }

    .foot :global(.button.secondary) {
      flex: none;
      width: var(--control-lg);
      padding-inline: 0;
    }

    .foot :global(.button.secondary .label) {
      display: none;
    }
  }

  @media print {
    .foot,
    .cook-action {
      display: none !important;
    }
  }
</style>

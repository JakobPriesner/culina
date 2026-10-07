<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  /** The controls pinned to the bottom while cooking: back a step, the ingredients, on. */
  interface Props {
    currentStep: number;
    stepCount: number;
    /** How tall the dock is, shared with the steps so none scrolls in under it. */
    height?: number;
    onprevious: () => void;
    onnext: () => void;
    /** Opens the ingredients, which are not beside the steps on a compact screen. */
    oningredients: () => void;
  }

  let {
    currentStep,
    stepCount,
    height = $bindable(0),
    onprevious,
    onnext,
    oningredients
  }: Props = $props();

  const last = $derived(currentStep === stepCount - 1);
</script>

<div class="dock" bind:clientHeight={height}>
  <div class="step-controls">
    <Button size="sm" disabled={currentStep === 0} onclick={onprevious}
      >{m['preview.previous']()}</Button
    >
    <div class="mobile-ingredients">
      <Button size="sm" onclick={oningredients}>{m['preview.ingredients']()}</Button>
    </div>
    <Button
      size="sm"
      variant="primary"
      label={last ? m['preview.done']() : undefined}
      onclick={onnext}
    >
      {last ? m['preview.doneShort']() : m['preview.next']()}
      <span aria-hidden="true">{last ? '✓' : '→'}</span>
    </Button>
  </div>
</div>

<style>
  .dock {
    position: fixed;
    bottom: 0;
    inset-inline: 0;
    z-index: 2;
    background: var(--surface);
    border-top: 1px solid var(--border);
  }

  .step-controls {
    display: flex;
    justify-content: space-between;
    gap: var(--space-3);
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--space-3) var(--layout-gutter-end)
      calc(var(--space-3) + env(safe-area-inset-bottom, 0px)) var(--layout-gutter-start);
  }

  .step-controls :global(button) {
    min-height: var(--control-lg);
  }

  .mobile-ingredients {
    display: none;
  }

  @media (width < 64rem) {
    .mobile-ingredients {
      display: block;
    }

    .step-controls {
      gap: var(--space-2);
      padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
    }
  }

  @media (max-width: 23.999rem) {
    .step-controls :global(button) {
      padding-inline: var(--space-2);
    }
  }

  @media screen and (max-height: 32rem) {
    .dock {
      position: static;
      margin-top: var(--space-6);
    }
  }
</style>

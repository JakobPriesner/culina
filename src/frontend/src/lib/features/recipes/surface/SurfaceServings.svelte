<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';
  import ServingsControl from './ServingsControl.svelte';
  import type { Scaling } from './scaled.svelte';
  import type { RecipeReading } from '../types';

  /** Same position in both weightings; moving it breaks the transition. */
  interface Props {
    recipe: RecipeReading;
    servings: number;
    scaling: Scaling;
    onservings?: (value: number) => void;
    /** Opens the sheet that scales by an amount rather than a number of portions. */
    onscaleto: () => void;
  }

  let { recipe, servings, scaling, onservings, onscaleto }: Props = $props();
</script>

<div class="servings">
  <ServingsControl
    value={servings}
    kind={recipe.yieldKind}
    label={recipe.yieldLabel}
    base={recipe.yieldAmount}
    onchange={(value) => onservings?.(value)}
  />

  <Button onclick={onscaleto}>{m['scaleTo.open']()}</Button>

  {#if scaling.timesAreDoubtful}
    <!-- Baking time follows thickness and oven temperature does not scale, so warn rather than invent a formula. -->
    <p class="warning">
      {m['recipe.timesWarning']({ count: scaling.baseYieldLabel })}
    </p>
  {/if}
</div>

<style>
  .servings {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-4);
    padding-block: var(--space-6);
    border-block: 1px solid var(--border);
  }

  .warning {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  @media (width < 52rem) {
    .servings {
      gap: var(--space-2);
      padding-block: var(--space-3);
    }

    .servings :global(.button) {
      min-height: var(--control-sm);
      padding-inline: var(--space-3);
      font-size: var(--text-sm);
    }
  }

  @media print {
    .servings {
      display: none !important;
    }
  }
</style>

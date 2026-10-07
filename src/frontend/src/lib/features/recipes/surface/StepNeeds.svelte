<script lang="ts">
  import { m } from '$shell/i18n';
  import type { Scaling } from './scaled.svelte';
  import type { Ingredient } from '../types';

  /**
   * What to get out for one step, above the sentence since it is read first; more than the sentence names, on purpose.
   * Plain text, not controls (forty tab stops otherwise), and absent while cooking, where the ingredient panel already shows it.
   */
  interface Props {
    ingredients: readonly Ingredient[];
    scaling: Scaling;
  }

  let { ingredients, scaling }: Props = $props();
</script>

<p class="needs">
  <span class="label">{m['recipe.stepNeeds']()}</span>

  {#each ingredients as ingredient (ingredient.id)}
    {@const amount = scaling.amountFor(ingredient).text}

    <span class="one">
      {#if amount}<span class="amount">{amount}</span>{/if}{ingredient.name}
    </span>
  {/each}
</p>

<style>
  .needs {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: 0 var(--space-2);
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .label {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  /* Separator between items only, never stranded at a wrapped line end. */
  .one + .one::before {
    content: '·';
    margin-inline-end: var(--space-2);
    color: var(--border-strong);
  }

  .amount {
    margin-inline-end: 0.25em;
    color: var(--text);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  /* Paper keeps this: with no panel to light up or tap, it is the only "what do I get out" on the sheet. */
  @media print {
    .needs {
      color: inherit;
    }
  }
</style>

<script lang="ts">
  import { m } from '$shell/i18n';
  import type { Scaling } from './scaled.svelte';
  import type { Ingredient } from '../types';

  /**
   * What to get out for one step.
   *
   * The sentence above it says what to *do*; this says what to have in front of
   * you before doing it, which is a different question and the one you ask
   * while the pan is still cold. It is more than the sentence names on purpose:
   * "combine everything and knead" needs five things and says none of them.
   *
   * Plain text, not controls. One button per ingredient per step would put
   * forty tab stops between the reader and the end of the method, to say
   * something the line already says by existing.
   *
   * It is absent while cooking — there the ingredient panel has contracted to
   * exactly this list, and saying it twice on a screen you are reading across
   * the kitchen is worse than saying it once.
   */
  interface Props {
    ingredients: readonly Ingredient[];
    /** The one source of every amount on the surface. */
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

  /* The separator belongs between the items, so it cannot be left stranded at
     the end of a wrapped line or after the last one. */
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

  /*
   * Paper keeps this. A printed sheet has no panel to light up and no step to
   * tap, so the line under each step is the only thing on it that answers
   * "what do I get out for this one" — which makes it worth more here than
   * anywhere else.
   */
  @media print {
    .needs {
      color: inherit;
    }
  }
</style>

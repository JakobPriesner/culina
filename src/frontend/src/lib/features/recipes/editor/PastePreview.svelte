<script lang="ts">
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  import { formatQuantity } from '../formatQuantity';
  import { quantityLabels } from '../quantityLabels';
  import { scaleQuantity } from '../scaling';
  import type { ParsedRecipe } from './parseRecipeText';

  interface Props {
    parsed: ParsedRecipe;
  }

  let { parsed }: Props = $props();

  const shown = (quantity: Parameters<typeof scaleQuantity>[0]) =>
    formatQuantity(scaleQuantity(quantity, 1), preferences.locale, quantityLabels).text;
</script>

<div class="preview">
  {#if parsed.title}
    <p class="title">{parsed.title}</p>
  {/if}

  {#if parsed.ingredients.length > 0}
    <h3 class="section">{m['editor.ingredients']()}</h3>
    <ul class="list">
      {#each parsed.ingredients as ingredient, index (index)}
        <li class="row">
          <span class="amount">{shown(ingredient.quantity)}</span>
          <span
            >{ingredient.name}{#if ingredient.note}<span class="note">, {ingredient.note}</span
              >{/if}</span
          >
        </li>
      {/each}
    </ul>
  {/if}

  {#if parsed.steps.length > 0}
    <h3 class="section">{m['editor.steps']()}</h3>
    <ol class="steps">
      {#each parsed.steps as step, index (index)}
        <li>{step}</li>
      {/each}
    </ol>
  {/if}
</div>

<style>
  .preview {
    padding: var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    margin-bottom: var(--space-3);
  }

  .section {
    margin-top: var(--space-3);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .list {
    margin: var(--space-2) 0 0;
    padding: 0;
    list-style: none;
  }

  .row {
    display: grid;
    grid-template-columns: minmax(4rem, auto) minmax(0, 1fr);
    gap: var(--space-3);
    padding-block: var(--space-1);
  }

  .amount {
    font-variant-numeric: tabular-nums;
    font-weight: var(--weight-medium);
    white-space: nowrap;
  }

  .note {
    color: var(--text-muted);
  }

  .steps {
    /* Numbers inside so they align with the headings. */
    list-style-position: inside;
    margin: var(--space-2) 0 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }
</style>

<script lang="ts">
  import type { LineCorrection } from './lineCorrection.svelte';

  /** The correction sheet for whichever line `correction` has open; downloaded the first time one is. */
  interface Props {
    correction: LineCorrection;
    householdId: string | null;
  }

  let { correction, householdId }: Props = $props();

  const row = $derived(correction.editedRow);
</script>

{#if correction.opened}
  {#await import('./NutritionCorrectionSheet.svelte') then { default: NutritionCorrectionSheet }}
    <NutritionCorrectionSheet
      name={row?.ingredient.name ?? null}
      {householdId}
      line={row?.line ?? null}
      unit={row ? correction.unitWordOf(row) : null}
      perUnit={row ? correction.perUnitOf(row) : null}
      weighFirst={correction.weighing}
      onchoose={correction.choose}
      onweigh={(grams) => void correction.weigh(grams)}
      onexclude={() => void correction.correct({ kind: 'exclude' })}
      onreset={() => void correction.correct({ kind: 'default' })}
      onclose={correction.close}
    />
  {/await}
{/if}

<script lang="ts">
  import { VisuallyHidden } from '$ds';
  import { m } from '$shell/i18n';

  import { labelNumber, nbsp, withBound } from './format';
  import { columnWords } from './headline';
  import { roundForLabel } from './rounding';
  import type { Nutrient } from './rounding';
  import type { Nutrition, NutritionValue } from './types';

  /**
   * The values in the order and indentation of a package (Regulation (EU) 1169/2011): energy in both
   * units, fat and what is in it, carbohydrate and what is in it, protein, salt. One column.
   */
  interface Props {
    nutrition: Nutrition;
  }

  let { nutrition }: Props = $props();

  const values = $derived(nutrition.values);

  /** A lower bound that rounds to nothing says nothing: "at least 0 g" is not a value, so it is a dash, read as "not known". */
  const unknown = (nutrient: Nutrient, value: NutritionValue) =>
    value.atLeast && roundForLabel(nutrient, value.value, true).value === 0;

  const grams = (nutrient: Nutrient, value: NutritionValue) =>
    withBound(`${labelNumber(nutrient, value)}${nbsp}g`, value.atLeast);

  const energy = $derived(
    withBound(
      `${labelNumber('energy', values.energyKj)}${nbsp}kJ / ${labelNumber('energy', values.energyKcal)}${nbsp}kcal`,
      values.energyKj.atLeast || values.energyKcal.atLeast
    )
  );

  const entry = (label: string, nutrient: Nutrient, value: NutritionValue, sub: boolean) => ({
    label,
    text: grams(nutrient, value),
    unknown: unknown(nutrient, value),
    sub
  });

  const rows = $derived([
    entry(m['nutrition.fat'](), 'macro', values.fat, false),
    entry(m['nutrition.saturates'](), 'saturates', values.saturatedFat, true),
    entry(m['nutrition.carbohydrate'](), 'macro', values.carbohydrate, false),
    entry(m['nutrition.sugars'](), 'macro', values.sugars, true),
    entry(m['nutrition.protein'](), 'macro', values.protein, false),
    entry(m['nutrition.salt'](), 'salt', values.salt, false)
  ]);
</script>

<table class="label">
  <thead>
    <tr>
      <th scope="col"><VisuallyHidden>{m['nutrition.nutrient']()}</VisuallyHidden></th>
      <th scope="col" class="value">
        {columnWords(nutrition)}
      </th>
    </tr>
  </thead>

  <tbody>
    <tr class="energy">
      <th scope="row">{m['nutrition.energy']()}</th>
      <td class="value">{energy}</td>
    </tr>

    {#each rows as row (row.label)}
      <tr class:sub={row.sub}>
        <th scope="row">{row.label}</th>
        <td class="value">
          {#if row.unknown}
            <span aria-hidden="true">–</span><VisuallyHidden
              >{m['nutrition.notKnown']()}</VisuallyHidden
            >
          {:else}
            {row.text}
          {/if}
        </td>
      </tr>
    {/each}
  </tbody>
</table>

<style>
  .label {
    width: 100%;
    border-collapse: collapse;
    font-size: var(--text-sm);
  }

  th,
  td {
    padding-block: var(--space-4);
    border-bottom: 1px solid var(--border);
    text-align: start;
    font-weight: var(--weight-regular);
  }

  thead th {
    color: var(--text-muted);
    padding-top: 0;
    padding-bottom: var(--space-3);
    font-size: var(--text-xs);
  }

  .energy > * {
    font-weight: var(--weight-semibold);
  }

  tbody tr:last-child > * {
    border-bottom: 0;
  }

  .value {
    text-align: end;
    font-variant-numeric: tabular-nums;
    padding-inline-start: var(--space-3);
    line-height: var(--leading-normal);
  }

  /* "of which": what a reader knows from the pack. */
  .sub th {
    padding-inline-start: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  @media print {
    .label {
      break-inside: avoid;
    }
  }
</style>

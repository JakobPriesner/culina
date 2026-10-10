<script lang="ts">
  import { VisuallyHidden } from '$ds';
  import { m } from '$shell/i18n';

  import { labelNumber, nbsp, withBound } from './format';
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

  const grams = (nutrient: Nutrient, value: NutritionValue) =>
    withBound(`${labelNumber(nutrient, value)}${nbsp}g`, value.atLeast);

  const energy = $derived(
    withBound(
      `${labelNumber('energy', values.energyKj)}${nbsp}kJ / ${labelNumber('energy', values.energyKcal)}${nbsp}kcal`,
      values.energyKj.atLeast || values.energyKcal.atLeast
    )
  );

  const rows = $derived([
    { label: m['nutrition.fat'](), text: grams('macro', values.fat), sub: false },
    { label: m['nutrition.saturates'](), text: grams('saturates', values.saturatedFat), sub: true },
    {
      label: m['nutrition.carbohydrate'](),
      text: grams('macro', values.carbohydrate),
      sub: false
    },
    { label: m['nutrition.sugars'](), text: grams('macro', values.sugars), sub: true },
    { label: m['nutrition.protein'](), text: grams('macro', values.protein), sub: false },
    { label: m['nutrition.salt'](), text: grams('salt', values.salt), sub: false }
  ]);
</script>

<table class="label">
  <thead>
    <tr>
      <th scope="col"><VisuallyHidden>{m['nutrition.nutrient']()}</VisuallyHidden></th>
      <th scope="col" class="value">
        {nutrition.per === 'piece' ? m['nutrition.perPiece']() : m['nutrition.perServing']()}
      </th>
    </tr>
  </thead>

  <tbody>
    <tr>
      <th scope="row">{m['nutrition.energy']()}</th>
      <td class="value">{energy}</td>
    </tr>

    {#each rows as row (row.label)}
      <tr class:sub={row.sub}>
        <th scope="row">{row.label}</th>
        <td class="value">{row.text}</td>
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
    padding-block: var(--space-1);
    border-bottom: 1px solid var(--border);
    text-align: start;
    font-weight: var(--weight-regular);
  }

  thead th {
    color: var(--text-muted);
  }

  tbody tr:last-child > * {
    border-bottom: 0;
  }

  .value {
    text-align: end;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  /* "of which": what a reader knows from the pack. */
  .sub th {
    padding-inline-start: var(--space-4);
    color: var(--text-muted);
  }

  @media print {
    .label {
      break-inside: avoid;
    }
  }
</style>

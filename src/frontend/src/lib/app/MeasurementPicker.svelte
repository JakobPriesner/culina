<script lang="ts">
  import { Select } from '$ds';

  import { m } from './i18n';
  import { preferences } from './preferences.svelte';
  import type { MeasurementSystem } from '$features/recipes/measurement';

  /** Which units a recipe's amounts are *shown* in; storage is unchanged, so switching back leaves it exactly as it was. */
  const id = 'measurement-picker';

  /** Hides the label for a caller that already names the control. */
  let { compact = false }: { compact?: boolean } = $props();

  const options = $derived([
    { value: 'metric', label: m['measurement.metric']() },
    { value: 'imperial', label: m['measurement.imperial']() }
  ]);
</script>

<div class="field">
  <label class="label" class:ds-clipped={compact} for={id}>{m['measurement.label']()}</label>

  <Select
    {id}
    {options}
    inline
    value={preferences.measurementSystem}
    onchange={(value) => preferences.setMeasurementSystem(value as MeasurementSystem)}
  />
</div>

<style>
  .field {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
  }

  .label {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>

<script lang="ts">
  import { m } from './i18n';
  import { preferences } from './preferences.svelte';
  import type { MeasurementSystem } from '$features/recipes/measurement';

  /**
   * Which units a recipe's amounts are shown in.
   *
   * A plain select, like the language beside it: there are two systems and
   * everyone recognises the control.
   *
   * It changes only what is *shown*. A recipe is stored in whatever its author
   * wrote, so switching this and switching back leaves it exactly as it was —
   * and a German recipe shared with an American is one recipe, read two ways.
   */
  const names: Record<MeasurementSystem, () => string> = {
    metric: m['measurement.metric'],
    imperial: m['measurement.imperial']
  };

  const id = 'measurement-picker';
</script>

<div class="field">
  <label class="label" for={id}>{m['measurement.label']()}</label>
  <select
    {id}
    class="select ds-control"
    value={preferences.measurementSystem}
    onchange={(event) =>
      preferences.setMeasurementSystem(event.currentTarget.value as MeasurementSystem)}
  >
    <option value="metric">{names.metric()}</option>
    <option value="imperial">{names.imperial()}</option>
  </select>
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

  .select {
    width: auto;
  }
</style>

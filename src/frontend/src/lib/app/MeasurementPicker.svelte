<script lang="ts">
  import { Select } from '$ds';

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

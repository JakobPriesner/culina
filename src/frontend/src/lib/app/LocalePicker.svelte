<script lang="ts">
  import { Select } from '$ds';

  import { locales, m, type LocaleChoice } from './i18n';
  import { preferences } from './preferences.svelte';

  let { compact = false }: { compact?: boolean } = $props();

  /**
   * A plain select: there are three choices, everyone recognises the control,
   * and a custom menu would buy nothing but keyboard bugs.
   *
   * Following the device comes first because it is where everybody starts,
   * and the one choice that is right on a phone and a laptop set to different
   * languages.
   */
  const names: Record<LocaleChoice, () => string> = {
    system: m['locale.system'],
    en: m['locale.en'],
    de: m['locale.de']
  };

  const choices: readonly LocaleChoice[] = ['system', ...locales];

  const id = 'locale-picker';

  const options = $derived(choices.map((choice) => ({ value: choice, label: names[choice]() })));
</script>

<div class="field">
  <label class="label" class:ds-clipped={compact} for={id}>{m['locale.label']()}</label>

  <Select
    {id}
    {options}
    inline
    value={preferences.localeChoice}
    onchange={(value) => preferences.setLocale(value)}
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

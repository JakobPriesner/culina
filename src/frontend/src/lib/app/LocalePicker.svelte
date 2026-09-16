<script lang="ts">
  import { Select } from '$ds';

  import { locales, m, type Locale } from './i18n';
  import { preferences } from './preferences.svelte';

  let { compact = false }: { compact?: boolean } = $props();

  /**
   * A plain select: there are two languages, everyone recognises the control,
   * and a custom menu would buy nothing but keyboard bugs.
   */
  const names: Record<Locale, () => string> = {
    en: m['locale.en'],
    de: m['locale.de']
  };

  const id = 'locale-picker';

  const options = $derived(locales.map((locale) => ({ value: locale, label: names[locale]() })));
</script>

<div class="field">
  <label class="label" class:ds-clipped={compact} for={id}>{m['locale.label']()}</label>

  <Select
    {id}
    {options}
    inline
    value={preferences.locale}
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

<script lang="ts">
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
</script>

<div class="field">
  <label class="label" class:compact for={id}>{m['locale.label']()}</label>
  <select
    {id}
    class="select"
    value={preferences.locale}
    onchange={(event) => preferences.setLocale(event.currentTarget.value)}
  >
    {#each locales as locale (locale)}
      <option value={locale}>{names[locale]()}</option>
    {/each}
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
    min-height: var(--control-sm);
    padding: var(--space-1) var(--space-2);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
  }

  .label.compact {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }
</style>

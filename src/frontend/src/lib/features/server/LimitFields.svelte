<script lang="ts">
  import { m } from '$shell/i18n';

  import SettingField from './SettingField.svelte';
  import {
    limitVariable,
    rateLimits,
    wholeNumber,
    type ServerDraft,
    type ServerFacts
  } from './types';

  /** The ceilings on what one visitor may ask for. */
  interface Props {
    draft: ServerDraft;
    facts: ServerFacts;
    disabled?: boolean;
  }

  let { draft = $bindable(), facts, disabled = false }: Props = $props();
</script>

<div class="limits">
  {#each rateLimits as limit (limit)}
    <SettingField
      label={m[`server.limit.${limit}`]()}
      variable={limitVariable(limit)}
      pinned={facts.pinned.has(limitVariable(limit))}
      inputmode="numeric"
      error={wholeNumber(draft.limits[limit]) === null ? m['server.number.invalid']() : undefined}
      {disabled}
      bind:value={draft.limits[limit]}
    />
  {/each}
</div>

<style>
  /* Two columns where there is room: nine short numbers in one column is a
     page of scrolling past boxes three characters wide. */
  .limits {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(min(100%, 16rem), 1fr));
    gap: var(--space-4) var(--space-6);
  }
</style>

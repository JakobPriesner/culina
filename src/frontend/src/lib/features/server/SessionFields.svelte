<script lang="ts">
  import { m } from '$shell/i18n';

  import SettingField from './SettingField.svelte';
  import { variables, wholeNumber, type ServerDraft, type ServerFacts } from './types';

  /** How long a session lasts, and how often a visit extends it. */
  interface Props {
    draft: ServerDraft;
    facts: ServerFacts;
    disabled?: boolean;
  }

  let { draft = $bindable(), facts, disabled = false }: Props = $props();

  const problem = (text: string) =>
    wholeNumber(text) === null ? m['server.number.invalid']() : undefined;
</script>

<SettingField
  label={m['server.sessionDays']()}
  hint={m['server.sessionDays.hint']()}
  variable={variables.sessionDays}
  pinned={facts.pinned.has(variables.sessionDays)}
  inputmode="numeric"
  error={problem(draft.sessionDays)}
  {disabled}
  bind:value={draft.sessionDays}
/>

<SettingField
  label={m['server.renewAfterHours']()}
  hint={m['server.renewAfterHours.hint']()}
  variable={variables.renewAfterHours}
  pinned={facts.pinned.has(variables.renewAfterHours)}
  inputmode="numeric"
  error={problem(draft.renewAfterHours)}
  {disabled}
  bind:value={draft.renewAfterHours}
/>

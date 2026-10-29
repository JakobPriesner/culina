<script lang="ts">
  import { m } from '$shell/i18n';

  import SettingField from './SettingField.svelte';
  import { variables, type ServerDraft, type ServerFacts } from './types';

  /** The relay the installed apps are notified through; naming one sends words outside this server, which the section says. */
  interface Props {
    draft: ServerDraft;
    facts: ServerFacts;
    disabled?: boolean;
  }

  let { draft = $bindable(), facts, disabled = false }: Props = $props();
</script>

<SettingField
  label={m['server.push.relay']()}
  hint={m['server.push.relay.hint']()}
  variable={variables.pushRelayUrl}
  pinned={facts.pinned.has(variables.pushRelayUrl)}
  type="url"
  inputmode="url"
  placeholder="https://relay.example.com"
  {disabled}
  bind:value={draft.pushRelayUrl}
/>

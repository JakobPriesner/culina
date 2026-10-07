<script lang="ts">
  import { Field, TextInput } from '$ds';
  import type { AiSettingsDraft } from '$features/assistance/settings/createAiSettingsDraft.svelte';
  import { providerFacts, type Provider } from '$features/assistance/types';

  /** Where a provider is, for the one that is told and the ones that are overridden. */
  interface Props {
    settings: AiSettingsDraft;
    provider: Provider;
    label: string;
    hint?: string;
  }

  let { settings, provider, label, hint }: Props = $props();
</script>

<Field {label} {hint}>
  {#snippet children({ id, describedBy, invalid })}
    <TextInput
      {id}
      {describedBy}
      {invalid}
      type="url"
      placeholder={providerFacts[provider].addressHint}
      value={settings.connectionFor(provider).baseUrl}
      oninput={(value) => settings.editConnection(provider, { baseUrl: value })}
    />
  {/snippet}
</Field>

<script lang="ts">
  import { Badge, Button, Field, TextInput } from '$ds';
  import type { AiSettingsDraft } from '$features/assistance/settings/createAiSettingsDraft.svelte';
  import { providerName } from '$features/assistance/settings/labels';
  import { catalogueProblem } from '$features/assistance/settings/modelChoices';
  import { assistance } from '$features/assistance/stores/assistance.svelte';
  import { providerFacts, providers } from '$features/assistance/types';
  import { m } from '$shell/i18n';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';
  import ProviderAddress from './ProviderAddress.svelte';

  // The API key is never returned by the API, so it isn't a plain field: an empty box would read as "no key".
  interface Props {
    settings: AiSettingsDraft;
  }

  let { settings }: Props = $props();
</script>

<SettingsSection title={m['ai.connections']()} description={m['ai.connections.hint']()}>
  {#each providers as provider (provider)}
    {@const facts = providerFacts[provider]}
    {@const connection = settings.connectionFor(provider)}
    {@const problem = catalogueProblem(assistance.models, provider)}

    <SettingsRow
      label={providerName(provider)}
      description={facts.needsApiKey ? m['ai.apiKey.hint']() : m['ai.address.required']()}
    >
      <Badge tone={connection.usable ? 'success' : 'neutral'}>
        {connection.usable ? m['ai.connected']() : m['ai.notConnected']()}
      </Badge>

      {#if !facts.needsApiKey}
        <ProviderAddress {settings} {provider} label={m['ai.address']()} />
      {:else if !settings.keyOpen.includes(provider)}
        <span class="state">
          {connection.apiKeyConfigured ? m['ai.apiKey.set']() : m['ai.apiKey.none']()}
        </span>
        <Button variant="secondary" size="sm" onclick={() => settings.openKey(provider)}>
          {connection.apiKeyConfigured ? m['ai.apiKey.replace']() : m['ai.apiKey.add']()}
        </Button>
        {#if connection.apiKeyConfigured}
          <!-- Explicit removal: an emptied field must never delete the stored key. -->
          <Button variant="ghost" size="sm" onclick={() => settings.removeKey(provider)}>
            {m['ai.apiKey.remove']()}
          </Button>
        {/if}
      {:else}
        <Field label={m['ai.apiKey']()}>
          {#snippet children({ id, describedBy, invalid })}
            <TextInput
              {id}
              {describedBy}
              {invalid}
              type="password"
              revealLabel={m['field.showKey']()}
              autocomplete="off"
              placeholder={m['ai.apiKey.placeholder']()}
              value={connection.apiKey ?? ''}
              oninput={(value) => settings.editConnection(provider, { apiKey: value || undefined })}
            />
          {/snippet}
        </Field>
        <Button variant="ghost" size="sm" onclick={() => settings.closeKey(provider)}>
          {m['ai.apiKey.cancel']()}
        </Button>
      {/if}

      {#if problem}
        <p class="failure" role="alert">{problem}</p>
      {/if}
    </SettingsRow>
  {/each}
</SettingsSection>

<style>
  .state {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .failure {
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>

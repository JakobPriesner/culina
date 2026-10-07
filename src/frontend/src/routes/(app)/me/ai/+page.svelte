<script lang="ts">
  import { onDestroy, onMount } from 'svelte';

  import { Disclosure } from '$ds';
  import { createAiSettingsDraft } from '$features/assistance/settings/createAiSettingsDraft.svelte';
  import { providerName } from '$features/assistance/settings/labels';
  import { keyNeededAgain } from '$features/assistance/settings/modelChoices';
  import { assistance } from '$features/assistance/stores/assistance.svelte';
  import { providerFacts, providers } from '$features/assistance/types';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';
  import AiBudget from './AiBudget.svelte';
  import AiUsage from './AiUsage.svelte';
  import JobAssignments from './JobAssignments.svelte';
  import ProviderAddress from './ProviderAddress.svelte';
  import ProviderConnections from './ProviderConnections.svelte';

  const settings = createAiSettingsDraft();

  onMount(() => settings.load());
  onDestroy(() => settings.dispose());
</script>

<svelte:head><title>{m['me.ai']()}</title></svelte:head>

<!-- Focus leaving any field commits it; on the document because commit() is a no-op when nothing is owed. -->
<svelte:document onfocusout={settings.commit} />

{#if settings.draft}
  <ProviderConnections {settings} />

  <p class="privacy">
    {settings.anythingHosted ? m['ai.privacy.mixed']() : m['ai.privacy.local']()}
  </p>

  <JobAssignments {settings} />

  <SettingsSection bare>
    <Disclosure summary={m['ai.advanced']()}>
      <div class="advanced">
        <p class="note">{m['ai.advanced.hint']()}</p>

        {#each providers as provider (provider)}
          {#if providerFacts[provider].needsApiKey}
            <ProviderAddress
              {settings}
              {provider}
              label={`${providerName(provider)} — ${m['ai.address']()}`}
              hint={keyNeededAgain(assistance.settings, settings.connectionFor(provider))
                ? m['ai.address.keyAgain']()
                : m['ai.address.optional']({ provider: providerName(provider) })}
            />
          {/if}
        {/each}
      </div>
    </Disclosure>
  </SettingsSection>

  <AiBudget {settings} />

  {#if assistance.usage}
    <AiUsage usage={assistance.usage} />
  {/if}
{/if}

<style>
  /* Outside the sections: a statement about the whole screen, not a setting. */
  .privacy {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .advanced {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding-top: var(--space-3);
  }
</style>

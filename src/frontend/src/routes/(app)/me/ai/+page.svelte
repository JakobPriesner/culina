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

  /**
   * Which models this instance can talk to, and which of them does what.
   *
   * Two lists rather than one form, because they answer different questions and
   * change at different rates: providers are connected once, jobs are
   * reassigned whenever a new model is better at one. What is edited, and how
   * it is kept, is `createAiSettingsDraft`; this page only lays it out.
   */
  const settings = createAiSettingsDraft();

  onMount(() => settings.load());
  onDestroy(() => settings.dispose());
</script>

<svelte:head><title>{m['me.ai']()}</title></svelte:head>

<!-- Focus leaving any field is the moment a typed value is done. Heard on the
     document rather than on a wrapper: commit() does nothing when nothing is
     owed, so hearing the rest of the page costs nothing. -->
<svelte:document onfocusout={settings.commit} />

{#if settings.draft}
  <ProviderConnections {settings} />

  <!-- Said plainly, before anybody turns anything on. -->
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
  /* Outside the enclosures, because it is a statement about the whole screen
     rather than a setting on it. */
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

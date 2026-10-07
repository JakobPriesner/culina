<script lang="ts">
  import { Field, Select, TextInput } from '$ds';
  import type { AiSettingsDraft } from '$features/assistance/settings/createAiSettingsDraft.svelte';
  import { jobHint, jobLabel, providerName } from '$features/assistance/settings/labels';
  import {
    modelsFor,
    offersWholeCatalogue,
    providerChoices,
    unlistedByProvider
  } from '$features/assistance/settings/modelChoices';
  import { assistance } from '$features/assistance/stores/assistance.svelte';
  import { capabilities, type Provider } from '$features/assistance/types';
  import { m } from '$shell/i18n';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';

  /**
   * Provider and model per job; one global choice meant picking the provider least bad at
   * everything.
   */
  interface Props {
    settings: AiSettingsDraft;
  }

  let { settings }: Props = $props();
</script>

{#if assistance.unlisted}
  <!-- The listing itself failed, so no per-provider row says why the pickers are bare text boxes. -->
  <p class="failure" role="alert">{m['ai.models.unlisted']()}</p>
{/if}

<SettingsSection title={m['ai.jobs']()} description={m['ai.jobs.hint']()}>
  {#each capabilities as capability (capability)}
    {@const use = settings.useFor(capability)}

    {@const choices = modelsFor(assistance.models, capability, use)}

    <SettingsRow label={jobLabel(capability)} description={jobHint(capability)}>
      <Field label={m['ai.job.provider']()}>
        {#snippet children({ id, describedBy, invalid })}
          <Select
            {id}
            {describedBy}
            {invalid}
            inline
            value={use.provider}
            options={providerChoices(capability)}
            onchange={(value) => {
              settings.editUse(capability, {
                provider: value as Provider | '',
                // Choosing a provider switches the job on; "not offered" switches it off.
                enabled: value !== '',
                // The old model belonged to the old provider.
                model: ''
              });
              settings.commit();
            }}
          />
        {/snippet}
      </Field>

      {#if use.provider !== ''}
        {#if !choices && assistance.listing}
          <!-- A placeholder select, not the text box, which would be swapped for a select under
               what was typed. -->
          <Field label={m['ai.job.model']()}>
            {#snippet children({ id, describedBy, invalid })}
              <Select
                {id}
                {describedBy}
                {invalid}
                inline
                disabled
                value=""
                options={[{ value: '', label: m['ai.models.listing']() }]}
              />
            {/snippet}
          </Field>
        {:else if choices}
          <!-- The whole catalogue when the filter came up empty; the hint says so. -->
          {@const unfiltered = offersWholeCatalogue(assistance.models, capability, use)}
          <Field
            label={m['ai.job.model']()}
            hint={unfiltered
              ? m['ai.models.unfiltered']({ provider: providerName(use.provider) })
              : undefined}
          >
            {#snippet children({ id, describedBy, invalid })}
              <Select
                {id}
                {describedBy}
                {invalid}
                inline
                value={use.model}
                options={choices}
                onchange={(value) => {
                  settings.editUse(capability, { model: value });
                  settings.commit();
                }}
              />
            {/snippet}
          </Field>
        {:else}
          <!-- No list at all: a text box loses only the convenience, not the ability to configure. -->
          <Field
            label={m['ai.job.model']()}
            hint={unlistedByProvider(assistance.models, use)
              ? m['ai.models.seeConnection']({ provider: providerName(use.provider) })
              : undefined}
          >
            {#snippet children({ id, describedBy, invalid })}
              <TextInput
                {id}
                {describedBy}
                {invalid}
                placeholder={use.defaultModel || m['ai.models.typed']()}
                value={use.model}
                oninput={(value) => settings.editUse(capability, { model: value })}
              />
            {/snippet}
          </Field>
        {/if}
      {/if}
    </SettingsRow>
  {/each}
</SettingsSection>

<style>
  .failure {
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>

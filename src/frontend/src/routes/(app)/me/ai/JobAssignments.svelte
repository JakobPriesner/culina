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
   * Which provider and model does each job.
   *
   * A provider is connected once; which provider does a job is changed
   * whenever somebody reads that a new model is better at it. Flattening the
   * two into "the assistant's settings" is what made this a single global
   * choice — and a single global choice meant picking the provider that was
   * least bad at everything.
   */
  interface Props {
    settings: AiSettingsDraft;
  }

  let { settings }: Props = $props();
</script>

{#if assistance.unlisted}
  <!-- The listing itself did not happen, so there is no per-provider row to
       say so. Without this the pickers below are bare text boxes and the
       screen gives no reason for it. -->
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
                // Choosing a provider is switching the job on; choosing
                // "not offered" is switching it off. One gesture, because
                // there is no state where both answers are interesting.
                enabled: value !== '',
                // The old model belonged to the old provider. Carrying it
                // over would name something the new one has never heard of.
                model: ''
              });
              settings.commit();
            }}
          />
        {/snippet}
      </Field>

      {#if use.provider !== ''}
        {#if !choices && assistance.listing}
          <!-- The provider has not answered yet. A placeholder select rather
               than the text box below it: the box would be replaced by a
               select a moment later, under whatever had been typed into it. -->
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
          <!-- The whole catalogue rather than the filtered part of it, when
               the filter came up empty. Said plainly under the picker, so
               nobody wonders why a writing model is being offered to the
               job that draws. -->
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
          <!-- No list to choose from at all. A text box is what this was
               before lists existed, and it still works — the convenience is
               what is lost, never the ability to configure anything. -->
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

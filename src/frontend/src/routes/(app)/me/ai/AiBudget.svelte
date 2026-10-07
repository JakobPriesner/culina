<script lang="ts">
  import { Switch } from '$ds';
  import type { AiSettingsDraft } from '$features/assistance/settings/createAiSettingsDraft.svelte';
  import { m } from '$shell/i18n';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';
  import BudgetField from './BudgetField.svelte';

  interface Props {
    settings: AiSettingsDraft;
  }

  let { settings }: Props = $props();

  const draft = $derived(settings.draft);
</script>

{#if draft}
  <SettingsSection title={m['ai.budget']()} description={m['ai.budget.hint']()}>
    <SettingsRow label={m['ai.enabled']()} description={m['ai.enabled.hint']()}>
      <Switch
        checked={draft.enabled}
        label={m['ai.enabled']()}
        onchange={(checked) => settings.setEnabled(checked)}
      />
    </SettingsRow>

    <BudgetField
      label={m['ai.budget.monthly']()}
      value={draft.monthlyBudget}
      onchange={(value) => settings.setBudget('monthlyBudget', value)}
    />
    <BudgetField
      label={m['ai.budget.personal']()}
      value={draft.personalBudget}
      onchange={(value) => settings.setBudget('personalBudget', value)}
    />
  </SettingsSection>
{/if}

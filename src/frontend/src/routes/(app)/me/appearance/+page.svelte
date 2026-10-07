<script lang="ts">
  import { importPush } from '$features/import/push.svelte';
  import { Button } from '$ds';
  import { Switch } from '$ds';
  import AppIconChoice from '$shell/AppIconChoice.svelte';
  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import MeasurementPicker from '$shell/MeasurementPicker.svelte';
  import { olliSetting } from '$shell/olli/setting.svelte';
  import ThemeChoice from '$shell/ThemeChoice.svelte';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';

  /**
   * How the app looks and which words and units it uses; set once, so not in the top bar, and in one enclosure as "how this reads to me".
   * Each picker is `compact`: the row prints the label and the picker keeps its own clipped one, so the select has exactly one name.
   */
</script>

<svelte:head><title>{m['me.appearance']()}</title></svelte:head>

<SettingsSection>
  <SettingsRow label={m['me.theme']()} group>
    <ThemeChoice />
  </SettingsRow>

  <SettingsRow label={m['me.appIcon']()} description={m['me.appIcon.hint']()} group>
    <AppIconChoice />
  </SettingsRow>

  <SettingsRow label={m['locale.label']()}>
    <LocalePicker compact />
  </SettingsRow>

  <SettingsRow label={m['measurement.label']()} description={m['me.amounts.hint']()}>
    <MeasurementPicker compact />
  </SettingsRow>

  <SettingsRow label={m['me.olli']()} description={m['me.olli.hint']()}>
    <Switch
      checked={olliSetting.animated}
      label={m['me.olli']()}
      onchange={(checked) => olliSetting.animate(checked)}
    />
  </SettingsRow>
  {#if importPush.supported}
    <SettingsRow
      label={m['intake.notify']()}
      description={importPush.enabled ? m['intake.notificationsOn']() : m['intake.continues']()}
    >
      <Button
        onclick={() => void (importPush.enabled ? importPush.disable() : importPush.enable())}
        loading={importPush.busy}
        >{importPush.enabled ? m['intake.notificationsOff']() : m['intake.notify']()}</Button
      >
    </SettingsRow>
    {#if importPush.failed}<p role="alert">{m['intake.notificationFailed']()}</p>{/if}
  {/if}
</SettingsSection>

<script lang="ts">
  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import MeasurementPicker from '$shell/MeasurementPicker.svelte';
  import ThemeChoice from '$shell/ThemeChoice.svelte';

  import SettingsRow from '../SettingsRow.svelte';
  import SettingsSection from '../SettingsSection.svelte';

  /**
   * How the app looks and which words and units it uses.
   *
   * These live here rather than in the top bar of every page: they are set once
   * and then never again, and a control used twice a year does not belong where
   * the eye lands every time.
   *
   * Three settings, one enclosure. They are not three unrelated things that
   * happen to share a page — they are the whole of "how this reads to me", and
   * three loose controls a gap apart could not say that.
   *
   * Each picker is `compact`: the row prints the label and the picker keeps its
   * own, clipped, so the select's accessible name is exactly the words above it
   * without the field carrying two labels.
   */
</script>

<svelte:head><title>{m['me.appearance']()}</title></svelte:head>

<SettingsSection>
  <SettingsRow label={m['me.theme']()} group>
    <ThemeChoice />
  </SettingsRow>

  <SettingsRow label={m['locale.label']()}>
    <LocalePicker compact />
  </SettingsRow>

  <SettingsRow label={m['measurement.label']()} description={m['me.amounts.hint']()}>
    <MeasurementPicker compact />
  </SettingsRow>
</SettingsSection>

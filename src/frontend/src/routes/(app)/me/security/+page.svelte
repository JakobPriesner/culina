<script lang="ts">
  import PasswordPanel from '$features/auth/PasswordPanel.svelte';
  import RecoveryCodesPanel from '$features/auth/RecoveryCodesPanel.svelte';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';

  /** The password and recovery codes: their own category since both ask for the password first and the codes need a paragraph that would drown the Account page. */
</script>

<svelte:head><title>{m['me.security']()}</title></svelte:head>

<SettingsSection title={m['me.password.title']()} description={m['me.password.body']()} bare>
  <PasswordPanel />
</SettingsSection>

<SettingsSection title={m['me.recovery.title']()} description={m['me.recovery.body']()} bare>
  <RecoveryCodesPanel />
</SettingsSection>

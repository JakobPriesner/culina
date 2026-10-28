<script lang="ts">
  import ConnectAgentPanel from '$features/auth/ConnectAgentPanel.svelte';
  import DevicesList from '$features/auth/DevicesList.svelte';
  import PasswordPanel from '$features/auth/PasswordPanel.svelte';
  import RecoveryCodesPanel from '$features/auth/RecoveryCodesPanel.svelte';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';

  /**
   * Everything about getting in: the password, recovery codes, where the account is signed in and the AI agents it lets in.
   * One category, not two: the codes and an agent's token both ask for the password first, and a sixth category no longer fits
   * the tablet header.
   */
</script>

<svelte:head><title>{m['me.security']()}</title></svelte:head>

<SettingsSection title={m['me.password.title']()} description={m['me.password.body']()} panel>
  <PasswordPanel />
</SettingsSection>

<SettingsSection title={m['me.recovery.title']()} description={m['me.recovery.body']()} panel>
  <RecoveryCodesPanel />
</SettingsSection>

<SettingsSection title={m['me.devices.title']()} description={m['me.devices.body']()} bare>
  <DevicesList />
</SettingsSection>

<SettingsSection title={m['me.agent.title']()} description={m['me.agent.body']()} panel>
  <ConnectAgentPanel />
</SettingsSection>

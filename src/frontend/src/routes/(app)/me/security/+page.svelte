<script lang="ts">
  import PasswordPanel from '$features/auth/PasswordPanel.svelte';
  import RecoveryCodesPanel from '$features/auth/RecoveryCodesPanel.svelte';
  import { m } from '$shell/i18n';

  import SettingsSection from '../SettingsSection.svelte';

  /**
   * The password, and what to do on the day it is forgotten.
   *
   * Its own category rather than more rows under Account: both sections ask
   * for the password before they do anything, and the recovery codes need a
   * paragraph of explanation that would drown the name and address the
   * account page is for.
   */
</script>

<svelte:head><title>{m['me.security']()}</title></svelte:head>

<SettingsSection title={m['me.password.title']()} description={m['me.password.body']()} bare>
  <PasswordPanel />
</SettingsSection>

<SettingsSection title={m['me.recovery.title']()} description={m['me.recovery.body']()} bare>
  <RecoveryCodesPanel />
</SettingsSection>

<script lang="ts">
  import { onDestroy } from 'svelte';

  import { Button } from '$ds';
  import { ErrorCodes } from '$api';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import FormFailure from './FormFailure.svelte';
  import FormField from './FormField.svelte';
  import { changePassword } from './recovery';
  import { session } from './session.svelte';
  import { createSubmission } from './submission.svelte';

  /** A new password for somebody who still knows the old one, asked for even when signed in so a session left open on another laptop cannot lock the owner out. Other devices are signed out afterwards; this one stays. */
  let currentPassword = $state('');
  let newPassword = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    const succeeded = await submission.run(() => changePassword(currentPassword, newPassword));

    if (!succeeded) {
      return;
    }

    currentPassword = '';
    newPassword = '';
    toaster.show({ message: () => m['me.password.changed'](), tone: 'success' });

    // The account's version moved, so the copy this device holds is stale.
    await session.refresh();
  }
</script>

<form class="form" onsubmit={submit} novalidate>
  <FormFailure
    failure={submission.failure}
    message={submission.failure?.code === ErrorCodes.incorrectPassword
      ? m['me.password.incorrect']()
      : undefined}
  />

  <FormField
    name="currentPassword"
    label={m['me.password.current']()}
    type="password"
    autocomplete="current-password"
    bind:value={currentPassword}
    {submission}
  />

  <FormField
    name="newPassword"
    label={m['me.password.new']()}
    hint={m['auth.register.passwordHint']()}
    type="password"
    autocomplete="new-password"
    bind:value={newPassword}
    {submission}
  />

  <div>
    <Button type="submit" variant="primary" loading={submission.showingProgress}>
      {m['me.password.submit']()}
    </Button>
  </div>
</form>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: 28rem;
  }
</style>

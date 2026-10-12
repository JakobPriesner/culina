<script lang="ts">
  import { onDestroy } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { clientError, connect, type ConnectFailure } from '$api';
  import AuthIntro from '$features/auth/AuthIntro.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import FormField from '$features/auth/FormField.svelte';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { m } from '$shell/i18n';

  /** The app's first screen: which self-hosted Culina it talks to (docs/native-app-design.md, 3.2). Never reached on the website. */
  let address = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  const explanations: Record<ConnectFailure, () => string> = {
    address: m['connect.failed.address'],
    unreachable: m['connect.failed.unreachable'],
    'not-culina': m['connect.failed.notCulina'],
    'not-set-up': m['connect.failed.notSetUp'],
    'server-too-old': m['connect.failed.serverTooOld'],
    'app-too-old': m['connect.failed.appTooOld']
  };

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    const connected = await submission.run(async () => {
      const failure = await connect(address);

      return failure ? clientError(`connect.${failure}`, explanations[failure]()) : null;
    });

    if (connected) {
      await goto(resolve('/(auth)/login'), { replaceState: true });
    }
  }
</script>

<svelte:head><title>{m['connect.title']()}</title></svelte:head>

<form class="form" onsubmit={submit} novalidate>
  <AuthIntro
    eyebrow={m['connect.eyebrow']()}
    title={m['connect.title']()}
    subtitle={m['connect.intro']()}
  />

  <FormFailure failure={submission.failure} message={submission.failure?.detail} />

  <FormField
    name="address"
    label={m['connect.address']()}
    type="url"
    autocomplete="off"
    inputmode="url"
    autofocus
    bind:value={address}
    {submission}
  />

  <SubmitButton label={m['connect.submit']()} {submission} />
</form>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }
</style>

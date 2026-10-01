<script lang="ts">
  import { onDestroy } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { ErrorCodes } from '$api';
  import FormField from '$features/auth/FormField.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { resetPassword } from '$features/auth/recovery';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { m } from '$shell/i18n';

  /**
   * Back in, for somebody who forgot their password.
   *
   * There is no "we sent you a link" here, because Culina sends nothing. The
   * page says where a code comes from instead: the ones they saved, or the
   * person who runs this Culina. On success they are signed straight in with
   * the password they just chose — making somebody type it a second time, on
   * the next screen, is a test nobody asked to sit.
   */
  let email = $state('');
  let code = $state('');
  let password = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    const succeeded = await submission.run(async () => {
      const failure = await resetPassword(email, code, password);

      return failure ?? (await session.signIn(email, password));
    });

    if (!succeeded) {
      return;
    }

    await goto(resolve('/(app)'), { replaceState: true });
  }
</script>

<svelte:head><title>{m['auth.reset.title']()}</title></svelte:head>

<form class="form" onsubmit={submit} novalidate>
  <header class="intro">
    <h1 class="title">{m['auth.reset.title']()}</h1>
    <p class="subtitle">{m['auth.reset.intro']()}</p>
  </header>

  <!-- One message for an unknown address and for a wrong, used or expired
       code: telling them apart says which addresses are registered here. -->
  <FormFailure
    failure={submission.failure}
    message={submission.failure?.code === ErrorCodes.invalidRecoveryCode
      ? m['auth.reset.failed']()
      : undefined}
  />

  <FormField
    name="email"
    label={m['auth.signIn.email']()}
    type="email"
    autocomplete="username"
    inputmode="email"
    autofocus
    bind:value={email}
    {submission}
  />

  <FormField
    name="code"
    label={m['auth.reset.code']()}
    hint={m['auth.reset.codeHint']()}
    autocomplete="one-time-code"
    bind:value={code}
    {submission}
  />

  <FormField
    name="password"
    label={m['auth.reset.password']()}
    hint={m['auth.register.passwordHint']()}
    type="password"
    autocomplete="new-password"
    bind:value={password}
    {submission}
  />

  <SubmitButton label={m['auth.reset.submit']()} {submission} />

  <p class="alternative">
    <a href={resolve('/(auth)/login')}>{m['auth.reset.back']()}</a>
  </p>
</form>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
  }

  .intro {
    margin-bottom: var(--space-6);
  }

  .subtitle {
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
  }

  .alternative {
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-align: center;
  }
</style>

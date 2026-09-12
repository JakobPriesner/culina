<script lang="ts">
  import { onDestroy } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { ErrorCodes } from '$api';
  import FormField from '$features/auth/FormField.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { safeRedirect } from '$features/auth/redirectTarget';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { m } from '$shell/i18n';

  /**
   * Sign in, and go back to wherever you were headed.
   *
   * The `next` parameter is why deep links work: following a link to a recipe
   * while signed out has to end on that recipe, not on the start page.
   */
  let email = $state('');
  let password = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  // From the URL, so from whoever wrote the link: only a path inside this app
  // is accepted. See `safeRedirect`.
  const destination = $derived(safeRedirect(page.url.searchParams.get('next')));

  const registerHref = $derived(
    `${resolve('/(auth)/register')}?next=${encodeURIComponent(destination)}`
  );

  async function submit(event: SubmitEvent) {
    event.preventDefault();

    const succeeded = await submission.run(() => session.signIn(email, password));

    if (!succeeded) {
      // The address stays: retyping it is a punishment for a typo in the other
      // field, and it is the half that is rarely wrong.
      password = '';

      return;
    }

    // Replaced, not pushed: pressing back from inside the app should not land
    // on a sign-in form for a session that already exists.
    await goto(destination, { replaceState: true });
  }
</script>

<svelte:head><title>{m['auth.signIn.title']()}</title></svelte:head>

<form class="form" onsubmit={submit} novalidate>
  <h1 class="title">{m['auth.signIn.title']()}</h1>

  <!--
    One message for "no such account" and for "wrong password", always. Telling
    them apart turns this form into a way to discover which addresses are
    registered here.
  -->
  <FormFailure
    failure={submission.failure}
    message={submission.failure?.code === ErrorCodes.invalidCredentials
      ? m['auth.signIn.failed']()
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
    name="password"
    label={m['auth.signIn.password']()}
    type="password"
    autocomplete="current-password"
    bind:value={password}
    {submission}
  />

  <SubmitButton label={m['auth.signIn.submit']()} {submission} />

  <p class="alternative">
    {m['auth.signIn.noAccount']()}
    <a href={registerHref}>{m['auth.signIn.register']()}</a>
  </p>
</form>

<style>
  .form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .title {
    font-size: var(--text-2xl);
  }

  .alternative {
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-align: center;
  }
</style>

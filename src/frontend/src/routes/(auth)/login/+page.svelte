<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { Button, ErrorState, Field, TextInput } from '$ds';
  import { ErrorCodes, type AppError } from '$api';
  import { safeRedirect } from '$features/auth/redirectTarget';
  import { session } from '$features/auth/session.svelte';
  import { createLoadingState } from '$shell/loadingState.svelte';
  import { m } from '$shell/i18n';

  /**
   * Sign in, and go back to wherever you were headed.
   *
   * The `next` parameter is why deep links work: following a link to a recipe
   * while signed out has to end on that recipe, not on the start page.
   */
  let email = $state('');
  let password = $state('');
  let failure = $state<AppError | null>(null);

  const busy = createLoadingState();

  // From the URL, so from whoever wrote the link: only a path inside this app
  // is accepted. See `safeRedirect`.
  const destination = $derived(safeRedirect(page.url.searchParams.get('next')));

  async function submit(event: SubmitEvent) {
    event.preventDefault();
    failure = null;
    busy.start();

    const error = await session.signIn(email, password);

    busy.stop();

    if (error) {
      failure = error;
      password = '';

      return;
    }

    // Replaced, not pushed: pressing back from inside the app should not land
    // on a sign-in form for a session that already exists.
    // eslint-disable-next-line svelte/no-navigation-without-resolve -- already a resolved in-app path, see safeRedirect
    await goto(destination, { replaceState: true });
  }
</script>

<svelte:head><title>{m['auth.signIn.title']()}</title></svelte:head>

<form class="form" onsubmit={submit}>
  <h1 class="title">{m['auth.signIn.title']()}</h1>

  {#if failure}
    <!-- One message for "no such account" and "wrong password", always:
         telling them apart turns this form into a way to discover which
         addresses are registered. -->
    <ErrorState
      title={m['auth.signIn.failed']()}
      body={failure.code === ErrorCodes.invalidCredentials ? '' : failure.detail}
      requestIdLabel={m['error.reference']()}
      requestId={failure.requestId}
    />
  {/if}

  <Field label={m['auth.signIn.email']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextInput
        {id}
        {describedBy}
        {invalid}
        type="email"
        autocomplete="username"
        inputmode="email"
        required
        bind:value={email}
      />
    {/snippet}
  </Field>

  <Field label={m['auth.signIn.password']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextInput
        {id}
        {describedBy}
        {invalid}
        type="password"
        autocomplete="current-password"
        required
        bind:value={password}
      />
    {/snippet}
  </Field>

  <Button type="submit" variant="primary" size="lg" full loading={busy.showing}>
    {m['auth.signIn.submit']()}
  </Button>
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
</style>

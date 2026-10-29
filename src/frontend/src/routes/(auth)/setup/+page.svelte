<script lang="ts">
  import { onDestroy, onMount, untrack } from 'svelte';

  import { goto } from '$app/navigation';
  import { resolve } from '$app/paths';
  import { Button, Disclosure, SectionLabel } from '$ds';
  import { m } from '$shell/i18n';

  import FormField from '$features/auth/FormField.svelte';
  import FormFailure from '$features/auth/FormFailure.svelte';
  import SubmitButton from '$features/auth/SubmitButton.svelte';
  import { register } from '$features/auth/registration.svelte';
  import { session } from '$features/auth/session.svelte';
  import { createSubmission } from '$features/auth/submission.svelte';
  import { tips } from '$features/onboarding/tips.svelte';
  import DatabaseFields from '$features/instance/DatabaseFields.svelte';
  import LimitFields from '$features/instance/LimitFields.svelte';
  import ProxyFields from '$features/instance/ProxyFields.svelte';
  import SaveStatus from '$features/instance/SaveStatus.svelte';
  import SecureCookiesField from '$features/instance/SecureCookiesField.svelte';
  import SessionFields from '$features/instance/SessionFields.svelte';
  import TelemetryFields from '$features/instance/TelemetryFields.svelte';
  import { server, type SaveOutcome } from '$features/instance/stores/instance.svelte';
  import { variables } from '$features/instance/types';
  import { useServerDrafts, type ServerPart } from '$features/instance/useServerDrafts.svelte';

  /**
   * Fresh-instance setup in dependency order: database, then the network-dependent settings (secure cookies, trusted proxy), then the account.
   * The account is last because those settings restart the server and renaming the session cookie would sign an earlier account out.
   */
  let { data } = $props();

  type Step = 'database' | 'server' | 'account';

  // Fixed when the page opens, so "step 2 of 3" cannot change meaning.
  const steps: readonly Step[] =
    untrack(() => data.setup?.stage) === 'database'
      ? ['database', 'server', 'account']
      : ['server', 'account'];

  let step = $state<Step>(steps[0]!);
  const drafts = useServerDrafts();

  let displayName = $state('');
  let email = $state('');
  let password = $state('');
  let householdName = $state('');

  const submission = createSubmission();

  onDestroy(() => submission.dispose());

  onMount(() => (step === 'database' ? drafts.loadDatabase() : openServer()));

  async function openServer() {
    await drafts.loadServer();

    if (!drafts.serverDraft || !server.serverFacts) return;

    // The browser knows whether it will keep a Secure cookie on this page; a TLS-terminating proxy hides that from the server.
    // A secure context rather than https: alone, since http://localhost keeps them too.
    if (!server.serverFacts.pinned.has(variables.secure)) {
      drafts.serverDraft.secure = window.isSecureContext;
    }
  }

  /** Goes on once a save is in effect; stays, with the reason on screen, when it is not. */
  async function advance(part: ServerPart, result: SaveOutcome, next: () => Promise<void>) {
    if (result.kind === 'failed' || result.kind === 'stalled') {
      return;
    }

    // An existing Culina moved to a new server already has an administrator: nothing to set up.
    if (result.kind === 'applied' && result.setup.stage === 'complete') {
      await goto(resolve('/(auth)/login'), { replace: true });

      return;
    }

    drafts.dismissOutcome(part);
    await next();
  }

  async function connect(event: SubmitEvent) {
    event.preventDefault();

    const result = await drafts.saveDatabase();

    if (!result) return;

    await advance('database', result, async () => {
      step = 'server';
      await openServer();
    });
  }

  async function configure(event: SubmitEvent) {
    event.preventDefault();

    const result = await drafts.saveServer();

    if (!result) return;

    await advance('server', result, async () => {
      step = 'account';
    });
  }

  async function retry() {
    const part = step === 'database' ? 'database' : 'server';

    await advance(part, await drafts.retry(part), async () => {
      step = step === 'database' ? 'server' : 'account';

      if (step === 'server') await openServer();
    });
  }

  async function createAccount(event: SubmitEvent) {
    event.preventDefault();

    let landedIn: string | null = null;

    const succeeded = await submission.run(async () => {
      const outcome = await register({ email, displayName, password, householdName });

      if ('code' in outcome) {
        return outcome;
      }

      landedIn = outcome.householdId;

      // Registering signs you in; a second password prompt would be pointless.
      return session.signIn(email, password);
    });

    if (succeeded) {
      // The first account always gets a kitchen, so the tour of it starts there, as after signing up.
      if (landedIn) tips.startTour();

      await goto(resolve('/(app)'), { replace: true });
    }
  }

  const position = $derived(steps.indexOf(step) + 1);
</script>

<svelte:head><title>{m['setup.title']()}</title></svelte:head>

<div class="setup">
  <header class="intro">
    <SectionLabel tone="accent" spacing="loose">
      {m['setup.step']({ current: position, total: steps.length })}
    </SectionLabel>
    <h1 class="title">
      {#if step === 'database'}
        {m['setup.database.title']()}
      {:else if step === 'server'}
        {m['setup.server.title']()}
      {:else}
        {m['setup.account.title']()}
      {/if}
    </h1>
    <p class="subtitle">
      {#if step === 'database'}
        {m['setup.database.intro']()}
      {:else if step === 'server'}
        {m['setup.server.intro']()}
      {:else}
        {m['setup.account.intro']()}
      {/if}
    </p>
  </header>

  {#if step === 'database' && drafts.databaseDraft && server.databaseFacts}
    <form class="form" onsubmit={connect} novalidate>
      <DatabaseFields
        bind:draft={drafts.databaseDraft}
        facts={server.databaseFacts}
        disabled={drafts.busy}
      />

      <SaveStatus phase={server.phase} outcome={drafts.databaseOutcome} onretry={retry} />

      <Button type="submit" variant="primary" loading={drafts.busy}
        >{m['setup.database.submit']()}</Button
      >
    </form>
  {:else if step === 'server' && drafts.serverDraft && server.serverFacts}
    {@const facts = server.serverFacts}

    <form class="form" onsubmit={configure} novalidate>
      <SecureCookiesField
        bind:checked={drafts.serverDraft.secure}
        pinned={facts.pinned.has(variables.secure)}
        disabled={drafts.busy}
      />

      <ProxyFields bind:draft={drafts.serverDraft} {facts} disabled={drafts.busy} />

      <Disclosure summary={m['setup.server.advanced']()}>
        <div class="advanced">
          <SessionFields bind:draft={drafts.serverDraft} {facts} disabled={drafts.busy} />
          <LimitFields bind:draft={drafts.serverDraft} {facts} disabled={drafts.busy} />
          <TelemetryFields bind:draft={drafts.serverDraft} {facts} disabled={drafts.busy} />
        </div>
      </Disclosure>

      <SaveStatus phase={server.phase} outcome={drafts.serverOutcome} onretry={retry} />

      <Button type="submit" variant="primary" loading={drafts.busy}
        >{m['setup.server.submit']()}</Button
      >
    </form>
  {:else if step === 'account'}
    <form class="form" onsubmit={createAccount} novalidate>
      <FormFailure failure={submission.failure} />

      <FormField
        name="displayName"
        label={m['auth.register.displayName']()}
        autocomplete="name"
        autofocus
        bind:value={displayName}
        {submission}
      />

      <FormField
        name="email"
        label={m['auth.register.email']()}
        type="email"
        autocomplete="username"
        inputmode="email"
        bind:value={email}
        {submission}
      />

      <FormField
        name="password"
        label={m['auth.register.password']()}
        hint={m['auth.register.passwordHint']()}
        type="password"
        autocomplete="new-password"
        bind:value={password}
        {submission}
      />

      <FormField
        name="householdName"
        label={m['auth.register.householdName']()}
        hint={m['auth.register.householdHint']()}
        required={false}
        bind:value={householdName}
        {submission}
      />

      <SubmitButton label={m['setup.account.submit']()} {submission} />
    </form>
  {:else if server.error}
    <p class="failure" role="alert">{m['setup.unreadable']()}</p>
    <Button onclick={() => (step === 'database' ? drafts.loadDatabase() : openServer())}>
      {m['error.retry']()}
    </Button>
  {/if}
</div>

<style>
  .setup,
  .form,
  .advanced {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .advanced {
    padding-top: var(--space-3);
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
  }

  .intro {
    margin-bottom: var(--space-2);
  }

  .subtitle {
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
  }

  .failure {
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
